using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class RegistryKey
    {
        public static void SetDefaultShortcutsFolder(string defaultFolder)
        {
            var keyPath = @"Software\TeamRalon\ShortcutsTR";// + AppName;
            var currentUser = Registry.CurrentUser;
            var folder = currentUser.OpenSubKey(keyPath, true);

            if (folder == null)
            {
                folder = currentUser.CreateSubKey(keyPath, true);
            }

            if (folder != null)
            {
                folder.SetValue("DefaultFolder", defaultFolder, RegistryValueKind.String);
            }
        }

        public static string GetDefaultShortcutsFolder(string defaultFolder)
        {
            var keyPath = @"Software\TeamRalon\ShortcutsTR";// + AppName;
            var currentUser = Registry.CurrentUser;
            var folder = currentUser.OpenSubKey(keyPath, true);

            if (folder == null)
            {
                folder = currentUser.CreateSubKey(keyPath, true);
            }

            if (folder != null)
            {
                defaultFolder = folder.GetValue("DefaultFolder", defaultFolder).ToString();
            }

            return defaultFolder;
        }
    }
}
