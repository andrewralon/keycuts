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

        public static readonly RegistryKey RightClickContextMenuContext = Registry.ClassesRoot;

        public static readonly string StartPath = $@"Software\TeamRalon\{Runner.AppName}";

        public static readonly string OutputFolderKeyName = "OutputFolder";

        public static readonly string ForceOverwriteKeyName = "ForceOverwrite";

        public static readonly string RightClickContextMenuKeyName = "RightClickContextMenu";

        public static RegistryKey CreateSubKey(RegistryKey context, string name, bool writable = false)
        {
            var key = context.OpenSubKey(name, writable);
            if (key == null)
            {
                Console.WriteLine($"CreateSubKey(\"{context.Name}\", \"{name}\", \"{writable}\")");

                key = context.CreateSubKey(name, writable);
            }
            return key;
        }

        public static void DeleteSubKeyTree(RegistryKey context, string keyPath, string treeName)
        {
            var key = context.OpenSubKey(keyPath, true);
            if (key != null)
            {
                Console.WriteLine($"DeleteSubKeyTree(\"{context.Name}\", \"{keyPath}\", \"{treeName}\"");

                key?.DeleteSubKeyTree(treeName, false);
            }
        }

        #region Get Methods

        public static string GetOutputFolder(string path)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath);
            var outputFolderKey = appNameKey?.GetValue(OutputFolderKeyName);

            if (outputFolderKey == null)
            {
                SetOutputFolder(path);
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
                SetForceOverwrite(forceOverwrite);
            }

            var forceOverwriteRaw = (int)appNameKey?.GetValue(ForceOverwriteKeyName, forceOverwrite);
            forceOverwrite = forceOverwriteRaw != 0;

            return forceOverwrite;
        }

        public static bool GetRightClickContextMenu(bool rightClickContextMenu)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath);
            var rightClickContextMenuKey = appNameKey?.GetValue(RightClickContextMenuKeyName);

            if (rightClickContextMenuKey == null)
            {
                SetRightClickContextMenu(rightClickContextMenu);
            }

            var rightClickContextMenuRaw = (int)appNameKey?.GetValue(RightClickContextMenuKeyName, rightClickContextMenu);
            rightClickContextMenu = rightClickContextMenuRaw != 0;

            return rightClickContextMenu;
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
        }

        public static void SetForceOverwrite(bool forceOverwrite)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath, true);

            SetRegistryValue(appNameKey, ForceOverwriteKeyName, forceOverwrite, RegistryValueKind.DWord);
        }

        public static void SetRightClickContextMenu(bool rightClickContextMenu)
        {
            var appNameKey = CreateSubKey(CurrentUserContext, StartPath, true);

            SetRegistryValue(appNameKey, RightClickContextMenuKeyName, rightClickContextMenu, RegistryValueKind.DWord);

            if (rightClickContextMenu)
            {
                CreateRightClickContextMenus();
            }
            else
            {
                RemoveRightClickContextMenus();
            }
        }

        #endregion Set Methods

        public static void CreateRightClickContextMenus()
        {
            var appNameGUI = Process.GetCurrentProcess().MainModule.FileName;

            var menu = "Create a keycut to here!";
            var command = $"\"{appNameGUI}\" \"%1\""; // Path of selected folder with %1
            var commandBackground = $"\"{appNameGUI}\" \"%V\""; // Path of current directory with %V

            var keyDirectoryShell = $@"Directory\shell\{Runner.AppName}";   // Directory/shell
            var keyDirectoryBackgroundShell = $@"Directory\Background\shell\{Runner.AppName}"; // Directory/Background/shell
            var keyStarShell = $@"*\shell\{Runner.AppName}";                // *\shell -- file
            var keyFolderShell = $@"Folder\shell\{Runner.AppName}";         // Folder\shell

            CreateRightClickContextMenu(RightClickContextMenuContext, keyDirectoryShell, menu, command);
            CreateRightClickContextMenu(RightClickContextMenuContext, keyDirectoryBackgroundShell, menu, commandBackground);
            CreateRightClickContextMenu(RightClickContextMenuContext, keyStarShell, menu, command);
            CreateRightClickContextMenu(RightClickContextMenuContext, keyFolderShell, menu, command);
        }

        public static void CreateRightClickContextMenu(RegistryKey context, string keyPath, string menu, string command)
        {
            var menuKey = CreateSubKey(context, keyPath, true);
            menuKey?.SetValue("", menu);

            var commandKey = CreateSubKey(context, $@"{keyPath}\command", true);
            commandKey?.SetValue("", command);
        }

        public static void RemoveRightClickContextMenus()
        {
            var treeName = Runner.AppName;

            var keyDirectoryShell = $@"Directory\shell";    // Directory/shell
            var keyDirectoryBackgroundShell = $@"Directory\Background\shell"; // Directory/Background/shell
            var keyStarShell = $@"*\shell";                 // *\shell -- file
            var keyFolderShell = $@"Folder\shell";          // Folder\shell

            RemoveRightClickContextMenu(RightClickContextMenuContext, keyDirectoryShell, treeName);
            RemoveRightClickContextMenu(RightClickContextMenuContext, keyDirectoryBackgroundShell, treeName);
            RemoveRightClickContextMenu(RightClickContextMenuContext, keyStarShell, treeName);
            RemoveRightClickContextMenu(RightClickContextMenuContext, keyFolderShell, treeName);
        }

        public static void RemoveRightClickContextMenu(RegistryKey context, string keyPath, string treeName)
        {
            DeleteSubKeyTree(context, keyPath, treeName);
        }
    }
}
