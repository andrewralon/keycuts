using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class ConsoleApp
    {
        // TODO Make this an int to return 0 or 1?
        public void Run(string destination, string path)
        {
            Shortcut shortcut = new Shortcut(destination, path);

            CreateShortcutFolder(shortcut.Path);

            CreateShortcutFile(shortcut);
        }

        private void CreateShortcutFolder(string path)
        {
            string directory = Path.GetDirectoryName(path);

            // Create the shortcuts folder if it doesn't exist already
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private bool CreateShortcutFile(Shortcut shortcut)
        {
            //if (Shortcut.Type == ShortcutType.Unknown)
            //{
            //    return false;
            //}

            //string pathOnly = "";
            //string filename = "";
            //string savePath = Path.Combine(_shortcutsFolder, shortcut);

            //// Check if the shortcut file already exists
            //if (File.Exists(savePath))
            //{
            //    DialogResult dr = MessageBox.Show(this, "This shortcut file already exists: " +
            //        Environment.NewLine + Environment.NewLine +
            //        "    " + savePath +
            //        Environment.NewLine + Environment.NewLine +
            //        "Would you like to overwrite it with your new shortcut?",
            //        "Overwrite existing file?", MessageBoxButtons.YesNo);

            //    if (dr != DialogResult.Yes)
            //    {
            //        return false;
            //    }
            //}

            //if (shortcutType != this.ShortcutType.Url) // File or Folder
            //{
            //    pathOnly = Path.GetDirectoryName(_newShortcutPath);
            //    filename = Path.GetFileName(_newShortcutPath);
            //}

            //// Create lines with comments and command based on type (file or folder)
            //string shortcutTypeLower = shortcutType.ToString().ToLower();
            //List<string> lines = new List<string>();
            //lines.Add("@ECHO OFF");
            //lines.Add("REM " + Text);
            //lines.Add("REM <" + shortcutTypeLower + ">" + savePath + "</" + shortcutTypeLower + ">");
            ////lines.Add("CHCP 65001>NUL");

            //if (shortcutType == this.ShortcutType.Url)
            //{
            //    lines.Add("START " + SanitizeBatAndCmdEscapeCharacters(target));
            //}
            //else if (shortcutType == this.ShortcutType.File)
            //{
            //    lines.Add("START \"\" /D \"" + pathOnly + "\" \"" + filename + "\"");
            //}
            //else if (shortcutType == this.ShortcutType.Folder)
            //{
            //    lines.Add("\"%SystemRoot%\\explorer.exe\" \"" + _newShortcutPath + "\"");
            //}

            //lines.Add("EXIT");

            //// Write the file to the given save path
            //File.WriteAllLines(savePath, lines.ToArray()); //, Encoding.UTF8);

            return true;
        }
    }
}
