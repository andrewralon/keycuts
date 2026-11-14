using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;

namespace keycuts.GUI
{
    public class Settings
    {
        public const string InvalidOutputFolderMessage = "Enter a full path for the output folder, such as C:\\Shortcuts.";

        public const string PathUpdateFailedMessage = "The output folder was saved, but your PATH couldn\u2019t be updated.";

        public const string ContextMenuFailedMessage = "Couldn\u2019t update the right-click menu.";

        private Runner runner;

        public string OutputFolder { get; set; } = string.Empty;

        /// <summary>"Ask before replacing a keycut" (#153), which replaced "Force overwrite" with the opposite meaning.</summary>
        public bool AskBeforeReplacing { get; set; } = Runner.DefaultAskBeforeReplacing;

        /// <summary>Save new file and folder keycuts with environment variables (#31).</summary>
        public bool UseEnvironmentVariables { get; set; }

        public bool RightClickContextMenus { get; set; }

        public ThemePreference Theme { get; set; }

        /// <summary>Check GitHub for a newer release once a day (#171). On by default.</summary>
        public bool CheckForUpdates { get; set; } = true;

        public Settings()
        {
            runner = new Runner();
        }

        /// <summary>The icon a new install shows (#218, restore defaults).</summary>
        public const AppIconChoice DefaultAppIcon = AppIconChoice.BlackAndWhite;

        /// <summary>
        /// What a new install has, for Settings' restore defaults (#218): ask before replacing, the right-click menu on (the
        /// installer adds it), no environment variables, the system theme, and update checks on. The output folder isn't a
        /// default here: restore defaults never touches it.
        /// </summary>
        public static Settings Defaults()
        {
            return new Settings
            {
                AskBeforeReplacing = Runner.DefaultAskBeforeReplacing,
                RightClickContextMenus = true,
                UseEnvironmentVariables = false,
                Theme = ThemePreference.System,
                CheckForUpdates = true
            };
        }

        public void LoadSettings()
        {
            OutputFolder = runner.GetOutputFolder();
            AskBeforeReplacing = runner.GetAskBeforeReplacing();
            UseEnvironmentVariables = runner.GetUseEnvironmentVariables();
            RightClickContextMenus = runner.GetRightClickContextMenu();
            Theme = runner.GetThemePreference();
            CheckForUpdates = runner.GetCheckForUpdates();
        }

        public static bool IsValidOutputFolder(string? folder)
        {
            return !string.IsNullOrWhiteSpace(folder) &&
                folder.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
                Path.IsPathFullyQualified(folder);
        }

        /// <summary>
        /// Stores every setting but the output folder: a toggle, the theme or restore defaults changes these, and must not write a
        /// folder that another window or the CLI may have changed since this one was loaded. Returns a message for the user when
        /// something failed, otherwise null.
        /// </summary>
        public string? SaveOptions()
        {
            try
            {
                runner.SetAskBeforeReplacing(AskBeforeReplacing);
                runner.SetUseEnvironmentVariables(UseEnvironmentVariables);
                runner.SetThemePreference(Theme);
                runner.SetCheckForUpdates(CheckForUpdates);

                return runner.SetRightClickContextMenu(RightClickContextMenus) ? null : ContextMenuFailedMessage;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is SecurityException || e is IOException)
            {
                return SaveFailedPrefix + e.Message;
            }
        }

        /// <summary>
        /// Stores <see cref="OutputFolder"/> and moves PATH to it. Says whether the folder itself was stored, apart from any message:
        /// PATH can fail after the folder is stored, and then the folder still counts.
        /// </summary>
        public FolderSaveResult SaveOutputFolder()
        {
            if (!IsValidOutputFolder(OutputFolder))
            {
                return new FolderSaveResult(false, InvalidOutputFolderMessage);
            }

            try
            {
                return new FolderSaveResult(true, runner.ChangeOutputFolder(OutputFolder) ? null : PathUpdateFailedMessage);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is SecurityException || e is IOException)
            {
                // The folder is written before PATH, so a failure may come after it was stored: ask the registry
                return new FolderSaveResult(IsStored(OutputFolder), SaveFailedPrefix + e.Message);
            }
        }

        /// <summary>The start of the message for a save the registry refused.</summary>
        public const string SaveFailedPrefix = "Couldn\u2019t save settings: ";

        private bool IsStored(string folder)
        {
            try
            {
                return string.Equals(runner.GetOutputFolder(), folder, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is SecurityException || e is IOException)
            {
                return false;
            }
        }
    }

    /// <summary>What saving the output folder did: whether the folder itself was stored, and a message for the user, or null.</summary>
    public sealed record FolderSaveResult(bool Stored, string? Message);
}
