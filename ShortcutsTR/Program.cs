using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Program
    {
        private static int Main(string[] args)
        {
            int result = Runner(args);  // RELEASE Uncomment this for normal use 

            //int result = RunnerDebug(); // DEBUG Uncomment this to pass in pre-determined parameters

            if (result == 0)
            {
                Console.WriteLine("Done. YAY!");
            }
            else
            {
                Console.WriteLine("Incorrect arguments given. Better luck next time.");
            }

            Console.WriteLine();
            Console.ReadKey(); // DEBUG Uncomment for testing so command prompt stays open

            return result;
        }

        private static int Runner(string[] args)
        {
            var result = 1;
            var options = new Options();
            var parsedArgs = Parser.Default.ParseArguments<Options>(args);

            if (!parsedArgs.Errors.Any())
            {
                options.Destination = parsedArgs.Value.Destination.Trim();
                options.Shortcut = parsedArgs.Value.Shortcut.Trim();
                options.OpenWithAppPath = parsedArgs.Value.OpenWithAppPath?.Trim();
                options.Force = parsedArgs.Value.Force;

                // Run app and pass arguments as parameters
                var app = new ConsoleApp();
                result = app.Run(options);
            }

            return result;
        }

        private static int RunnerDebug()
        {
            var destination =
                @"C:\randomfile.txt";       // File
                //@"C:\Users";              // Folder
                //@"C:\Dropbox.lnk";        // Shortcut to folder
                //@"C:\Public Desktop.lnk"; // Shortcut to folder via relative path (down) -- are these allowed?
                //@"%HOMEDRIVE%%HOMEPATH%\Desktop\My Documents.lnk"; // Shortcut to folder via relative path (up) -- are these allowed?
                //"https://github.com/";    // URL
                //@"C:\randomurl.url";      // URL shortcut file
                //@"C:\Windows\System32\drivers\etc\hosts"; // hosts file
                //@"C:\hosts.lnk";          // Shortcut to hosts file -- this is a weird use case that will *not* be implemented

            var shortcut =
                //@"C:\Shortcuts\test.bat"; // Full path to shortcut
                //@"test.bat";              // Incomplete path to shortcut
                @"test";                    // Incomplete path to shortcut, no extension

            var openWithAppPath =                   // Path to program to open destination
                //"";                               // Empty path given; will open normally
                @"C:\Windows\System32\notepad.exe"; // Path to notepad
                //@"C:\Program Files (x86)\Notepad++\notepad++.exe"; // Path to notepad++

            // Force - False by default. To overwrite shortcut if it exists, add "-f"

            string[] debugArgs = new string[]
            {
                "-d " + destination,
                "-s " + shortcut,
                "-o " + openWithAppPath,
                "-f"
            };

            return Runner(debugArgs);
        }
    }
}
