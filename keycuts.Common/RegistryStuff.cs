using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;

namespace keycuts.Common
{
    public class RegistryStuff
    {
        public static readonly RegistryKey CurrentUserContext = Registry.CurrentUser;

        public static readonly string StartPath = $@"Software\TeamRalon\{Runner.AppName}";

        public static readonly string OutputFolderKeyName = "OutputFolder";

        /// <summary>The setting before #153, when it was "Force overwrite" (1 = replace without asking). Read, never written.</summary>
        public static readonly string ForceOverwriteKeyName = "ForceOverwrite";

        /// <summary>"Ask before replacing a keycut" (#153). Until it's saved once, the old ForceOverwrite value decides.</summary>
        public static readonly string AskBeforeReplacingKeyName = "AskBeforeReplacing";

        /// <summary>The user's last choice. The installer reads it so an upgrade doesn't bring back a menu they turned off.</summary>
        public static readonly string RightClickContextMenuKeyName = "RightClickContextMenu";

        public static readonly string ThemeKeyName = "Theme";

        /// <summary>Save new file and folder keycuts with environment variables (#31). Off unless set.</summary>
        public static readonly string UseEnvironmentVariablesKeyName = "UseEnvironmentVariables";

        /// <summary>Set once the first-run welcome panel was dismissed or used, so it never shows again.</summary>
        public static readonly string WelcomeDismissedKeyName = "WelcomeDismissed";

        /// <summary>
        /// Whether the Creator shows its description box (#205): written when the user shows or hides it, read when the Creator
        /// opens. Hidden unless set to 1.
        /// </summary>
        public static readonly string ShowDescriptionKeyName = "ShowDescription";

        /// <summary>Check GitHub for a newer release once a day (#171). On unless set to 0.</summary>
        public static readonly string CheckForUpdatesKeyName = "CheckForUpdates";

        /// <summary>When the last update check read a release, as a round-trip UTC timestamp.</summary>
        public static readonly string LastUpdateCheckKeyName = "LastUpdateCheck";

        /// <summary>
        /// The newest release that check saw: its version (<c>1.2.0</c>) and its page, as one <c>REG_MULTI_SZ</c> value (line 1 the
        /// version, line 2 the URL). One value, so a write can't pair one release's version with another's URL (#191). It says what
        /// GitHub last <i>reported</i>, not what is installed.
        /// </summary>
        public static readonly string LatestReleaseSeenKeyName = "LatestReleaseSeen";

        /// <summary>The release (<c>1.2.0</c>) whose update notice was dismissed.</summary>
        public static readonly string DismissedReleaseKeyName = "DismissedRelease";

        /// <summary>
        /// The update cache's names before #191. They are never read: the first write of a new value deletes them, and the cost of
        /// not migrating is one more daily check (or Check now) and, at worst, a dismissed notice showing once more.
        /// </summary>
        public static readonly string[] LegacyUpdateKeyNames = { "LatestVersion", "LatestReleaseUrl", "DismissedUpdateVersion" };

        #region Get Methods

        /// <summary>Reads never write: a missing or unreadable value just yields the default.</summary>
        private static object ReadValue(string keyName)
        {
            try
            {
                using var appNameKey = CurrentUserContext.OpenSubKey(StartPath);

                return appNameKey?.GetValue(keyName);
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return null;
            }
        }

        public static string GetOutputFolder(string path)
        {
            var value = ReadValue(OutputFolderKeyName) as string;

            return string.IsNullOrWhiteSpace(value) ? path : TrimFolder(value);
        }

        /// <summary>The installer stores the folder as Windows Installer resolves it, with a trailing backslash.</summary>
        public static string TrimFolder(string folder)
        {
            return Path.TrimEndingDirectorySeparator(folder);
        }

        public static bool GetAskBeforeReplacing(bool defaultValue)
        {
            return ResolveAskBeforeReplacing(ReadValue(AskBeforeReplacingKeyName), ReadValue(ForceOverwriteKeyName), defaultValue);
        }

        /// <summary>
        /// The pure part of <see cref="GetAskBeforeReplacing"/>: the new value if it's set, else the old ForceOverwrite
        /// value turned around (ForceOverwrite=1 means "don't ask"), else <paramref name="defaultValue"/>.
        /// </summary>
        public static bool ResolveAskBeforeReplacing(object askValue, object forceOverwriteValue, bool defaultValue)
        {
            if (askValue is int ask)
            {
                return ask != 0;
            }

            if (forceOverwriteValue is int force)
            {
                return force == 0;
            }

            return defaultValue;
        }

        /// <summary>Whether the verbs are really there, which is what the Settings checkbox should show.</summary>
        public static bool GetRightClickContextMenu()
        {
            return new ContextMenuRegistrar().IsRegistered();
        }

        public static bool GetUseEnvironmentVariables()
        {
            return ReadValue(UseEnvironmentVariablesKeyName) is int raw && raw != 0;
        }

        public static void SetUseEnvironmentVariables(bool useEnvironmentVariables)
        {
            SetRegistryValue(UseEnvironmentVariablesKeyName, useEnvironmentVariables, RegistryValueKind.DWord);
        }

        public static ThemePreference GetThemePreference()
        {
            return ThemePreferences.Parse(ReadValue(ThemeKeyName) as string);
        }

        public static bool GetWelcomeDismissed()
        {
            return ReadValue(WelcomeDismissedKeyName) is int raw && raw != 0;
        }

