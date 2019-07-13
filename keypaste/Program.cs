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
                    if (File.Exists(file))
                    {
                        CopyContentsToClipboard(file);
                        result = 0;
                    }
                    else
                    {
                        Console.WriteLine("File not found:   " + file);

                        if (!Path.HasExtension(file))
                        {
                            Console.WriteLine("No file extension found. Trying .txt....");
                            var fileTxt = file + ".txt";

                            if (File.Exists(fileTxt))
                            {
                                Console.WriteLine("Found file here:  " + fileTxt);

                                CopyContentsToClipboard(fileTxt);
                                result = 0;
                            }
                        }
                        else
                        {

                            Console.WriteLine("Trying the exe directory....");

                            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                            var fileExeDir = Path.Combine(exeDir, file);

                            Console.WriteLine("exeDir:      " + exeDir);
                            Console.WriteLine("fileExeDir:  " + fileExeDir);

                            if (File.Exists(fileExeDir))
                            {
                                Console.WriteLine("Found file here:  " + fileExeDir);

                                CopyContentsToClipboard(fileExeDir);
                                result = 0;
                            }
                            else
                            {
                                Console.WriteLine("File not found:   " + fileExeDir);
                                result = -1;
                            }
                        }
                    }
                }
                catch
                {
                    result = -1;
                }
            }
            else
            {
                // Show help text
                //Parser.Default.ParseArguments<Options>(new string[] { "--help" });
                Console.WriteLine("Maybe include some args next time...?");
                result = 0;
            }

#if DEBUG
            Console.ReadKey();
#endif

            return result;
        }

        public static string CopyContentsToClipboard(string file)
        {
            var contents = File.ReadAllText(file);
            Clipboard.SetText(contents);
            return contents;
        }
    }
}
