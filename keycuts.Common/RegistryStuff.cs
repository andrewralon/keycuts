using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public class RegistryStuff
    {
        public static readonly RegistryKey CurrentUserContext = Registry.CurrentUser;

        public static readonly string StartPath = $@"Software\TeamRalon\{Runner.AppName}";

        public static readonly string OutputFolderKeyName = "OutputFolder";

        public static readonly string ForceOverwriteKeyName = "ForceOverwrite";

        public static bool ForceOverwriteDefault { get { return false; } }

        public static RegistryKey CreateSubKey(RegistryKey context, string name, bool writable = false)
        {
            var combined = $"{StartPath}{name}";

            var key = context.OpenSubKey(combined, writable);
            if (key == null)
            {
                Console.WriteLine($"CreateSubKey({context.Name}, {name}, {writable})");

                key = context.CreateSubKey(name, writable);
            }
            return key;
        }

        #region Get Methods

        public static string GetOutputFolder(string path)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath);
            var outputFolderKey = appNameKey?.GetValue(OutputFolderKeyName);

            if (outputFolderKey == null)
            {
                SetOutputFolder(path);//, appNameKey);
            }

            path = appNameKey?.GetValue(OutputFolderKeyName, path).ToString();

            return path;
        }

        public static bool GetForceOverwrite(bool forceOverwrite)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath);
            var forceOverwriteKey = appNameKey?.GetValue(ForceOverwriteKeyName);

            if (forceOverwriteKey == null)
            {
                SetForceOverwrite(forceOverwrite);//, appNameKey);
            }

            var x = (int)appNameKey?.GetValue(ForceOverwriteKeyName, forceOverwrite);
            forceOverwrite = x != 0;

            return forceOverwrite;
        }

        #endregion Get Methods

        #region Set Methods

        public static void SetRegistryValue(RegistryKey appNameKey, string keyName, object keyValue, 
            RegistryValueKind registryValueKind = RegistryValueKind.String)
        {
            Console.WriteLine($"SetRegistryValue(\"{appNameKey}\", \"{keyName}\", \"{keyValue}\", \"{registryValueKind.ToString()}\")");

            appNameKey?.SetValue(keyName, keyValue, registryValueKind);
        }

        public static void SetOutputFolder(string path)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath, true);

            SetRegistryValue(appNameKey, OutputFolderKeyName, path, RegistryValueKind.String);

            //SetOutputFolder(path, appNameKey);
        }

        //public static void SetOutputFolder(string path, RegistryKey appNameKey)
        //{
        //    Console.WriteLine($"SetOutputFolder(\"{path}\", \"{appNameKey.Name}\")");

        //    appNameKey?.SetValue(OutputFolderKeyName, path, RegistryValueKind.String);
        //}

        public static void SetForceOverwrite(bool forceOverwrite)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath, true);

            SetRegistryValue(appNameKey, ForceOverwriteKeyName, forceOverwrite, RegistryValueKind.DWord);

            //SetForceOverwrite(forceOverwrite, appNameKey);
        }

        //public static void SetForceOverwrite(bool forceOverwrite, RegistryKey appNameKey)
        //{
        //    Console.WriteLine($"SetForceOverwrite({forceOverwrite}, \"{appNameKey.Name}\"");

        //    var binary = Convert.ToInt16(forceOverwrite);

        //    appNameKey?.SetValue(ForceOverwriteKeyName, forceOverwrite, RegistryValueKind.DWord);
        //}

        #endregion Set Methods

        public static void CreateRightClickContextMenus()
        {
            var appNameGUI = Process.GetCurrentProcess().MainModule.FileName;
            var context = Registry.ClassesRoot;

            var menu = "Create a keycut to here!";
            var command = $"\"{appNameGUI}\" \"%1\""; // Path of selected folder with %1
            var commandBackground = $"\"{appNameGUI}\" \"%V\""; // Path of current directory with %V

            var keyDirectoryShell = $@"Directory\shell\{Runner.AppName}";
            var keyDirectoryBackgroundShell = $@"Directory\Background\shell\{Runner.AppName}";
            var keyStarShell = $@"*\shell\{Runner.AppName}";
            var keyFolderShell = $@"Folder\shell\{Runner.AppName}";

            // Directory\shell
            CreateRightClickContextMenu(context, keyDirectoryShell, menu, command);

            // Directory\Background\shell
            CreateRightClickContextMenu(context, keyDirectoryBackgroundShell, menu, commandBackground);

            // *\shell -- file
            CreateRightClickContextMenu(context, keyStarShell, menu, command);

            // Folder\shell
            CreateRightClickContextMenu(context, keyFolderShell, menu, command);
        }

        public static void CreateRightClickContextMenu(RegistryKey context, string keyPath, string menu, string command)
        {
            Console.WriteLine($"CreateRightClickContextMenu(\"{context.Name}\", \"{keyPath}\", \"{menu}\", {command})");

            var menuKey = CreateSubKey(context, keyPath, true);
            menuKey?.SetValue("", menu);

            var commandKey = CreateSubKey(context, $@"{keyPath}\command", true);
            commandKey?.SetValue("", command);
        }
    }
}
