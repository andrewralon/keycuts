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

            if (!File.Exists(fileOriginal))
            {
                if (!Path.HasExtension(fileOriginal))
                {
                    Console.WriteLine("No file extension found. Trying .txt....");
                    file = Path.Combine(fileOriginal, ".txt");
                }
            }

            if (!File.Exists(file))
            {
                Console.WriteLine("Trying the exe directory....");

                var exeDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                file = Path.Combine(exeDir, fileOriginal);
            }

            return CopyContentsToClipboard(file);
        }

        public static int CopyContentsToClipboard(string file)
        {
            int result = -1;

            try
            {
                if (File.Exists(file))
                {
                    Console.WriteLine("Found file here:  " + file);
                    var contents = File.ReadAllText(file);
                    Clipboard.SetText(contents);
                    result = 0;
                }
                else
                {
                    Console.WriteLine("File not found:   " + file);
                }
            }
            catch
            {
            }

            return result;
        }
    }
}
