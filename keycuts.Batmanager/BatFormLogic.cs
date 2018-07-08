using keycuts.Common;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace keycuts.Batmanager
{
    public class BatFormLogic
    {
        #region Fields

        private readonly string explorer = "\\explorer.exe\"";
        private readonly string start = "START ";

        private readonly string patternCLSID = "\".+explorer.exe\" \"[shell:]?::({.+})\"";
        private readonly string patternFolder = "\".+explorer.exe\" \"(.+)\"";
        private readonly string patternHostsFile = "START \"\" \\/[BD] \".+\" \"(.+)hosts\"$";
        private readonly string patternFile = "START \"\" \\/[BD] \".+\" \"(.+)\"$";
        private readonly string patternCommand = "START \"\" \\/[BD] \".+\" (.+)";
        private readonly string patternUrl = "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

        #endregion Fields

        #region Public Methods

        public void PopulateDataGrid(DataGrid dataGrid)
        {
            var outputFolder = RegistryStuff.GetOutputFolder(@"C:\Shortcuts");
            var batFiles = Directory.GetFiles(outputFolder, "*.bat").ToList();
            var bats = ParseBats(batFiles);
            dataGrid.ItemsSource = bats;
        }

        public List<Bat> ParseBats(List<string> batFiles)
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

        #region Validation Methods

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

        public bool IsBat(DataGrid dataGrid, out Bat bat)
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

        public void Edit(DataGrid dataGrid)
        {
            if (IsBat(dataGrid, out Bat bat))
            {
                Process.Start(bat.Path);
            }
        }

        public void Run(DataGrid dataGrid)
        {
            if (IsBat(dataGrid, out Bat bat))
            {
                Process.Start(bat.Path);
            }
        }

        //private void Copy(DataGrid dataGrid)
        //{
        //    // Not needed -- works already
        //}

        public void OpenDestinationLocation(DataGrid dataGrid)
        {
            if (IsBat(dataGrid, out Bat bat))
            {
                var location = Path.GetDirectoryName(bat?.Destination);
                Process.Start(location);
            }
        }

        public void Delete(DataGrid dataGrid)
        {
            if (IsBat(dataGrid, out Bat bat))
            {
                //MessageBox.Show("Are you sure you want to move this to the Recycle Bin?", );
                FileSystem.DeleteFile(bat.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                dataGrid.Items.Remove(bat);
            }
        }

        #endregion Public Methods
    }
}
