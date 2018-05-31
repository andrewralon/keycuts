using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.CLI
{
    public class RegistryKey
    {
        public static void SetDefaultShortcutsFolder(string defaultFolder, string keyPath)
        {
            var currentUser = Registry.CurrentUser;
            var folder = currentUser.OpenSubKey(keyPath, true);

            if (folder == null)
            {
                folder = currentUser.CreateSubKey(keyPath, true);
            }

            folder?.SetValue("DefaultFolder", defaultFolder, RegistryValueKind.String);
        }

        public static string GetDefaultShortcutsFolder(string defaultFolder, string keyPath = null)
        {
            if (keyPath == null)
            {
                var appName = Assembly.GetExecutingAssembly().GetName().Name;
                keyPath = $"{ConsoleApp.RegistryKeyStartPath}{appName}";
            }

            var currentUser = Registry.CurrentUser;
            var folder = currentUser.OpenSubKey(keyPath, true);

            if (folder == null)
            {
                folder = currentUser.CreateSubKey(keyPath, true);
            }

            defaultFolder = folder?.GetValue("DefaultFolder", defaultFolder).ToString();

            return defaultFolder;
        }
    }
}
