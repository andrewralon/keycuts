using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class Settings
    {
        public const string InvalidOutputFolderMessage = "Enter a full path for the output folder, such as C:\\Shortcuts.";

        public const string PathUpdateFailedMessage = "The output folder was saved, but your PATH couldn\u2019t be updated.";

        public const string ContextMenuFailedMessage = "Couldn\u2019t update the right-click menu.";

        private Runner runner;

        public string OutputFolder { get; set; } = string.Empty;

        public bool ForceOverwrite { get; set; }

        /// <summary>Save new file and folder keycuts with environment variables (#31).</summary>
        public bool UseEnvironmentVariables { get; set; }

        public bool RightClickContextMenus { get; set; }

        public ThemePreference Theme { get; set; }

        public Settings()
        {
            runner = new Runner();
        }

        public void LoadSettings()
        {
            OutputFolder = runner.GetOutputFolder();
            ForceOverwrite = runner.GetForceOverwrite();
            UseEnvironmentVariables = runner.GetUseEnvironmentVariables();
            RightClickContextMenus = runner.GetRightClickContextMenu();
            Theme = runner.GetThemePreference();
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

                runner.SetForceOverwrite(ForceOverwrite);
                runner.SetUseEnvironmentVariables(UseEnvironmentVariables);
                runner.SetThemePreference(Theme);

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
