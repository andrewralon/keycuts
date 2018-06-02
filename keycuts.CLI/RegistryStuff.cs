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

        private static readonly string ContextMenuFile = $@"*\shell\{Program.AppName}";

        private static readonly string ContextMenuFileCommand = $@"*\shell\{Program.AppName}\command";

        private static readonly string ContextMenuFolder = $@"Folder\shell\{Program.AppName}";

        private static readonly string ContextMenuFolderCommand = $@"Folder\shell\{Program.AppName}\command";

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

        public static void CreateRightClickContextMenu()
        {
            var menu = "Create a keycut to here!";
            var appName = Process.GetCurrentProcess().MainModule.FileName;
            var command = $"\"{appName}\" \"%1\"";
            var context = Registry.ClassesRoot;

            // File
            var menuKey = CreateSubKey(context, ContextMenuFile, true);
            menuKey?.SetValue("", menu);

            var commandKey = CreateSubKey(context, ContextMenuFileCommand, true);
            commandKey?.SetValue("", command);

            // Folder
            menuKey = CreateSubKey(context, ContextMenuFolder, true);
            menuKey?.SetValue("", menu);

            commandKey = CreateSubKey(context, ContextMenuFolderCommand, true);
            commandKey?.SetValue("", command);
        }
    }
}
