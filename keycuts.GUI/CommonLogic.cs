using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class CommonLogic
    {
        public static ExitCode CreateShortcut(string destination, string shortcutName, bool force = false)
        {
            var result = ExitCode.NotStarted;
            var runner = new Runner();

            if (!force)
            {
                force = runner.GetForceOverwrite();
            }

            if (!string.IsNullOrEmpty(destination) && 
                !string.IsNullOrEmpty(shortcutName))
            {
                var args = new KeycutArgs(
                    destination,
                    shortcutName,
                    force: force);

                result = runner.Run(args);
            }
            else
            {
                result = ExitCode.BadArguments;
            }

            return result;
        }

        public static void SetOutputFolder(string outputFolder)
        {
            var runner = new Runner();
            runner.SetOutputFolder(outputFolder);
        }
    }
}
