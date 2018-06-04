using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.CLI
{
    public class RegistryStuff
    {
        public static readonly RegistryKey CurrentUserContext = Registry.CurrentUser;

        public static readonly string OutputFolderStartPath = @"Software\TeamRalon\";

        public static readonly string OutputFolderKeyName = "OutputFolder";

        public static string ShortcutsFolderPath { get { return $"{OutputFolderStartPath}{Program.AppName}"; } }

        public static RegistryKey CreateSubKey(RegistryKey context, string name, bool writable = false)
        {
            var key = context.OpenSubKey(name, writable);
            if (key == null)
            {
                Console.WriteLine($"CreateSubKey({context.Name}, {name}, {writable})");
                key = context.CreateSubKey(name, writable);
            }
            return key;
        }

        public static void SetOutputFolder(string path)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, ShortcutsFolderPath, true);

            SetOutputFolder(path, appNameKey);
        }

        public static void SetOutputFolder(string path, RegistryKey appNameKey)
        {
            Console.WriteLine($"SetOutputFolder({path}, {appNameKey.Name})");

            appNameKey?.SetValue(OutputFolderKeyName, path, RegistryValueKind.String);
        }

        public static string GetOutputFolder(string path)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, ShortcutsFolderPath);
            var outputFolder = appNameKey?.GetValue(OutputFolderKeyName);

            if (outputFolder == null)
            {
                SetOutputFolder(path, appNameKey);
            }

            path = appNameKey?.GetValue(OutputFolderKeyName, path).ToString();

            return path;
        }

        public static void CreateRightClickContextMenus()
        {
            var appNameGUI = Process.GetCurrentProcess().MainModule.FileName;
            var context = Registry.ClassesRoot;

            var menu = "Create a keycut to here!";
            var command = $"\"{appNameGUI}\" \"%1\"";
            var commandBackground = $"\"{appNameGUI}\" \"%V\"";

            var keyDirectoryShell = $@"Directory\shell\{Program.AppName}";
            var keyDirectoryBackgroundShell = $@"Directory\Background\shell\{Program.AppName}";
            var keyStarShell = $@"*\shell\{Program.AppName}";
            var keyFolderShell = $@"Folder\shell\{Program.AppName}";

            // Directory\shell -- get path of selected folder with %1
            CreateRightClickContextMenu(context, keyDirectoryShell, menu, command);

            // Directory\Background\shell -- get current directory with %V
            CreateRightClickContextMenu(context, keyDirectoryBackgroundShell, menu, commandBackground);

            // File
            CreateRightClickContextMenu(context, keyStarShell, menu, command);

            // Folder
            CreateRightClickContextMenu(context, keyFolderShell, menu, command);
        }

        public static void CreateRightClickContextMenu(RegistryKey context, string keyPath, string menu, string command)
        {
            var menuKey = CreateSubKey(context, keyPath, true);
            menuKey?.SetValue("", menu);

            var commandKey = CreateSubKey(context, $@"{keyPath}\command", true);
            commandKey?.SetValue("", command);
        }
    }
}
