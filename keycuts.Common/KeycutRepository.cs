using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public enum KeycutRepositoryError
    {
        NotFound,
        AccessDenied,
        InvalidPath,
        InUse,
        Unknown
    }

    public class KeycutRepositoryException : Exception
    {
        public KeycutRepositoryError Error { get; }

        public KeycutRepositoryException(KeycutRepositoryError error, string message, Exception inner = null)
            : base(message, inner)
        {
            Error = error;
        }
    }

    /// <summary>Reads and deletes the keycut .bat files in one output folder.</summary>
    public class KeycutRepository
    {
        public static readonly TimeSpan DefaultCheckTimeout = TimeSpan.FromSeconds(2);

        private const int MaxParallelChecks = 8;

        private readonly string _outputFolder;

        public KeycutRepository(string outputFolder)
        {
            _outputFolder = outputFolder;
        }

        public string OutputFolder => _outputFolder;

        /// <summary>Production repository for the configured output folder.</summary>
        public static KeycutRepository CreateDefault()
        {
            return new KeycutRepository(new Runner().GetOutputFolder());
        }

        /// <summary>
        /// Loads every parseable keycut. A missing folder yields an empty list (it is never created here).
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The folder can't be read.</exception>
        public Task<IReadOnlyList<Keycut>> LoadAllAsync(CancellationToken ct = default)
        {
            return Task.Run(() => LoadAll(ct), ct);
        }

        public bool Exists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var fileName = name + ShortcutFile.Extension;

                return Directory.Exists(_outputFolder) &&
                    Directory.EnumerateFiles(_outputFolder, "*" + ShortcutFile.Extension)
                        .Any(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            var all = await LoadAllAsync(ct).ConfigureAwait(false);

            return all.Count;
        }

        /// <exception cref="KeycutRepositoryException">The file is already gone, locked, or can't be deleted.</exception>
        public Task DeleteAsync(Keycut keycut, CancellationToken ct = default)
        {
            return Task.Run(() => Delete(keycut), ct);
        }

        /// <summary>
        /// Sets <see cref="Keycut.DestinationStatus"/> for File/Folder keycuts. URLs, commands and
        /// shell (CLSID) keys are never checked. Each check has its own timeout; on timeout the status stays Unknown.
        /// </summary>
        public Task CheckDestinationsAsync(IEnumerable<Keycut> keycuts, CancellationToken ct = default)
        {
            return CheckDestinationsAsync(keycuts, DefaultCheckTimeout, ct);
        }

        public async Task CheckDestinationsAsync(IEnumerable<Keycut> keycuts, TimeSpan timeout, CancellationToken ct = default)
        {
            var checkable = keycuts.Where(IsCheckable).ToList();

            using (var gate = new SemaphoreSlim(MaxParallelChecks))
            {
                var tasks = checkable.Select(async keycut =>
                {
                    await gate.WaitAsync(ct).ConfigureAwait(false);

                    try
                    {
                        var status = await CheckOneAsync(keycut.Destination, timeout, ct).ConfigureAwait(false);

                        if (status != DestinationStatus.Unknown)
                        {
                            keycut.DestinationStatus = status;
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        private IReadOnlyList<Keycut> LoadAll(CancellationToken ct)
        {
            var keycuts = new List<Keycut>();

            if (string.IsNullOrWhiteSpace(_outputFolder) || _outputFolder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw new KeycutRepositoryException(KeycutRepositoryError.InvalidPath, $"Invalid keycuts folder \"{_outputFolder}\".");
            }

            if (!Directory.Exists(_outputFolder))
            {
                return keycuts;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(_outputFolder, "*" + ShortcutFile.Extension);
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't read keycuts folder \"{_outputFolder}\".");
            }

            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();

                var keycut = TryParse(file);

                if (keycut != null)
                {
                    keycuts.Add(keycut);
                }
            }

            return keycuts;
        }

        internal static Keycut TryParse(string file)
        {
            try
            {
                var shortcut = new ShortcutFile(file);

                if (string.IsNullOrEmpty(shortcut.Shortcut) ||
                    string.IsNullOrEmpty(shortcut.Command) ||
                    shortcut.Type == ShortcutType.Unknown)
                {
                    Debug.WriteLine($"keycuts: skipped unparseable file \"{file}\"");
                    return null;
                }

                return new Keycut
                {
                    Name = shortcut.Shortcut,
                    Kind = KeycutKinds.FromShortcutType(shortcut.Type),
                    RawType = shortcut.Type,
                    Description = shortcut.Description ?? "",
                    Destination = (shortcut.Destination ?? "").Trim('"'),
                    FilePath = file
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"keycuts: skipped \"{file}\": {ex.Message}");
                return null;
            }
        }

        private static void Delete(Keycut keycut)
        {
            var path = keycut.FilePath;

            try
            {
                if (!File.Exists(path))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{path}\" no longer exists.");
                }

                File.Delete(path);
            }
            catch (KeycutRepositoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't delete \"{path}\".");
            }
        }

        private static bool IsCheckable(Keycut keycut)
        {
            return !string.IsNullOrWhiteSpace(keycut.Destination) &&
                (keycut.RawType == ShortcutType.File ||
                 keycut.RawType == ShortcutType.Folder ||
                 keycut.RawType == ShortcutType.HostsFile);
        }

        private static async Task<DestinationStatus> CheckOneAsync(string destination, TimeSpan timeout, CancellationToken ct)
        {
            var work = Task.Run(() =>
            {
                try
                {
                    var path = Environment.ExpandEnvironmentVariables(destination);

                    return File.Exists(path) || Directory.Exists(path) ?
                        DestinationStatus.Ok :
                        DestinationStatus.Missing;
                }
                catch (Exception)
                {
                    return DestinationStatus.Unknown;
                }
            });

            var finished = await Task.WhenAny(work, Task.Delay(timeout, ct)).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            return finished == work ? await work.ConfigureAwait(false) : DestinationStatus.Unknown;
        }

        private static KeycutRepositoryException Translate(Exception ex, string message)
        {
            var error = ex switch
            {
                UnauthorizedAccessException => KeycutRepositoryError.AccessDenied,
                FileNotFoundException => KeycutRepositoryError.NotFound,
                DirectoryNotFoundException => KeycutRepositoryError.NotFound,
                PathTooLongException => KeycutRepositoryError.InvalidPath,
                ArgumentException => KeycutRepositoryError.InvalidPath,
                NotSupportedException => KeycutRepositoryError.InvalidPath,
                IOException => KeycutRepositoryError.InUse,
                _ => KeycutRepositoryError.Unknown
            };

            return new KeycutRepositoryException(error, message, ex);
        }
    }
}
