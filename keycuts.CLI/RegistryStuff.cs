using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

        //private static readonly string ContextMenuName = "Folder\\shell\\";

        //private static readonly string ContextMenuCommand = "Folder\\shell\\";

        public static string AppName = Assembly.GetExecutingAssembly().GetName().Name;

        public static string ShortcutsFolderPath { get { return $"{OutputFolderStartPath}{AppName}"; } }

        public static RegistryKey CreateSubKey(RegistryKey context, string name, bool writable = false)
        {
            var key = context.OpenSubKey(name, writable);
            if (key == null)
            {
                Console.WriteLine($"CreateSubKey({CurrentUserContext.Name}, {name}, {writable})");
                key = CurrentUserContext.CreateSubKey(name, writable);
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
            // CHECK IF IT EXISTS ALREADY

            var context = Registry.ClassesRoot;
            var exePath = @"C:\dev\keycuts\keycuts.CLI\bin\Debug\keycuts.exe";

            var menu = "Create a keycut to here!";
            var command = "";

            var menuKey = CreateSubKey(context, menu, true);
            menuKey?.SetValue("", menu);

            var commandKey = CreateSubKey(context, command, true);
            commandKey.SetValue("", exePath);
        }
    }
}
