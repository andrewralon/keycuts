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

        private static readonly string patternCLSID = "\".+explorer.exe\" \"[shell:]?::({.+})\"";
        private static readonly string patternFolder = "\".+explorer.exe\" \"(.+)\"";
        private static readonly string patternHostsFile = "START \"\" \\/[BD] \".+\" \"(.+)hosts\"$";
        private static readonly string patternFile = "START \"\" \\/[BD] \".+\" \"(.+)\"$";
        private static readonly string patternCommand = "START \"\" \\/[BD] \".+\" (.+)";
        private static readonly string patternUrl = "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

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
            var skippedFiles = new List<string>();

            foreach (var batFile in batFiles)
            {
                var allLines = File.ReadAllLines(batFile);
                var lines = allLines
                    .Where(x => x.Contains(explorer) ||
                                x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

                var bat = new Bat
                {
                    Path = batFile,
                    Shortcut = Path.GetFileNameWithoutExtension(batFile)
                };

                foreach (var line in lines)
                {
                    bat.Command = line;

                    if (IsCLSIDKey(line, out string clsidKey))
                    {
                        bat.Destination = clsidKey;
                        bat.Type = ShortcutType.CLSIDKey;
                        break;
                    }
                    else if (IsFolder(line, out string folder))
                    {
                        bat.Destination = folder;
                        bat.Type = ShortcutType.Folder;
                        break;
                    }
                    else if (IsHostsFile(line, out string hostsFile))
                    {
                        bat.Destination = hostsFile;
                        bat.Type = ShortcutType.HostsFile;
                        break;
                    }
                    else if (IsFile(line, out string file))
                    {
                        bat.Destination = file;
                        bat.Type = ShortcutType.File;
                        break;
                    }
                    else if (IsCommand(line, out string command))
                    {
                        bat.Destination = command;
                        bat.Type = ShortcutType.Command;
                        break;
                    }
                    else if (IsValidUrl(line, out string url))
                    {
                        bat.Destination = url;
                        bat.Type = ShortcutType.Url;
                        break;
                    }

                    bat.Type = ShortcutType.Unknown;
                }

                if (!string.IsNullOrEmpty(bat.Shortcut) &&
                    !string.IsNullOrEmpty(bat.Command) &&
                    bat.Type != ShortcutType.Unknown)
                {
                    bats.Add(bat);
                }
                else
                {
                    skippedFiles.Add(batFile);
                }
            }

            if (skippedFiles.Any())
            {
                Console.WriteLine("*** Skipped files: ***");
                skippedFiles.ForEach(t => Console.WriteLine(t));
            }

            return bats;
        }

        private static bool IsCLSIDKey(string line, out string clsidKey)
        {
            return MatchesRegex(patternCLSID, line, out clsidKey);
        }

        private static bool IsFolder(string line, out string folder)
        {
            return MatchesRegex(patternFolder, line, out folder);
        }

        private static bool IsHostsFile(string line, out string hostsFile)
        {
            return MatchesRegex(patternHostsFile, line, out hostsFile);
        }

        private static bool IsFile(string line, out string file)
        {
            return MatchesRegex(patternFile, line, out file);
        }

        private static bool IsCommand(string line, out string command)
        {
            return MatchesRegex(patternCommand, line, out command);
        }

        private static bool IsValidUrl(string line, out string url)
        {
            return MatchesRegex(patternUrl, line, out url);
            //return Shortcut.IsValidUrl(line.Substring(start.Length), out url);
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
