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
        public static List<Bat> Bats = new List<Bat>()
        {
            new Bat("andrew", @"D:\Dropbox\Andrew", ShortcutType.Folder),
            new Bat("aud", @"C:\Program Files (x86)\Audacity\audacity.exe", ShortcutType.File)
        };

        public static void PopulateDataGrid(DataGrid dataGrid)
        {
            var outputFolder = RegistryStuff.GetOutputFolder(@"C:\Shortcuts");
            var shortcuts = Directory.GetFiles(outputFolder, "*.bat").ToList();

            var bats = shortcuts.Select(t => BatParser.Parse(t)).ToList();
            dataGrid.ItemsSource = bats;
        }

        public static List<Bat> ParseShortcutFiles(List<string> shortcuts)
        {
            var bats = new List<Bat>();



            return bats;
        }
    }
}
