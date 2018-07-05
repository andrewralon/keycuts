using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace keycuts.Batmanager
{
    public class BatFormLogic
    {
        public static void PopulateDataGrid(DataGrid dataGrid)
        {
            var bats = new List<Bat>();

            var outputFolder = RegistryStuff.GetOutputFolder(@"C:\Shortcuts");
            var shortcuts = Directory.GetFiles(outputFolder, "*.bat").ToList();

            foreach (var shortcut in shortcuts)
            {
                var lines = File.ReadAllLines(shortcut)
                    .Where(x => !string.IsNullOrEmpty(x) &&
                        (x.StartsWith("START", StringComparison.CurrentCultureIgnoreCase) || x.Contains("\\explorer.exe")))
                    .ToList();

                var bat = BatParser.Parse(shortcut, lines);

                if (bat != null)
                {
                    bats.Add(bat);
                }
            }

            dataGrid.ItemsSource = bats;
        }
    }
}
