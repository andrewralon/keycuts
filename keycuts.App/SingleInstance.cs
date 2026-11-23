using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// One keycuts per user on Windows (issue #307): it stays in the tray after its window closes, so a second start (the Start
    /// menu, Explorer's "keycut this!" with a target, a start at login) hands its arguments to the copy that runs and ends. The
    /// first copy owns a named mutex and listens on a named pipe; the arguments travel as one JSON array. macOS has no use for it:
    /// Launch Services runs one copy and sends it the files. A copy that can't reach the running one (it is hung) runs on its own
    /// rather than not at all.
    /// </summary>
    public sealed class SingleInstance : IDisposable
    {
        private const int MaxMessage = 64 * 1024;

        private readonly string name;

        private readonly Mutex mutex;

        private readonly CancellationTokenSource stop = new();

        private SingleInstance(string name, Mutex mutex)
        {
            this.name = name;
            this.mutex = mutex;
        }

        /// <summary>This process's ownership, kept for as long as it runs; null when it runs alone.</summary>
        public static SingleInstance? Current { get; set; }

        private static readonly object gate = new();

        private static readonly List<string[]> waiting = new();

        private static Action<string[]>? handler;

        /// <summary>
        /// Starts from before the app is up hand their arguments over at once and wait here: the first copy listens from its first
        /// line of Main, since a second start gives up after a few seconds. <paramref name="received"/> is called, on a worker
        /// thread, with those and every later start.
        /// </summary>
        public static void OnSecondStart(Action<string[]> received)
        {
            string[][] early;
            lock (gate)
            {
                handler = received;
                early = waiting.ToArray();
                waiting.Clear();
            }

            foreach (var args in early)
            {
                received(args);
            }
        }

        /// <summary>For <see cref="Listen"/>: passes a start on, or keeps it until <see cref="OnSecondStart"/> says what to do.</summary>
        public static void Deliver(string[] args)
        {
            Action<string[]>? target;
            lock (gate)
            {
                target = handler;
                if (target == null)
                {
                    waiting.Add(args);
                }
            }

            target?.Invoke(args);
        }

        /// <summary>Per user, and apart for a Debug run, which never meets the installed copy.</summary>
        public static string DefaultName()
        {
            return "keycuts-" + Environment.UserName + (DebugOverrides.GetOutputFolder() != null ? "-debug" : "");
        }

        /// <summary>The owner if this is the first copy; null when another copy runs.</summary>
        public static SingleInstance? TryOwn(string name)
        {
            var mutex = new Mutex(true, name, out var created);
            if (!created)
            {
                mutex.Dispose();
                return null;
            }

            return new SingleInstance(name, mutex);
        }

        /// <summary>Hands <paramref name="args"/> to the running copy; false when none answers within the timeout.</summary>
        public static bool Send(string name, IReadOnlyList<string> args, int timeoutMilliseconds = 3000)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", name, PipeDirection.Out);
                client.Connect(timeoutMilliseconds);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(args));
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Calls <paramref name="received"/> with each start's arguments, on a worker thread, until disposed.</summary>
        public void Listen(Action<string[]> received)
        {
            var token = stop.Token;
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        await server.WaitForConnectionAsync(token);
                        var text = await ReadAsync(server, token);
                        if (JsonSerializer.Deserialize<string[]>(text) is { } args)
                        {
                            received(args);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
                    {
                        // A client that left half way, or sent what isn't ours: wait for the next
                        System.Diagnostics.Debug.WriteLine("keycuts: a second start sent something unreadable: " + ex.Message);
                    }
                }
            }, token);
        }

        private static async Task<string> ReadAsync(Stream stream, CancellationToken token)
        {
            var buffer = new byte[MaxMessage];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), token);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }

            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        public void Dispose()
        {
            stop.Cancel();
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Released from another thread than the one that took it: the process ending frees it anyway
            }

            mutex.Dispose();
            stop.Dispose();
        }
    }
}
