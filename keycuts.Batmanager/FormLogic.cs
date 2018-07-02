using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace keycuts.Batmanager
{
    public class FormLogic
    {
        public static List<Bat> Bats = new List<Bat>()
        {
            new Bat("andrew", @"D:\Dropbox\Andrew", ShortcutType.Folder),
            new Bat("aud", @"C:\Program Files (x86)\Audacity\audacity.exe", ShortcutType.File)
        };

        public static void PopulateDataGrid(DataGrid dataGrid)
        {
            var items = new List<Bat>();
            
            foreach (var bat in Bats)
            {
                items.Add(bat);
            }

            dataGrid.ItemsSource = items;
        }
    }
}
