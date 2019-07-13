using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace keypaste
{
    public class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var result = -1;

#if DEBUG
            args = new string[] { "address" };
#endif

            if (args.Any())
            {
                var file = args[0];

                try
                {
                    result = AttemptToCopyContentsToClipboard(file);
                }
                catch
                {
                }
            }
            else
            {
                // Show help text
                //Parser.Default.ParseArguments<Options>(new string[] { "--help" });
                Console.WriteLine("Maybe include some args next time....?");
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

        public static int AttemptToCopyContentsToClipboard(string file)
        {
            string fileOriginal = file;

            if (!CheckIfExists(ref file))
            {
                TryExtensionIfNotGiven(ref file, ".txt");
            }

            if (!CheckIfExists(ref file))
            {
                CheckExeDirectory(ref file, fileOriginal);

                if (!CheckIfExists(ref file))
                {
                    TryExtensionIfNotGiven(ref file, ".txt");
                }
            }

            return CopyContentsToClipboard(file);
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

        private static void TryExtensionIfNotGiven(ref string file, string extension)
        {
            if (!Path.HasExtension(file))
            {
                Console.WriteLine("No extension given. Trying " + extension + "....");
                file += extension;
            }
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
            int result = -1;

            try
            {
                if (File.Exists(file))
                {
                    Console.WriteLine("File found:       " + file);
                    Console.WriteLine("Copying contents to clipboard....");

                    var contents = File.ReadAllText(file);
                    Clipboard.SetText(contents);
                    result = 0;

                    Console.WriteLine("Done!");
                }
                else
                {
                    Console.WriteLine("File not found:   " + file);
                    Console.WriteLine("Giving up.... :(");
                }
            }
            catch
            {
            }

            return result;
        }
    }
}
