using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace keycuts.Batmanager
{
    public class BatFormLogic
    {
        #region Fields

        private static readonly string explorer = "\\explorer.exe\"";
        private static readonly string start = "START ";

        private static readonly string patternFolder = "\".+explorer.exe\" \"(.+)\"";
        private static readonly string patternFile = "START \"\" \\/[BD] \".+\" \"(.+)\"$";
        private static readonly string patternCommand = "START \"\" \\/[BD] \".+\" (.+)";

        #endregion Fields

        #region Public Methods

        public static void PopulateDataGrid(DataGrid dataGrid)
        {
            var outputFolder = RegistryStuff.GetOutputFolder(@"C:\Shortcuts");
            var batFiles = Directory.GetFiles(outputFolder, "*.bat").ToList();
            var bats = ParseBats(batFiles);
            dataGrid.ItemsSource = bats;
        }

        public static List<Bat> ParseBats(List<string> batFiles)
        {
            var bats = new List<Bat>();

            foreach (var batFile in batFiles)
            {
                var allLines = File.ReadAllLines(batFile);
                var lines = allLines
                    .Where(x => x.Contains(explorer) ||
                                x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

                var bat = new Bat();

                foreach (var line in lines)
                {
                    bat.Command = line;
                    bat.Shortcut = Path.GetFileNameWithoutExtension(batFile);

                    if (IsFolder(line, out string folder))
                    {
                        bat.Destination = folder;
                        bat.ShortcutType = ShortcutType.Folder;
                        break;
                    }
                    else if (IsFile(line, out string file))
                    {
                        bat.Destination = file;
                        bat.ShortcutType = ShortcutType.File;
                        break;
                    }
                    else if (Shortcut.IsValidUrl(line.Substring(start.Length), out string newUrl))
                    {
                        bat.Destination = newUrl;
                        bat.ShortcutType = ShortcutType.Url;
                        break;
                    }
                    else if (IsCommand(line, out string command))
                    {
                        bat.Destination = command;
                        bat.ShortcutType = ShortcutType.Command;
                        break;
                    }

                    bat.ShortcutType = ShortcutType.Unknown;
                }

                if (!string.IsNullOrEmpty(bat.Shortcut) &&
                    !string.IsNullOrEmpty(bat.Command) &&
                    bat.ShortcutType != ShortcutType.Unknown)
                {
                    bats.Add(bat);
                }
            }

            return bats;
        }

        private static bool IsFolder(string line, out string folder)
        {
            return MatchesRegex(patternFolder, line, out folder);
        }

        private static bool IsFile(string line, out string file)
        {
            return MatchesRegex(patternFile, line, out file);
        }

        private static bool IsCommand(string line, out string command)
        {
            return MatchesRegex(patternCommand, line, out command);
        }

        public static bool MatchesRegex(string pattern, string line, out string result)
        {
            result = "";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(line);

            if (match.Success)
            {
                result = match.Groups[1].Value;
            }

            return match.Success;
        }

        #endregion Public Methods
    }
}
