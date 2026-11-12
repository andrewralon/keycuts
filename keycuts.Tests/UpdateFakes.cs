#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;

namespace keycuts.Tests
{
    /// <summary>A release source that never touches the network (#171).</summary>
    sealed class FakeReleaseSource : IReleaseSource
    {
        public Func<LatestRelease> Next { get; set; } = () => Release("v1.1.1");

        public int Calls { get; private set; }

        public static LatestRelease Release(string tag, string? url = null)
        {
            return new LatestRelease(tag, url ?? "https://github.com/andrewralon/keycuts/releases/tag/" + tag);
        }

        public Task<LatestRelease> GetLatestAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Next());
        }
    }

    /// <summary>The update check's state without the registry.</summary>
    sealed class FakeUpdateState : IUpdateStateStore
    {
        private DateTimeOffset? lastCheck;

        private string? latestVersion;

        private string? latestReleaseUrl;

        private string? dismissedVersion;

        public bool Enabled { get; set; } = true;

        /// <summary>Thrown by every read, as an unreadable registry key would.</summary>
        public Exception? ReadFailure { get; set; }

        /// <summary>Thrown by every write.</summary>
        public Exception? WriteFailure { get; set; }

        public int Writes { get; private set; }

        public bool CheckForUpdates
        {
            get { return Read(Enabled); }
        }

        public DateTimeOffset? LastCheck
        {
            get { return Read(lastCheck); }
            set { Write(); lastCheck = value; }
        }

        public string LatestVersion
        {
            get { return Read(latestVersion)!; }
            set { Write(); latestVersion = value; }
        }

        public string LatestReleaseUrl
        {
            get { return Read(latestReleaseUrl)!; }
            set { Write(); latestReleaseUrl = value; }
        }

        public string DismissedVersion
        {
            get { return Read(dismissedVersion)!; }
            set { Write(); dismissedVersion = value; }
        }

        private T Read<T>(T value)
        {
            if (ReadFailure != null)
            {
                throw ReadFailure;
            }

            return value;
        }

        private void Write()
        {
            if (WriteFailure != null)
            {
                throw WriteFailure;
            }

            Writes++;
        }
    }

    static class UpdateFakes
    {
        public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

        /// <summary>A checker running 1.1.1.0, as <see cref="Runner.Version"/> reports four parts.</summary>
        public static UpdateChecker Checker(FakeReleaseSource source, FakeUpdateState state, string current = "1.1.1.0", Func<DateTimeOffset>? now = null)
        {
            return new UpdateChecker(source, state, Version.Parse(current), now ?? (() => Now));
        }
    }
}
