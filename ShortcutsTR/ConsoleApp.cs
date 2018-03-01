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
        public int Run(string destination, string path)
        {
            var result = false;

            var shortcut = new Shortcut(destination, path);

            if (shortcut.Type != ShortcutType.Unknown)
            {
                CreateShortcutFolder(shortcut.Folder);

                result = CreateShortcutFile(shortcut);
            }

            return result ? 0 : 1;
        }

        private void CreateShortcutFolder(string folder)
        {
            // Create the shortcuts folder if it doesn't exist already
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        private bool CreateShortcutFile(Shortcut shortcut, bool overwrite = false)
        {
            var result = false;

            // Check if the shortcut file already exists
            if (!File.Exists(shortcut.FullPath) || overwrite)
            {
                // Create lines with comments and command based on type (file or folder)
                var shortcutTypeLower = shortcut.Type.ToString().ToLower();
                var lines = new List<string>
                {
                    "@ECHO OFF",
                    // TODO Put app name and version here
                    //string.Format("REM {0}", shortcut.Text), 
                    string.Format("REM <{0}>{1}</{2}>", shortcutTypeLower, shortcut.FullPath, shortcutTypeLower)
                };

                if (shortcut.Type == ShortcutType.Url)
                {
                    lines.Add(string.Format("START {0}", SanitizeBatAndCmdEscapeCharacters(shortcut.Destination)));
                }
                else if (shortcut.Type == ShortcutType.File)
                {
                    lines.Add(string.Format("START \"\" /D \"{0}\" \"{1}\"", shortcut.Folder, shortcut.FilenameWithExtension));
                }
                else if (shortcut.Type == ShortcutType.HostsFile)
                {
                    // TODO Fix this -- the command prompt stays open?!
                    
                    // TODO Fix this -- should it create a .lnk file with this or similar?
                    //"C:\Program Files (x86)\Notepad++\notepad++.exe" C:\Windows\System32\drivers\etc\hosts

                    var notepadPath = @"%windir%\system32\notepad.exe";

                    lines.Add(string.Format("\"{0}\" \"{1}\"", notepadPath, shortcut.Destination));
                }
                else if (shortcut.Type == ShortcutType.Folder)
                {
                    lines.Add(string.Format("\"%SystemRoot%\\explorer.exe\" \"{0}\"", shortcut.Destination));
                }

                lines.Add("EXIT");

                // Write the file to the given save path
                File.WriteAllLines(shortcut.FullPath, lines.ToArray());

                result = true;
            }
            else
            {
                Console.WriteLine("The shortcut file already exists: ");
                Console.WriteLine(shortcut.FullPath);
                Console.WriteLine();
                result = false;
            }

            return result;
        }

        private string SanitizeBatAndCmdEscapeCharacters(string command)
        {
            // TODO Use Regex instead! Or create a new Uri???

            // TODO Check for ^& and %% in existing string instead of blindly replacing

            // CMD uses & for commands, so replace it with ^&
            command = command.Replace("&", "^&");

            // Bat files use % for commands, so replace it with %%
            command = command.Replace("%", "%%");

            return command;
        }
    }
}
