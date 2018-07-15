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

        public void PopulateDataGrid(DataGrid dataGrid, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            if (Directory.Exists(outputFolder))
            {
                var batFiles = Directory.GetFiles(outputFolder, "*.bat");
                if (batFiles.Any())
                {
                    bats = ParseBats(batFiles.ToList(), dataGrid);
                }
                else
                {
                    bats = new List<Bat>();
                }

                var columns = new List<BatColumn>();
                bats.ForEach(s => columns.Add(new BatColumn(s)));

                dataGrid.ItemsSource = columns;
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
                Process.Start(location);
            }
        }

        //private void Copy(DataGrid dataGrid)
        //{
        //    // Not needed -- works already
        //}

        public void Delete(DataGrid dataGrid)
        {
            if (Bat.IsBat(dataGrid, out Bat bat))
            {
                File.Delete(bat.Path);
                bats.Remove(bat);
                dataGrid.Items.Refresh();
            }
        }

        public void SetOutputFolder(string outputFolder)
        {
            RegistryStuff.SetOutputFolder(outputFolder);
        }
    }
}
