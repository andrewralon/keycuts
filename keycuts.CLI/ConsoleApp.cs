﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class ConsoleApp
    {
        public readonly string AppName;

        public readonly string Version;

        public static readonly string DefaultFolder = @"C:\Shortcuts";

        public static readonly string RegistryKeyStartPath = @"Software\TeamRalon\";

        public ConsoleApp(string appName, string version)
        {
            AppName = appName;
            Version = version;
        }

        public int Run(Options options)
        {
            return Run(options.Destination, options.Shortcut, options.DefaultFolder, options.OpenWithAppPath, options.Force);
        }

        public int Run(string destination, string shortcutPath, string defaultFolder, string openWithAppPath = null, bool force = false)
        {
            var result = false;

            string registryKeyPath = string.Format("{0}{1}", RegistryKeyStartPath, AppName);
            var oldDefaultFolder = RegistryKey.GetDefaultShortcutsFolder(DefaultFolder, registryKeyPath);

            if (defaultFolder == null)
            {
                // If not given, use the existing default folder
                defaultFolder = oldDefaultFolder;
            }

            if (defaultFolder != oldDefaultFolder)
            {
                // Update the registry key if the default folder has changed
                RegistryKey.SetDefaultShortcutsFolder(defaultFolder, registryKeyPath);
            }

            PathSetup.AddToOrReplaceInSystemPath(oldDefaultFolder, defaultFolder);

            var shortcut = new Shortcut(destination, shortcutPath, defaultFolder, openWithAppPath);

            if (shortcut.Type != ShortcutType.Unknown)
            {
                CreateShortcutFolder(shortcut.Folder);

                result = CreateShortcutFile(shortcut, force);
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

            // If the shortcut file already exists, let overwrite decide
            if (!File.Exists(shortcut.FullPath) || overwrite)
            {
                // Create lines with comments and command based on type (file or folder)
                var shortcutTypeLower = shortcut.Type.ToString().ToLower();

                var lines = new List<string>
                {
                    "@ECHO OFF",
                    string.Format("REM {0} {1}", AppName, Version),
                    string.Format("REM <shortcut>{0}</shortcut>", shortcut.FullPath),
                    string.Format("REM <type>{0}</type>", shortcutTypeLower),
                    string.Format("REM <destination>{0}</destination>", shortcut.Destination),
                };

                // START: "" = Title (empty) of console window
                //  /B = don't create a new window
                //  "{0}" = command/program
                //  "{1}" = parameters
                var start = "START \"\" /B \"{0}\"";
                var command = "";

                if (shortcut.OpenWithApp)
                {
                    command = string.Format(start + " \"{1}\"", shortcut.OpenWithAppPath, shortcut.Destination);
                }
                else
                {
                    if (shortcut.Type == ShortcutType.Url)
                    {
                        command = string.Format(start, Shortcut.SanitizeBatEscapeCharacters(shortcut.Destination));
                    }
                    else if (shortcut.Type == ShortcutType.File)
                    {
                        command = string.Format(start, shortcut.Destination);
                    }
                    else if (shortcut.Type == ShortcutType.HostsFile)
                    {
                        var notepadPath = @"%windir%\system32\notepad.exe";

                        command = string.Format(start + " \"{1}\"", notepadPath, shortcut.Destination);
                    }
                    else if (shortcut.Type == ShortcutType.Folder)
                    {
                        command = string.Format("\"%SystemRoot%\\explorer.exe\" \"{0}\"", shortcut.Destination);
                    }
                }

                lines.Add(command);
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
    }
}