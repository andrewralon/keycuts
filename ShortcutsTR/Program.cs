using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Program
    {
        private static string destination;

        private static string shortcutPath;

        private static void Main(string[] args)
        {
            // RELEASE Uncomment this for normal use 
            //Runner(args);

            // DEBUG Uncomment this section to manually pass destination and shortcut name
            string[] debugArgs = new string[]
            {
                //@"C:\randomfile.txt", // File
                //@"C:\Users", // Folder
                //@"C:\randomurl.url", // URL shortcut file
                @"C:\Dropbox.lnk", // Shortcut to folder
                //@"C:\Windows\System32\drivers\etc\hosts", // hosts file
                @"C:\Shortcuts\test.bat",
                @"C:\third\argument"
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
                destination = args[0];
                shortcutPath = args[1];

                // DEBUG Uncomment for more information
                //Console.WriteLine(string.Format("  Destination:   {0}", destination));
                //Console.WriteLine(string.Format("  Shortcut Path: {0}", shortcutPath));

                // TODO Handle 3 arguments to tell which app will open the destination file/folder/URL
                //  Example: Open a file with Notepad++, not Notepad
                if (args.Length > 2)
                {
                    Console.WriteLine("Only two arguments are supported for now. Ignoring extra arguments.");
                }

                // Run app and pass arguments as parameters
                var app = new ConsoleApp();
                app.Run(destination, shortcutPath);
            }
            else
            {
                Console.WriteLine("  arg1 = location of destination file, folder, or URL (no spaces)");
                Console.WriteLine("    Examples:");
                Console.WriteLine("      C:\\full\\path\to\\file.extension");
                Console.WriteLine("      C:\\full\\path\to\\folder");
                Console.WriteLine("      https://website.com/");
                Console.WriteLine("  arg2 = full path of shortcut filename");
                Console.WriteLine("    Example:");
                Console.WriteLine("      C:\\Shortcuts\\shortcut-name.bat");
                Console.WriteLine("Not enough arguments given. Maybe next time.");
            }

            // DEBUG Uncomment for testing so command prompt stays open
            Console.ReadKey();
        }
    }
}
