using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public enum UpdateStatus
    {
        UpToDate,
        Available,
        Failed
    }

    /// <summary>What a check found. <see cref="Latest"/> has three parts; it and <see cref="ReleaseUrl"/> are null when the check failed.</summary>
    public sealed class UpdateCheckResult
    {
        public static readonly UpdateCheckResult Failed = new UpdateCheckResult(UpdateStatus.Failed, null, null);

        public UpdateCheckResult(UpdateStatus status, Version latest, string releaseUrl)
        {
            Status = status;
            Latest = latest;
            ReleaseUrl = releaseUrl;
        }

        public UpdateStatus Status { get; }

        public Version Latest { get; }

        public string ReleaseUrl { get; }
    }

    /// <summary>The update check's state (HKCU in production). Tests use a fake so they never touch the registry.</summary>
    public interface IUpdateStateStore
    {
        /// <summary>The Settings toggle, on unless turned off.</summary>
        bool CheckForUpdates { get; }

        /// <summary>When the last check that read a release finished, in UTC.</summary>
        DateTimeOffset? LastCheck { get; set; }

        /// <summary>The newest release that check saw (<c>1.2.0</c>), so the notice shows on every start, not only after a check.</summary>
        string LatestVersion { get; set; }

        string LatestReleaseUrl { get; set; }

        /// <summary>The release whose notice was dismissed; only a newer one shows again.</summary>
        string DismissedVersion { get; set; }
    }

    public class RegistryUpdateStateStore : IUpdateStateStore
    {
        public bool CheckForUpdates
        {
            get { return RegistryStuff.GetCheckForUpdates(); }
        }

        public DateTimeOffset? LastCheck
        {
            get { return RegistryStuff.GetLastUpdateCheck(); }
            set { RegistryStuff.SetLastUpdateCheck(value ?? throw new ArgumentNullException(nameof(value))); }
        }

        public string LatestVersion
        {
            get { return RegistryStuff.GetString(RegistryStuff.LatestVersionKeyName); }
            set { RegistryStuff.SetRegistryValue(RegistryStuff.LatestVersionKeyName, value ?? string.Empty); }
        }

        public string LatestReleaseUrl
        {
            get { return RegistryStuff.GetString(RegistryStuff.LatestReleaseUrlKeyName); }
            set { RegistryStuff.SetRegistryValue(RegistryStuff.LatestReleaseUrlKeyName, value ?? string.Empty); }
        }

        public string DismissedVersion
        {
            get { return RegistryStuff.GetString(RegistryStuff.DismissedUpdateVersionKeyName); }
            set { RegistryStuff.SetRegistryValue(RegistryStuff.DismissedUpdateVersionKeyName, value ?? string.Empty); }
        }
    }

    /// <summary>
    /// Tells the GUI when a newer release is out (#171). It only reads the latest release and links to its page; nothing is
    /// downloaded or installed. At most one request a day on startup, none while the setting is off; <see cref="CheckNowAsync"/>
    /// always asks. Versions compare by their first three parts, since the assembly version has four (<c>1.1.1.0</c>) and in .NET
    /// <c>1.2.0</c> is less than <c>1.2.0.0</c>. Nothing here throws for a network, response or registry problem.
    /// </summary>
    public class UpdateChecker
    {
        public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

        private readonly IReleaseSource source;

        private readonly IUpdateStateStore store;

        private readonly Func<DateTimeOffset> now;

        /// <param name="currentVersion">The running version; compared by its first three parts.</param>
        /// <param name="now">The clock; tests pass their own.</param>
        public UpdateChecker(IReleaseSource source, IUpdateStateStore store, Version currentVersion, Func<DateTimeOffset> now = null)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            CurrentVersion = Normalize(currentVersion ?? throw new ArgumentNullException(nameof(currentVersion)));
            this.now = now ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>GitHub, the HKCU state and <see cref="Runner.Version"/>.</summary>
        public static UpdateChecker CreateDefault()
        {
            return new UpdateChecker(new GitHubReleaseSource(), new RegistryUpdateStateStore(), Version.Parse(Runner.Version));
        }

        /// <summary>The running version in three parts.</summary>
        public Version CurrentVersion { get; }

        public bool IsEnabled
        {
            get { return Read(() => store.CheckForUpdates, true); }
        }

        /// <summary>
        /// The startup check: asks GitHub unless the setting is off or a check already read a release in the last day
        /// (a last check in the future, from a clock that was set back, doesn't count). The result is stored; read it with <see cref="GetNotice"/>.
        /// </summary>
        /// <returns>Whether a request was made.</returns>
        public async Task<bool> CheckOnStartupAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return false;
            }

            var lastCheck = Read(() => store.LastCheck, null);
            var current = now();
            if (lastCheck.HasValue && lastCheck.Value <= current && current - lastCheck.Value < CheckInterval)
            {
                return false;
            }

            await CheckNowAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        /// <summary>Asks GitHub now, whatever the setting and the last check. Stores what it read.</summary>
        public async Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken = default)
        {
            LatestRelease release;
            try
            {
                release = await source.GetLatestAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (IsCheckFailure(e))
            {
                return UpdateCheckResult.Failed;
            }

            // A tag that isn't a version (v2.0.0-beta marked as latest by hand) counts as no update
            if (release == null || !TryParseTag(release.Tag, out var latest))
            {
                return UpdateCheckResult.Failed;
            }

            var url = GitHubReleaseSource.IsReleasePage(release.Url) ? release.Url : GitHubReleaseSource.ReleasesPageUrl;

            Write(() =>
            {
                store.LatestVersion = latest.ToString(3);
                store.LatestReleaseUrl = url;
                store.LastCheck = now();
            });

            return Describe(latest, url);
        }

        /// <summary>The newest release a check has seen, whether or not it's newer. Null before the first check.</summary>
        public UpdateCheckResult GetLatestKnown()
        {
            if (!TryParseVersion(Read(() => store.LatestVersion, null), out var latest))
            {
                return null;
            }

            return Describe(latest, ReadUrl());
        }

        /// <summary>
        /// What the creator's footer shows: the newest known release, when the setting is on, it's newer than this version and
        /// its notice wasn't dismissed. Null otherwise (after an upgrade too, since the stored release is then this one).
        /// </summary>
        public UpdateCheckResult GetNotice()
        {
            if (!IsEnabled)
            {
                return null;
            }

            var latest = GetLatestKnown();
            if (latest == null || latest.Status != UpdateStatus.Available)
            {
                return null;
            }

            if (TryParseVersion(Read(() => store.DismissedVersion, null), out var dismissed) && !IsNewer(latest.Latest, dismissed))
            {
                return null;
            }

            return latest;
        }

        /// <summary>Hides the notice for this release; a newer one shows again. A failure to store it is ignored.</summary>
        public void Dismiss(Version version)
        {
            if (version != null)
            {
                Write(() => store.DismissedVersion = Normalize(version).ToString(3));
            }
        }

        #region Versions

        /// <summary>Reads a release tag: <c>v1.2.0</c> or <c>1.2.0</c> (two to four parts). Anything else, a prerelease suffix included, is false.</summary>
        public static bool TryParseTag(string tag, out Version version)
        {
            var text = tag?.Trim() ?? string.Empty;
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            return TryParseVersion(text, out version);
        }

        public static bool TryParseVersion(string text, out Version version)
        {
            if (!string.IsNullOrWhiteSpace(text) && Version.TryParse(text.Trim(), out var parsed))
            {
                version = Normalize(parsed);
                return true;
            }

            version = null;
            return false;
        }

        /// <summary>The first three parts; a missing third part is 0. The fourth is dropped, as MSI upgrades ignore it.</summary>
        public static Version Normalize(Version version)
        {
            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }

        public static bool IsNewer(Version candidate, Version current)
        {
            return Normalize(candidate) > Normalize(current);
        }

        #endregion Versions

        private UpdateCheckResult Describe(Version latest, string url)
        {
            return new UpdateCheckResult(IsNewer(latest, CurrentVersion) ? UpdateStatus.Available : UpdateStatus.UpToDate, latest, url);
        }

        private string ReadUrl()
        {
            var url = Read(() => store.LatestReleaseUrl, null);
            return GitHubReleaseSource.IsReleasePage(url) ? url : GitHubReleaseSource.ReleasesPageUrl;
        }

        private static bool IsCheckFailure(Exception e)
        {
            return e is HttpRequestException || e is OperationCanceledException || e is JsonException ||
                e is InvalidDataException || e is IOException;
        }

        private static bool IsStoreFailure(Exception e)
        {
            return ContextMenuRegistrar.IsRegistryFailure(e);
        }

        private static T Read<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception e) when (IsStoreFailure(e))
            {
                return fallback;
            }
        }

        private static void Write(Action write)
        {
            try
            {
                write();
            }
            catch (Exception e) when (IsStoreFailure(e))
            {
                // Not stored: the next start checks again, or the notice shows again
            }
        }
    }
}
