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

        /// <summary>Saves everything it can. Returns a message for the user when something failed, otherwise null.</summary>
        public string? SaveSettings()
        {
            if (!IsValidOutputFolder(OutputFolder))
            {
                return InvalidOutputFolderMessage;
            }

            try
            {
                var problems = new List<string>();

                if (!runner.ChangeOutputFolder(OutputFolder))
                {
                    problems.Add(PathUpdateFailedMessage);
                }

                runner.SetAskBeforeReplacing(AskBeforeReplacing);
                runner.SetUseEnvironmentVariables(UseEnvironmentVariables);
                runner.SetThemePreference(Theme);
                runner.SetCheckForUpdates(CheckForUpdates);

                if (!runner.SetRightClickContextMenu(RightClickContextMenus))
                {
                    problems.Add(ContextMenuFailedMessage);
                }

                return problems.Count == 0 ? null : string.Join(Environment.NewLine, problems);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is SecurityException || e is IOException)
            {
                return $"Couldn\u2019t save settings: {e.Message}";
            }
        }
    }
}
