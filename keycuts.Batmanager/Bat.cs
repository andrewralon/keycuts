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
        private readonly string patternHostsFile = "START \"\" \\/[BD] \"(.+)\" \"(.+hosts)\"$";
        private readonly string patternFile = "START \"\" \\/[BD] \"(.+)\"( \"(.+)\")?$";
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

                if (IsCLSIDKey(line, ShortcutType.CLSIDKey, out string clsidKey))
                {
                    Destination = clsidKey;
                    Type = ShortcutType.CLSIDKey;
                    break;
                }
                else if (IsFolder(line, ShortcutType.Folder, out string folder))
                {
                    Destination = folder;
                    Type = ShortcutType.Folder;
                    break;
                }
                else if (IsHostsFile(line, ShortcutType.HostsFile, out string hostsFile, out string openWithApp))
                {
                    Destination = hostsFile;
                    Type = ShortcutType.HostsFile;
                    OpenWithApp = openWithApp;
                    break;
                }
                else if (IsFile(line, ShortcutType.File, out string file))
                {
                    Destination = file;
                    Type = ShortcutType.File;
                    break;
                }
                else if (IsCommand(line, ShortcutType.Command, out string command))
                {
                    Destination = command;
                    Type = ShortcutType.Command;
                    break;
                }
                else if (IsValidUrl(line, ShortcutType.Url, out string url))
                {
                    Destination = url;
                    Type = ShortcutType.Url;
                    break;
                }

                Type = ShortcutType.Unknown;
            }

            Destination = $"\"{Destination}\"";

            if (!string.IsNullOrEmpty(OpenWithApp))
            {
                OpenWithApp = $"\"{OpenWithApp}\"";
            }
        }

        public static bool IsBat(DataGrid dataGrid, out Bat bat)
        {
            bat = null;
            var result = false;
            if (dataGrid.SelectedCells.Any())
            {
                var selectedItem = dataGrid.SelectedCells[0];
                bat = selectedItem.Item as Bat;
                if (bat != null)
                {
                    result = true;
                }
            }
            return result;
        }

        #region Validation Methods

        private bool IsCLSIDKey(string line, ShortcutType shortcutType, out string clsidKey)
        {
            return MatchesRegex(patternCLSID, line, shortcutType, out clsidKey);
        }

        private bool IsFolder(string line, ShortcutType shortcutType, out string folder)
        {
            return MatchesRegex(patternFolder, line, shortcutType, out folder);
        }

        private bool IsHostsFile(string line, ShortcutType shortcutType, out string hostsFile, out string openWithApp)
        {
            return MatchesRegex(patternHostsFile, line, shortcutType, out hostsFile, out openWithApp);
        }

        private bool IsFile(string line, ShortcutType shortcutType, out string file)
        {
            return MatchesRegex(patternFile, line, shortcutType, out file);
        }

        private bool IsCommand(string line, ShortcutType shortcutType, out string command)
        {
            return MatchesRegex(patternCommand, line, shortcutType, out command);
        }

        private bool IsValidUrl(string line, ShortcutType shortcutType, out string url)
        {
            return MatchesRegex(patternUrl, line, shortcutType, out url);
        }

        public bool MatchesRegex(string pattern, string line, ShortcutType shortcutType, out string result)
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

        public bool MatchesRegex(string pattern, string line, ShortcutType shortcutType, out string result, out string openWithApp)
        {
            result = "";
            openWithApp = "";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(line);

            if (match.Success)
            {
                if (shortcutType == ShortcutType.HostsFile)
                {
                    if (match.Groups[1].Value != "" && match.Groups[2].Value != "")
                    {
                        openWithApp = match.Groups[1].Value;
                        result = match.Groups[2].Value;
                    }
                }
                else
                {
                    result = match.Groups[1].Value;
                }
            }

            return match.Success;
        }

        #endregion Validation Methods
    }
}
