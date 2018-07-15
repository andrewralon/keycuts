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
    public class Bat
    {
        private readonly string explorer = "\\explorer.exe\"";
        private readonly string start = "START ";

        private readonly string patternCLSID = "\".+explorer.exe\" \"[shell:]?::({.+})\"";
        private readonly string patternFolder = "\".+explorer.exe\" \"(.+)\"";
        private readonly string patternHostsFile = "START \"\" \\/[BD] \".+\" \"(.+)hosts\"$";
        private readonly string patternFile = "START \"\" \\/[BD] \".+\" \"(.+)\"$";
        private readonly string patternCommand = "START \"\" \\/[BD] \".+\" (.+)";
        private readonly string patternUrl = "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

        public string Path { get; set; }
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Destination { get; set; }
        public string OpenWithApp { get; set; }
        public string Command { get; set; }

        public Bat()
        {
        }

        public Bat(string batFile, DataGrid dgBats)
        {
            var allLines = File.ReadAllLines(batFile);
            var lines = allLines
                .Where(x => x.Contains(explorer) ||
                            x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            Path = batFile;
            Shortcut = System.IO.Path.GetFileNameWithoutExtension(batFile);

            foreach (var line in lines)
            {
                Command = line;

                if (IsCLSIDKey(line, out string clsidKey))
                {
                    Destination = clsidKey;
                    Type = ShortcutType.CLSIDKey;
                    break;
                }
                else if (IsFolder(line, out string folder))
                {
                    Destination = folder;
                    Type = ShortcutType.Folder;
                    break;
                }
                else if (IsHostsFile(line, out string hostsFile))
                {
                    Destination = hostsFile;
                    Type = ShortcutType.HostsFile;
                    break;
                }
                else if (IsFile(line, out string file))
                {
                    Destination = file;
                    Type = ShortcutType.File;
                    break;
                }
                else if (IsCommand(line, out string command))
                {
                    Destination = command;
                    Type = ShortcutType.Command;
                    break;
                }
                else if (IsValidUrl(line, out string url))
                {
                    Destination = url;
                    Type = ShortcutType.Url;
                    break;
                }

                Type = ShortcutType.Unknown;
            }
        }

        public Bat(string path, string shortcut, string command, string destination, ShortcutType type, string openWithApp = "")
        {
            Path = path;
            Shortcut = shortcut;
            Type = type;
            Destination = destination;
            OpenWithApp = openWithApp;
            Command = command;
        }

        #region Validation Methods

        public static bool IsBat(DataGrid dataGrid, out Bat bat)
        {
            bat = null;
            var result = false;
            if (dataGrid.SelectedCells.Any())
            {
                var selectedItem = dataGrid.SelectedCells[0];
                bat = selectedItem.Item as Bat;
                result = true;
            }
            return result;
        }

        private bool IsCLSIDKey(string line, out string clsidKey)
        {
            return MatchesRegex(patternCLSID, line, out clsidKey);
        }

        private bool IsFolder(string line, out string folder)
        {
            return MatchesRegex(patternFolder, line, out folder);
        }

        private bool IsHostsFile(string line, out string hostsFile)
        {
            return MatchesRegex(patternHostsFile, line, out hostsFile);
        }

        private bool IsFile(string line, out string file)
        {
            return MatchesRegex(patternFile, line, out file);
        }

        private bool IsCommand(string line, out string command)
        {
            return MatchesRegex(patternCommand, line, out command);
        }

        private bool IsValidUrl(string line, out string url)
        {
            return MatchesRegex(patternUrl, line, out url);
        }

        public bool MatchesRegex(string pattern, string line, out string result)
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

        #endregion Validation Methods
    }
}
