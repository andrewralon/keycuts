using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;

namespace keycuts.GUI
{
    public class DragDrop
    {
        public static void DragAndEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) // Normal files or folders
            {
                e.Effects = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(DataFormats.Text)) // URL drag and drop from browser "lock" icon
            {
                e.Effects = DragDropEffects.Copy;
            }

            return;
        }

        public static string[] GetDroppedFiles(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (files == null) // Check for URL drag and drop from browser "lock" icon
            {
                files = new string[]
                {
                    (string)e.Data.GetData(DataFormats.Text, false)
                };
            }

            return files;
        }
    }
}
