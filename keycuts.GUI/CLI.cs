using keycuts.CLI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class CLI
    {
        public static void CreateShortcut(string destination, string shortcutName)
        {
            if (!string.IsNullOrEmpty(destination) && 
                !string.IsNullOrEmpty(shortcutName))
            {
                var args = new string[]
                {
                    // Surround with quotes?
                    $"-d {destination}",
                    $"-s {shortcutName}"
                };

                Program.Main(args);
            }
        }

        public static void OpenShortcutsFolder()
        {
            var defaultFolder = RegistryKey.GetDefaultShortcutsFolder(Program.DefaultFolder);

            Process.Start(defaultFolder);
        }
    }
}
