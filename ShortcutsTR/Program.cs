using CommandLine;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Program
    {
        public static string DefaultFolder { get; private set; } = @"C:\Shortcuts";

        private static int Main(string[] args)
        {
            //int result = Runner(args); // new string[] { "--help" });		// RELEASE For normal use, called from the command line

            int result = RunnerDebug();	// DEBUG Uncomment this to test pre-determined parameters

            if (result == 0)
            {
                Console.WriteLine("Done. YAY!");
            }
            else
            {
                //Console.WriteLine("Did something bad happen?");
            }

            //Console.ReadKey(); // DEBUG Uncomment for testing so command prompt stays open

            return result;
        }

        private static int RunnerDebug()
        {
            var destination =
                //@"C:\randomfile.txt";		// File
                //@"C:\Users";				// Folder
                //@"C:\Dropbox.lnk";		// Shortcut to folder
                //"https://github.com/";  // URL
                //"https://calendar.google.com/calendar/r?tab=mc&pli=1#main_7";
                "https://www.google.com/search?q=regex+replace+c%23&oq=regex+replace+c%23&aqs=chrome..69i57j0l5.15967j0j7&sourceid=chrome&ie=UTF-8";
                //@"C:\randomurl.url";		// URL shortcut file
                //@"C:\Windows\System32\drivers\etc\hosts"; // hosts file

            var shortcut =
                //@"C:\Shortcuts\test.bat";	// Full path to shortcut
                //@"test.bat";				// Incomplete path to shortcut
                @"test3";                    // Incomplete path to shortcut, no extension

            var openWithAppPath =
                "";									// Empty path given; will open normally
                //@"C:\Windows\System32\notepad.exe";	// Path to notepad
                //@"C:\Program Files (x86)\Notepad++\notepad++.exe"; // Path to notepad++

            // Force - False by default. To overwrite existing shortcut, use "-f"

            string[] debugArgs = new string[]
            {
                //"-o C:\\Shortcuts",
                "-d " + destination,
                "-s " + shortcut,
                "-a " + openWithAppPath,
                "-f"
            };

            return Runner(debugArgs);
        }

        private static int Runner(string[] args)
        {
            var result = 1;

            string appName = Assembly.GetExecutingAssembly().GetName().Name;
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            var parsedArgs = Parser.Default.ParseArguments<Options>(args);
            if (!parsedArgs.Errors.Any())
            {
                Console.WriteLine(appName);
                Console.WriteLine(version);

                var options = new Options
                {
                    Destination = parsedArgs.Value.Destination?.Trim(),
                    Shortcut = parsedArgs.Value.Shortcut?.Trim(),
                    OpenWithAppPath = parsedArgs.Value.OpenWithAppPath?.Trim(),
                    Force = parsedArgs.Value.Force,
                    DefaultFolder = parsedArgs.Value.DefaultFolder?.Trim()
                };

                if (options.DefaultFolder != null)
                {
                    RegistryKey.SetDefaultShortcutsFolder(options.DefaultFolder);
                }
                else if (RegistryKey.GetDefaultShortcutsFolder("") == "")
                {
                    RegistryKey.SetDefaultShortcutsFolder(DefaultFolder);
                }

                if (options.Destination != null && options.Shortcut != null)
                {
                    // Run app and pass arguments as parameters
                    var app = new ConsoleApp(appName, version);
                    result = app.Run(options);
                }
            }
            //else
            //{
            //	foreach (var error in parsedArgs.Errors)
            //	{
            //		Console.WriteLine("Error: " + error.ToString());
            //	}
            //}

            return result;
        }
    }
}
