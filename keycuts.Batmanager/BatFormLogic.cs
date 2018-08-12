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
        private List<Bat> bats = new List<Bat>();

        private string _outputFolder;

        public void PopulateDataGrid(DataGrid dataGrid, string outputFolder)
        {
            _outputFolder = outputFolder;

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            if (Directory.Exists(outputFolder))
            {
                var batFiles = Directory.GetFiles(outputFolder, "*.bat");
                bats = ParseBats(batFiles.ToList(), dataGrid);

                var columns = new List<Bat>();
                bats.ForEach(s => columns.Add(s));

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

        public List<Bat> ParseBats(List<string> batFiles, DataGrid dataGrid)
        {
            var bats = new List<Bat>();
            var skippedFiles = new List<string>();

            foreach (var batFile in batFiles)
            {
                var bat = new Bat(batFile, dataGrid);

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

        public void Edit(DataGrid dataGrid)
        {
            if (Bat.IsBat(dataGrid, out Bat bat))
            {
                Process.Start("notepad.exe", bat.Path);
            }
        }

        public void Run(DataGrid dataGrid)
        {
            if (Bat.IsBat(dataGrid, out Bat bat))
            {
                Process.Start(bat.Path);
            }
        }

        public void OpenDestinationLocation(DataGrid dataGrid)
        {
            if (Bat.IsBat(dataGrid, out Bat bat))
            {
                var location = Path.GetDirectoryName(bat?.Destination);
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
            if (Bat.IsBat(dataGrid, out Bat bat))
            {
                File.Delete(bat.Path);
                bats.Remove(bat);
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
