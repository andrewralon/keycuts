using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public class Runner
    {
        public static readonly string AppName = "keycuts";

        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static readonly string DefaultOutputFolder = @"C:\Shortcuts";

        public static readonly bool DefaultForceOverwrite = false;

        public static readonly bool DefaultRightClickContextMenu = true;

        public static readonly string RightClickContextMenuText = "keycut this!";

        public Runner()
        {
        }

        public ExitCode Run(KeycutArgs args)
        {
            return Run(args.Destination, args.Shortcut, args.OutputFolder, args.OpenWithApp, args.Force);
        }

        public ExitCode Run(string destination, string shortcutPath, string outputFolder, string openWithAppPath = null, bool force = false)
        {
            var result = ExitCode.NotStarted;

            var currentOutputFolder = RegistryStuff.GetOutputFolder(DefaultOutputFolder);

            outputFolder = SetOutputFolder(outputFolder, currentOutputFolder);

            var systemPathResult = PathSetup.AddToOrReplaceInSystemPath(currentOutputFolder, outputFolder);

            if (!systemPathResult)
            {
                result = ExitCode.CannotUpdatePath;
            }
            else
            {
                var shortcut = new Shortcut(destination, shortcutPath, outputFolder, openWithAppPath);

                if (shortcut.Type != ShortcutType.Unknown)
                {
                    var createOutputFolderResult = CreateOutputFolder(shortcut.Folder);

                    if (!createOutputFolderResult)
                    {
                        result = ExitCode.CannotCreateOutputFolder;
                    }
                    else
                    {
                        var createShortcutResult = CreateShortcutFile(shortcut, force);

                        result = createShortcutResult ?
                            ExitCode.Success :
                            ExitCode.FileAlreadyExists;
                    }
                }
            }

            return result;
        }

        #region Get Methods

        public string GetOutputFolder()
        {
            var outputFolder = RegistryStuff.GetOutputFolder(DefaultOutputFolder);

            return outputFolder;
        }

        public bool GetForceOverwrite()
        {
            var forceOverwrite = RegistryStuff.GetForceOverwrite(DefaultForceOverwrite);

            return forceOverwrite;
        }

        public bool GetRightClickContextMenu()
        {
            var rightClickContextMenu = RegistryStuff.GetRightClickContextMenu(DefaultRightClickContextMenu);

            return rightClickContextMenu;
        }

        #endregion Get Methods

        #region Set Methods

        public string SetOutputFolder(string outputFolder, string currentOutputFolder = null)
        {
            if (currentOutputFolder == null)
            {
                currentOutputFolder = RegistryStuff.GetOutputFolder(DefaultOutputFolder);
            }

            if (outputFolder == null)
            {
                // If not given, use the existing default folder
                outputFolder = currentOutputFolder;
            }

            if (outputFolder != currentOutputFolder)
            {
                // Update the registry key if the output folder has changed
                RegistryStuff.SetOutputFolder(outputFolder);
            }

            return outputFolder;
        }

        public void SetForceOverwrite(bool forceOverwrite)
        {
            var currentForceOverwrite = RegistryStuff.GetForceOverwrite(DefaultForceOverwrite);

            if (currentForceOverwrite != forceOverwrite)
            {
                RegistryStuff.SetForceOverwrite(forceOverwrite);
            }
        }

        public void SetRightClickContextMenu(bool rightClickContextMenu)
        {
            var currentRightClickContextMenu = RegistryStuff.GetRightClickContextMenu(DefaultForceOverwrite);

            if (currentRightClickContextMenu != rightClickContextMenu)
            {
                RegistryStuff.SetRightClickContextMenu(rightClickContextMenu);
            }
        }

        #endregion Get Methods

        private bool CreateOutputFolder(string folder)
        {
            var result = true;

            // Create the shortcuts folder if it doesn't exist already
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return result;
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
                    $"REM {AppName} {Version}",
                    $"REM <shortcut>{shortcut.FullPath}</shortcut>",
                    $"REM <type>{shortcutTypeLower}</type>",
                    $"REM <destination>{shortcut.Destination}</destination>",
                };

                // START: "" = Title (empty) of console window
                //  /B = don't create a new window
                //  "{0}" = command/program
                //  "{1}" = parameters
                var start = "START \"\" /B \"{0}\"";
                var command = "";

                if (shortcut.OpenWithApp)
                {
                    start = $"{start} \"{{1}}\"";
                    command = string.Format(start, shortcut.OpenWithAppPath, shortcut.Destination);
                }
                else
                {
                    if (shortcut.Type == ShortcutType.Url)
                    {
                        var sanitized = Shortcut.SanitizeBatEscapeCharacters(shortcut.Destination);

                        command = $"START {sanitized}";
                    }
                    else if (shortcut.Type == ShortcutType.File)
                    {
                        command = string.Format(start, shortcut.Destination);
                    }
                    else if (shortcut.Type == ShortcutType.HostsFile)
                    {
                        var notepadPath = @"%windir%\system32\notepad.exe";

                        start = $"{start} \"{{1}}\"";
                        command = string.Format(start, notepadPath, shortcut.Destination);
                    }
                    else if (shortcut.Type == ShortcutType.Folder || shortcut.Type == ShortcutType.CLSIDKey)
                    {
                        command = $"\"%SystemRoot%\\explorer.exe\" \"{shortcut.Destination}\"";
                    }
                }

                lines.Add(command);
                lines.Add("EXIT");

                // Write the file to the given save path
                File.WriteAllLines(shortcut.FullPath, lines.ToArray());

                Console.WriteLine($"Shortcut created: \"{shortcut.FullPath}\"");

                result = true;
            }

            return result;
        }

        #region Static Methods

        public static void OpenBatmanager()
        {
            var projectFolder = Directory.GetCurrentDirectory();
            var batmanager = Path.Combine(projectFolder, "keycuts.Batmanager.exe");
#if DEBUG
            projectFolder = Directory.GetParent(projectFolder).Parent.Parent.FullName;
            batmanager = Path.Combine(projectFolder, "keycuts.Batmanager\\bin\\Debug\\keycuts.Batmanager.exe");
#endif
            Process.Start(batmanager);
        }

        #endregion Static Methods
    }
}