        public static bool GetShowDescription()
        {
            return ReadValue(ShowDescriptionKeyName) is int raw && raw != 0;
        }

        public static bool GetCheckForUpdates()
        {
            return !(ReadValue(CheckForUpdatesKeyName) is int raw) || raw != 0;
        }

        public static DateTimeOffset? GetLastUpdateCheck()
        {
            return ParseUpdateCheckTime(ReadValue(LastUpdateCheckKeyName) as string);
        }

        /// <summary>The stored form of <see cref="LastUpdateCheckKeyName"/>: round-trip ("o") UTC, the same on every culture.</summary>
        public static string FormatUpdateCheckTime(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>The inverse of <see cref="FormatUpdateCheckTime"/>; null for a missing or unreadable value, which means "never checked".</summary>
        public static DateTimeOffset? ParseUpdateCheckTime(string value)
        {
            return DateTimeOffset.TryParseExact(value, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
        }

        public static (string Version, string Url)? GetLatestReleaseSeen()
        {
            return ParseLatestReleaseSeen(ReadValue(LatestReleaseSeenKeyName));
        }

        /// <summary>The stored form of <see cref="LatestReleaseSeenKeyName"/>: the version, then the URL. Both are required, since an empty entry would end the multi-string early.</summary>
        public static string[] FormatLatestReleaseSeen(string version, string url)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A release needs a version.", nameof(version));
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A release needs a page.", nameof(url));
            }

            return new[] { version, url };
        }

        /// <summary>
        /// The inverse of <see cref="FormatLatestReleaseSeen"/>. Only a multi-string of exactly two non-empty entries counts;
        /// anything else (missing, another kind of value, an edited value) is null, which means "never checked".
        /// </summary>
        public static (string Version, string Url)? ParseLatestReleaseSeen(object value)
        {
            if (value is string[] { Length: 2 } lines && !string.IsNullOrWhiteSpace(lines[0]) && !string.IsNullOrWhiteSpace(lines[1]))
            {
                return (lines[0], lines[1]);
            }

            return null;
        }

        /// <summary>A string value, or null when it's missing, unreadable or not a string.</summary>
        public static string GetString(string keyName)
        {
            return ReadValue(keyName) as string;
        }

        #endregion Get Methods

        #region Set Methods

        public static void SetRegistryValue(string keyName, object keyValue,
            RegistryValueKind registryValueKind = RegistryValueKind.String)
        {
            using var appNameKey = CurrentUserContext.CreateSubKey(StartPath, true);

            appNameKey.SetValue(keyName, keyValue, registryValueKind);
        }

        public static void SetOutputFolder(string path)
        {
            SetRegistryValue(OutputFolderKeyName, path, RegistryValueKind.String);
        }

        public static void SetAskBeforeReplacing(bool askBeforeReplacing)
        {
            SetRegistryValue(AskBeforeReplacingKeyName, askBeforeReplacing ? 1 : 0, RegistryValueKind.DWord);
        }

        public static void SetThemePreference(ThemePreference preference)
        {
            SetRegistryValue(ThemeKeyName, preference.ToString(), RegistryValueKind.String);
        }

        public static void SetWelcomeDismissed()
        {
            SetRegistryValue(WelcomeDismissedKeyName, 1, RegistryValueKind.DWord);
        }

        public static void SetShowDescription(bool showDescription)
        {
            SetRegistryValue(ShowDescriptionKeyName, showDescription ? 1 : 0, RegistryValueKind.DWord);
        }

        public static void SetCheckForUpdates(bool checkForUpdates)
        {
            SetRegistryValue(CheckForUpdatesKeyName, checkForUpdates ? 1 : 0, RegistryValueKind.DWord);
        }

        public static void SetLastUpdateCheck(DateTimeOffset value)
        {
            SetRegistryValue(LastUpdateCheckKeyName, FormatUpdateCheckTime(value), RegistryValueKind.String);
        }

        /// <summary>One <c>SetValue</c>, so the version and URL are stored together or not at all. Removes the pre-#191 values afterwards.</summary>
        public static void SetLatestReleaseSeen(string version, string url)
        {
            SetRegistryValue(LatestReleaseSeenKeyName, FormatLatestReleaseSeen(version, url), RegistryValueKind.MultiString);
            RemoveLegacyUpdateValues();
        }

        public static void SetDismissedRelease(string version)
        {
            SetRegistryValue(DismissedReleaseKeyName, version ?? string.Empty, RegistryValueKind.String);
            RemoveLegacyUpdateValues();
        }

        // Best effort and idempotent: it runs after the new value is stored, so a failure here leaves harmless leftovers, and the next write tries again
        private static void RemoveLegacyUpdateValues()
        {
            try
            {
                using var appNameKey = CurrentUserContext.OpenSubKey(StartPath, true);

                if (appNameKey == null)
                {
                    return;
                }

                foreach (var name in LegacyUpdateKeyNames)
                {
                    appNameKey.DeleteValue(name, false);
                }
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                // Left for the next write
            }
        }

        /// <summary>Records the choice only after the verbs changed, so the two can't drift apart. False on failure.</summary>
        public static bool SetRightClickContextMenu(bool rightClickContextMenu)
        {
            var registrar = new ContextMenuRegistrar();
            var changed = rightClickContextMenu ? registrar.Register() : registrar.Unregister();

            if (changed)
            {
                SetRegistryValue(RightClickContextMenuKeyName, rightClickContextMenu, RegistryValueKind.DWord);
            }

            return changed;
        }

        #endregion Set Methods
    }
}
