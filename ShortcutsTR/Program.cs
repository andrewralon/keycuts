using CommandLine;
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
        public static readonly string RegistryKeyStartPath = @"Software\TeamRalon\";

        public static readonly string DefaultFolder = @"C:\Shortcuts";

        private static int Main(string[] args)
        {
            var result = 0;
#if DEBUG
            result = RunnerDebug(); // Tests pre-determined parameters
#else
            result = Runner(args);  // new string[] { "--help" });
#endif
            if (result == 0)
            {
                Console.WriteLine("Done. YAY!");
            }
            else
            {
                Console.WriteLine("Did something bad happen?");
            }
#if DEBUG
            Console.ReadKey(); // Leave command prompt open when testing
#endif
            return result;
        }

        private static int RunnerDebug()
        {
            var outputFolder =
                //@"C:\NewShortcutFolder";
                @"C:\Shortcuts";

            var destination =
                //@"C:\randomfile.txt";     // File
                //@"C:\Users";				// Folder
                //@"C:\Dropbox.lnk";		// Shortcut to folder
                //"https://github.com/";    // URL #1
                //"https://calendar.google.com/calendar/r?tab=mc&pli=1#main_7"; // URL #2
                //@"C:\randomurl.url";		// URL shortcut file
                @"C:\Windows\System32\drivers\etc\hosts"; // hosts file

            var shortcut =
                //@"C:\Shortcuts\test.bat";	// Full path to shortcut
                //@"test.bat";				// Incomplete path to shortcut
                @"test3";                   // Incomplete path to shortcut, no extension

            var openWithAppPath =
                //@"C:\Windows\System32\notepad.exe";	// Path to notepad
                //@"C:\Program Files (x86)\Notepad++\notepad++.exe"; // Path to notepad++
                "";                                     // Empty path; will open normally

            // Force - False by default. To overwrite existing shortcut, use "-f"

            string[] debugArgs = new string[]
            {
                "-o " + outputFolder,
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

            Console.WriteLine(string.Format("{0} {1}", appName, version));

            var parsedArgs = Parser.Default.ParseArguments<Options>(args);
            if (!parsedArgs.Errors.Any())
            {
                var options = new Options
                {
                    Destination = parsedArgs.Value.Destination?.Trim(),
                    Shortcut = parsedArgs.Value.Shortcut?.Trim(),
                    OpenWithAppPath = parsedArgs.Value.OpenWithAppPath?.Trim(),
                    Force = parsedArgs.Value.Force,
                    DefaultFolder = parsedArgs.Value.DefaultFolder?.Trim()
                };

                string registryKeyPath = string.Format("{0}{1}", RegistryKeyStartPath, appName);
                var oldDefaultFolder = RegistryKey.GetDefaultShortcutsFolder(DefaultFolder, registryKeyPath);

                if (options.DefaultFolder == null)
                {
                    // If not given, use the existing default folder
                    options.DefaultFolder = oldDefaultFolder;
                }

                if (options.DefaultFolder != oldDefaultFolder)
                {
                    // Update the registry key if the default folder has changed
                    RegistryKey.SetDefaultShortcutsFolder(options.DefaultFolder, registryKeyPath);
                }

                PathSetup.AddToOrReplaceInSystemPath(oldDefaultFolder, options.DefaultFolder);

                if (options.Destination != null && options.Shortcut != null)
                {
                    // Run app and pass arguments as parameters
                    var app = new ConsoleApp(appName, version);
                    result = app.Run(options);
                }
                else
                {
                    // Throw new exception here?
                    Console.WriteLine("Values cannot be null: Destination, Shortcut");
                }
            }
            else
            {
                Console.WriteLine("***Errors:");

                foreach (var error in parsedArgs.Errors)
                {
                    Console.WriteLine(error.ToString());
                }
            }

            return result;
        }
    }
}
