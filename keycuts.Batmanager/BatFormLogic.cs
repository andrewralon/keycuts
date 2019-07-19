using keycuts.Common;
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
        private List<ShortcutFile> shortcuts = new List<ShortcutFile>();

        private string _outputFolder = "";

        public void PopulateDataGrid(DataGrid dataGrid, string outputFolder)
        {
            _outputFolder = outputFolder;

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            if (Directory.Exists(outputFolder))
            {
                var shortcutFiles = Directory.GetFiles(outputFolder, $"*{ShortcutFile.Extension}");
                shortcuts = ParseShortcutFiles(shortcutFiles.ToList(), dataGrid);

                var columns = new List<ShortcutFile>();
                shortcuts.ForEach(s => columns.Add(s));

                dataGrid.ItemsSource = columns;

                if (dataGrid.Columns.Count > 0)
                {
                    dataGrid.Columns[0].Visibility = Visibility.Hidden;
                    dataGrid.Columns[dataGrid.Columns.Count - 1].Visibility = Visibility.Hidden;
                }
            }
            else
            {
                MessageBox.Show($"Directory \"{outputFolder}\" does not exist!");
            }
        }

        public List<ShortcutFile> ParseShortcutFiles(List<string> shortcutFiles, DataGrid dataGrid)
        {
            var shortcuts = new List<ShortcutFile>();
            var skippedFiles = new List<string>();

            foreach (var shortcutFile in shortcutFiles)
            {
                var shortcut = new ShortcutFile(shortcutFile);

                if (!string.IsNullOrEmpty(shortcut.Shortcut) &&
                    !string.IsNullOrEmpty(shortcut.Command) &&
                    shortcut.Type != ShortcutType.Unknown)
                {
                    shortcuts.Add(shortcut);
                }
                else
                {
                    skippedFiles.Add(shortcutFile);
                }
            }

            if (skippedFiles.Any())
            {
                Console.WriteLine("*** Skipped files: ***");
                skippedFiles.ForEach(t => Console.WriteLine(t));
            }

            return shortcuts;
        }

        public static bool IsShortcutFile(DataGrid dataGrid, out ShortcutFile shortcutFile)
        {
            shortcutFile = null;
            var result = false;
            if (dataGrid.SelectedCells.Any())
            {
                var selectedItem = dataGrid.SelectedCells[0];
                shortcutFile = selectedItem.Item as ShortcutFile;
                if (shortcutFile != null)
                {
                    result = true;
                }
            }
            return result;
        }

        public void Edit(DataGrid dataGrid)
        {
            if (IsShortcutFile(dataGrid, out ShortcutFile shortcutFile))
            {
                Process.Start("notepad.exe", shortcutFile.Path);
            }
        }

        public void Run(DataGrid dataGrid)
        {
            if (IsShortcutFile(dataGrid, out ShortcutFile shortcutFile))
            {
                Process.Start(shortcutFile.Path);
            }
        }

        public void OpenDestinationLocation(DataGrid dataGrid)
        {
            if (IsShortcutFile(dataGrid, out ShortcutFile shortcutFile))
            {
                var location = Path.GetDirectoryName(shortcutFile?.Destination);
                if (location != "")
                {
                    Process.Start(location);
                }
            }
        }

        public void Copy(DataGrid dataGrid)
        {
            // Not used -- Copy() below is called via DataGrid_CopyingRowClipboardContent event
        }

        public void Copy(DataGrid dataGrid, DataGridRowClipboardEventArgs e)
        {
            var currentCell = e.ClipboardRowContent[dataGrid.CurrentCell.Column.DisplayIndex - 1];
            e.ClipboardRowContent.Clear();
            e.ClipboardRowContent.Add(currentCell);
        }

        public void Delete(DataGrid dataGrid)
        {
            if (IsShortcutFile(dataGrid, out ShortcutFile shortcutFile))
            {
                File.Delete(shortcutFile.Path);
                shortcuts.Remove(shortcutFile);
                dataGrid.Items.Refresh();
            }

            PopulateDataGrid(dataGrid, _outputFolder);
        }

        public void SetOutputFolder(string outputFolder)
        {
            RegistryStuff.SetOutputFolder(outputFolder);
        }
    }
}
