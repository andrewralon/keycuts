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

        private static readonly string startBD = "START \"\" \\/[BD] \".+\" \"([A-Za-z0-9:\\ \\/\\-\\.\\(\\)\\%]+)\"$";
        private static readonly string patternFolder = "\".+explorer.exe\" \"(.+)\"";

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

                    if (IsFolder(line, out string result))
                    {
                        bat.Destination = result;
                        bat.ShortcutType = ShortcutType.Folder;
                        break;
                    }
                    else if (MatchesRegex(start, line, out string notFolder))
                    {

                    }
                    else if (line.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // It's NOT a folder! -- Could be File, Url, HostsFile, or CLSIDKey
                        bat.Command = line;

                        Console.WriteLine($"Line: {line}");

                        var url = line.Substring(start.Length);
                        if (Shortcut.IsValidUrl(url, out string newUrl))
                        {
                            bat.Destination = newUrl;
                            bat.ShortcutType = ShortcutType.Url;
                        }
                        else
                        {
                            var regex = new Regex(startBD, RegexOptions.IgnoreCase);
                            var match = regex.Match(line);

                            if (match.Success)
                            {
                                // It's a File!
                                bat.Destination = match.Groups[1].Value;
                                bat.ShortcutType = ShortcutType.File;
                            }
                            else
                            {
                                bat.ShortcutType = ShortcutType.Command;
                            }
                        }

                        break;
                    }
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

        private static bool IsFolder(string line, out string result)
        {
            return MatchesRegex(patternFolder, line, out result);
        }

        private static bool IsFile(string line, out string result)
        {
            result = "";
            return false;

            //return MatchesRegex(patternXXXX, line, out result);
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
