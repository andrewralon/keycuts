using System;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// Where the Settings icon picker reads the icon and applies a pick. A pick takes effect at once, not on Save, so it goes
    /// here rather than through <see cref="ISettingsStore"/>. So does the transparent background.
    /// </summary>
    public interface IAppIconService
    {
        AppIconChoice Choice { get; }

        /// <summary>Whether the windows and the shortcuts show the icon without its background.</summary>
        bool Transparent { get; }

        /// <summary>Saves the choice and shows it everywhere now. Returns a message for the user when it couldn't be saved.</summary>
        string? Apply(AppIconChoice choice);

        /// <summary>Saves the transparent background and shows it everywhere now. Returns a message when it couldn't be saved.</summary>
        string? ApplyTransparent(bool transparent);
    }

    /// <summary>The app's: the registry and <see cref="AppIconManager"/>.</summary>
    public class AppIconService : IAppIconService
    {
        public const string SaveFailedMessage = "The icon changed, but it couldn’t be saved for next time.";

        public AppIconChoice Choice
        {
            get { return AppIconManager.Choice; }
        }

        public bool Transparent
        {
            get { return AppIconManager.Transparent; }
        }

        public string? Apply(AppIconChoice choice)
        {
            // Shown even when the registry refuses, so the pick still does something this session
            AppIconManager.SetChoice(choice);
            return Save(() => RegistryStuff.SetAppIcon(choice));
        }

        public string? ApplyTransparent(bool transparent)
        {
            AppIconManager.SetTransparent(transparent);
            return Save(() => RegistryStuff.SetAppIconTransparent(transparent));
        }

        private static string? Save(Action write)
        {
            try
            {
                write();
                return null;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is System.Security.SecurityException || e is System.IO.IOException)
            {
                return SaveFailedMessage;
            }
        }
    }

    /// <summary>Keeps the choice in memory only: the default for a <see cref="SettingsViewModel"/> made without one, as in tests.</summary>
    public class InMemoryAppIconService : IAppIconService
    {
        public InMemoryAppIconService(AppIconChoice choice = AppIconChoice.BlackAndWhite, bool transparent = false)
        {
            Choice = choice;
            Transparent = transparent;
        }

        public AppIconChoice Choice { get; private set; }

        public bool Transparent { get; private set; }

        public string? Apply(AppIconChoice choice)
        {
            Choice = choice;
            return null;
        }

        public string? ApplyTransparent(bool transparent)
        {
            Transparent = transparent;
            return null;
        }
    }
}
