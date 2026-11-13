using System;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// Where the Settings icon picker reads the icon and applies a pick. A pick takes effect at once, not on Save, so it goes
    /// here rather than through <see cref="ISettingsStore"/>.
    /// </summary>
    public interface IAppIconService
    {
        AppIconChoice Choice { get; }

        /// <summary>Saves the choice and shows it everywhere now. Returns a message for the user when it couldn't be saved.</summary>
        string? Apply(AppIconChoice choice);
    }

    /// <summary>The app's: the registry and <see cref="AppIconManager"/>.</summary>
    public class AppIconService : IAppIconService
    {
        public const string SaveFailedMessage = "The icon changed, but it couldn’t be saved for next time.";

        public AppIconChoice Choice
        {
            get { return AppIconManager.Choice; }
        }

        public string? Apply(AppIconChoice choice)
        {
            // Shown even when the registry refuses, so the pick still does something this session
            AppIconManager.SetChoice(choice);

            try
            {
                RegistryStuff.SetAppIcon(choice);
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
        public InMemoryAppIconService(AppIconChoice choice = AppIconChoice.BlackAndWhite)
        {
            Choice = choice;
        }

        public AppIconChoice Choice { get; private set; }

        public string? Apply(AppIconChoice choice)
        {
            Choice = choice;
            return null;
        }
    }
}
