using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Program
    {
        private static string shortcutPath;

        private static string shortcutName;

        private static void Main(string[] args)
        {
            // RELEASE Uncomment this for normal use 
            //Runner(args);

            // DEBUG Uncomment this section to manually pass destination and shortcut name
            string[] debugArgs = new string[2]
            {
                "C:\\windows - version.txt",
                "test"
            };
            Runner(debugArgs);
        }

        // TODO Make this an int to return 0 or 1?
        private static void Runner(string[] args)
        {
            // TODO Print app name here
            Console.WriteLine();
            Console.WriteLine("Shortcuts");

            if (args != null && args.Length >= 2)
            {
                if (args.Length == 2)
                {
                    shortcutPath = args[0];
                    shortcutName = args[1];

                    // TODO Run app and pass arguments as parameters
                    //command to run app goes here
                }
                else
                {
                    Console.WriteLine("Only two arguments are supported for now. Ignoring extra arguments.");
                }
            }
            else
            {
                Console.WriteLine("  arg1 = location of destination file, folder, or URL (no spaces)");
                Console.WriteLine("    Examples:");
                Console.WriteLine("      c:\\full\\path\to\\file.extension");
                Console.WriteLine("      c:\\full\\path\to\\folder");
                Console.WriteLine("      https://website.com/");
                Console.WriteLine("  arg2 = name of shortcut");
                Console.WriteLine("Not enough arguments given. Maybe next time.");
            }

            // DEBUG Uncomment for testing so command prompt stays open
            Console.ReadKey();
        }

    }
}
