using CommandLine;
using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.CLI
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var result = Run(args);
            if (result != ExitCode.Success)
            {
                Console.WriteLine($"Error: {result}");
            }
            return (int)result;
        }

        public static ExitCode Run(string[] args)
        {
            var result = ExitCode.NotStarted;

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
                    // Show help text
                    Parser.Default.ParseArguments<Options>(new string[] { "--help" });
                    result = ExitCode.Success;
                }
            }

            if (result != ExitCode.Success)
            {
                result = ExitCode.BadArguments;
            }

            return result;
        }
    }
}
