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
            //if (IsWindowsShortcut(destination))
            //{
            //    destination = FollowWindowsShortcut(destination);
            //}

            var shortcut = new Shortcut(destination, path);

            if (shortcut.Type != ShortcutType.Unknown)
            {
                CreateShortcutFolder(shortcut.Folder);

                CreateShortcutFile(shortcut);
            }
        }

        private void CreateShortcutFolder(string folder)
        {
            // Create the shortcuts folder if it doesn't exist already
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        private bool CreateShortcutFile(Shortcut shortcut)
        {
            bool result = false;

            // Check if the shortcut file already exists
            //if (!File.Exists(shortcut.FullPath))
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
                File.WriteAllLines(shortcut.FullPath, lines.ToArray()); //, Encoding.UTF8);

                result = true;
            }

            return result;
        }

        private string SanitizeBatAndCmdEscapeCharacters(string command)
        {
            // TODO Use Regex instead!

            // TODO Check for ^& and %% in existing string instead of blindly replacing

            // CMD uses & for commands, so replace it with ^&
            command = command.Replace("&", "^&");

            // Bat files use % for commands, so replace it with %%
            command = command.Replace("%", "%%");

            return command;
        }

        public static string FollowWindowsShortcut(string file)
        {
            var result = file;

            // Follow the shortcut if it is one!
            if (IsWindowsShortcut(file))
            {
                result = GetShortcutTargetPath(file);
            }

            // TODO Handle resolving a relative
            //result = Path.GetFullPath(file);

            return result;
        }

        public static bool IsWindowsShortcut(string shortcutFilename)
        {
            // Code found here: http://stackoverflow.com/questions/310595/how-can-i-test-programmatically-if-a-path-file-is-a-shortcut
            string path = Path.GetDirectoryName(shortcutFilename);
            string file = Path.GetFileName(shortcutFilename);

            //Shell32.Shell shell = new Shell32.Shell();
            //Shell32.Folder folder = shell.NameSpace(path);
            //Shell32.FolderItem folderItem = folder.ParseName(file);

            //if (folderItem != null)
            //{
            //    return folderItem.IsLink;
            //}

            return false; // Not found
        }

        public static string GetShortcutTargetPath(string shortcutFilename)
        {
            // Code found here: http://www.emoticode.net/c-sharp/get-full-path-of-file-a-shortcut-link-references.html
            string path = Path.GetDirectoryName(shortcutFilename);
            string file = Path.GetFileName(shortcutFilename);

            //Shell32.Shell shell = new Shell32.Shell();
            //Shell32.Folder folder = shell.NameSpace(path);
            //Shell32.FolderItem folderItem = folder.ParseName(file);

            //if (folderItem != null)
            //{
            //    Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;
            //    return link.Path;
            //}

            return ""; // Not found
        }
    }
}
