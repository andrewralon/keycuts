using Microsoft.Win32;
using System;
using System.IO;

namespace keycuts.Common
{
    public class RegistryStuff
    {
        public static readonly RegistryKey CurrentUserContext = Registry.CurrentUser;

        public static readonly string StartPath = $@"Software\TeamRalon\{Runner.AppName}";

        public static readonly string OutputFolderKeyName = "OutputFolder";

        public static readonly string ForceOverwriteKeyName = "ForceOverwrite";

        /// <summary>The user's last choice. The installer reads it so an upgrade doesn't bring back a menu they turned off.</summary>
        public static readonly string RightClickContextMenuKeyName = "RightClickContextMenu";

        public static readonly string ThemeKeyName = "Theme";

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

        public static bool GetForceOverwrite(bool forceOverwrite)
        {
            return ReadValue(ForceOverwriteKeyName) is int raw ? raw != 0 : forceOverwrite;
        }

        /// <summary>Whether the verbs are really there, which is what the Settings checkbox should show.</summary>
        public static bool GetRightClickContextMenu()
        {
            return new ContextMenuRegistrar().IsRegistered();
        }

        public static ThemePreference GetThemePreference()
        {
            return ThemePreferences.Parse(ReadValue(ThemeKeyName) as string);
        }

        #endregion Get Methods

        #region Set Methods

        public static void SetRegistryValue(string keyName, object keyValue,
            RegistryValueKind registryValueKind = RegistryValueKind.String)
        {
            Console.WriteLine($"SetRegistryValue(\"{keyName}\", \"{keyValue}\", \"{registryValueKind}\")");

            using var appNameKey = CurrentUserContext.CreateSubKey(StartPath, true);

            appNameKey.SetValue(keyName, keyValue, registryValueKind);
        }

        public static void SetOutputFolder(string path)
        {
            SetRegistryValue(OutputFolderKeyName, path, RegistryValueKind.String);
        }

        public static void SetForceOverwrite(bool forceOverwrite)
        {
            SetRegistryValue(ForceOverwriteKeyName, forceOverwrite, RegistryValueKind.DWord);
        }

        public static void SetThemePreference(ThemePreference preference)
        {
            SetRegistryValue(ThemeKeyName, preference.ToString(), RegistryValueKind.String);
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
