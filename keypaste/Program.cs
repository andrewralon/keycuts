using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using keycuts.Common;

namespace keypaste
{
    public class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var result = -1;

#if DEBUG
            //args = new string[] { "3net" };       // URL
            args = new string[] { "CORE" };         // URL with args
            //args = new string[] { "3rdparty" };   // Folder
            //args = new string[] { "cerebro" };    // File
            //args = new string[] { "setpoint" };     // Command
            //args = new string[] { "rb" };         // CLSIDKey
#endif

            if (args.Any() && !string.IsNullOrEmpty(args[0]))
            {
                try
                {
                    var file = args[0];
                    string fileOriginal = file;

                    CheckAllTheFiles(ref file, fileOriginal);

                    if (!CheckIfExists(ref file))
                    {
                        CheckShortcutsDirectory(ref file, fileOriginal);

                        CheckAllTheFiles(ref file, fileOriginal);
                    }

                    if (!CheckIfExists(ref file))
                    {
                        CheckExeDirectory(ref file, fileOriginal);

                        CheckAllTheFiles(ref file, fileOriginal);
                    }

                    result = CopyContentsToClipboard(file);
                }
                catch
                {
                    Console.WriteLine("BAD THINGS.");
                }
            }
            else
            {
                // Show help text
                //Parser.Default.ParseArguments<Options>(new string[] { "--help" });
                Console.WriteLine("Maybe include an file path argument next time....?");
                result = 0;
            }

            if (result != 0)
            {
                Console.WriteLine("ERROR! Bad things!");
                Console.WriteLine("Exit code: " + result + "....");
            }

#if DEBUG
            Console.ReadKey();
#endif

            return result;
        }

        private static string CheckAllTheFiles(ref string file, string fileOriginal)
        {
            if (!CheckIfExists(ref file))
            {
                TryExtensionIfNotGiven(ref file, fileOriginal, ".txt");
            }

            if (!CheckIfExists(ref file))
            {
                TryExtensionIfNotGiven(ref file, fileOriginal, ".bat");
            }

            return file;
        }

        private static bool CheckIfExists(ref string file)
        {
            var result = false;

            if (File.Exists(file))
            {
                result = true;
            }
            else
            {
                Console.WriteLine("File not found:   " + file);
            }

            return result;
        }

        private static void TryExtensionIfNotGiven(ref string file, string fileOriginal, string extension)
        {
            file = Path.ChangeExtension(file, null);

            if (!Path.HasExtension(fileOriginal))
            {
                file += extension;
            }
        }

        private static void CheckShortcutsDirectory(ref string file, string fileOriginal)
        {
            Console.WriteLine("Trying the shortcuts directory....");
            var shortcutsDir = RegistryStuff.GetOutputFolder(Runner.DefaultOutputFolder);
            file = Path.Combine(shortcutsDir, fileOriginal);
        }

        private static void CheckExeDirectory(ref string file, string fileOriginal)
        {
            Console.WriteLine("Trying the exe directory....");
            var exeDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            file = Path.Combine(exeDir, fileOriginal);
        }

        public static int CopyContentsToClipboard(string file)
        {
            var result = -1;
            var contents = "";

            try
            {
                if (File.Exists(file))
                {
                    Console.WriteLine("File found:       " + file);
                    Console.WriteLine("Copying contents to clipboard....");

                    var extension = Path.GetExtension(file);

                    if (extension == ".bat")
                    {
                        // Parse out the desired text from a known keycut file
                        var shortcutFile = new ShortcutFile(file);
                        contents = shortcutFile.Destination;
                    }
                    else
                    {
                        contents = File.ReadAllText(file);
                    }

                    Console.WriteLine("");
                    Console.WriteLine("*** contents - start ***");
                    Console.WriteLine(contents);
                    Console.WriteLine("*** contents - end ***");
                    Console.WriteLine("");

                    Clipboard.SetText(contents);
                    result = 0;
                }
                else
                {
                    Console.WriteLine("File not found:   " + file);
                    Console.WriteLine("Giving up.... :(");
                }
            }
            catch
            {
                Console.WriteLine("BAD THINGS.");
            }

            Console.WriteLine("Done!");

            return result;
        }
    }
}
