using CommandLine;
using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.CLI
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var result = Run(args);

            if (result == 0)
            {
                Console.WriteLine("Done. YAY!");
            }
            else
            {
                Console.WriteLine("Did something bad happen?");
            }
#if DEBUG
            Console.Read(); // Leave command prompt open when testing
#endif
            return result;
        }

        public static int Run(string[] args)
        {
            var result = -1;

            var parsedArgs = Parser.Default.ParseArguments<Options>(args);
            if (!parsedArgs.Errors.Any())
            {
                var options = new Options
                {
                    Destination = parsedArgs.Value.Destination?.Trim(),
                    Shortcut = parsedArgs.Value.Shortcut?.Trim(),
                    OpenWithApp = parsedArgs.Value.OpenWithApp?.Trim(),
                    OutputFolder = parsedArgs.Value.OutputFolder?.Trim(),
                    Force = parsedArgs.Value.Force
                };

                if (options.Destination != null && options.Shortcut != null)
                {
                    var keycutArgs = new KeycutArgs(
                        options.Destination,
                        options.Shortcut,
                        options.OpenWithApp,
                        options.OutputFolder,
                        options.Force);

                    // Run app and pass arguments as parameters
                    var app = new Runner();
                    result = app.Run(keycutArgs);
                }
                else
                {
                    throw new Exception("Bad things happened.");
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
