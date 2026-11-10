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
        public static string CurrentDirectory { get; set; }

        public static int Main(string[] args)
        {
            var result = Run(args);
            if (result != ExitCode.Success)
            {
                Console.WriteLine($"Error: {result}");
            }

#if DEBUG
            Console.ReadKey();
#endif

            return (int)result;
        }

        public static ExitCode Run(string[] args)
        {
            // "create" is the default verb, so the original "keycuts -d … -s …" still works
            return Parser.Default.ParseArguments<Options, PasteOptions>(args).MapResult(
                (Options parsed) => RunCreate(parsed),
                (PasteOptions parsed) => RunPaste(parsed),
                errors => errors.IsHelp() || errors.IsVersion() ? ExitCode.Success : ExitCode.NotStarted);
        }

        /// <summary><c>keycuts paste &lt;name&gt;</c>, which the keypaste.cmd shim beside keycuts.exe also runs (#97).</summary>
        private static ExitCode RunPaste(PasteOptions parsed)
        {
            return KeycutPaste.Run(
                parsed.Name,
                KeycutPaste.GetDefaultFolders(),
                System.IO.File.Exists,
                System.IO.File.ReadAllLines,
                System.IO.File.ReadAllText,
                WindowsClipboard.SetText,
                Console.Out,
                Console.Error);
        }

        private static ExitCode RunCreate(Options parsed)
        {
            var result = ExitCode.NotStarted;

            var options = new Options
            {
                Destination = parsed.Destination?.Trim(),
                Shortcut = parsed.Shortcut?.Trim(),
                OpenWithApp = parsed.OpenWithApp?.Trim(),
                OutputFolder = parsed.OutputFolder?.Trim(),
                Force = parsed.Force
            };

            if (options.Destination != null && options.Shortcut != null)
            {
                var keycutArgs = new KeycutArgs(
                    options.Destination,
                    options.Shortcut,
                    options.OpenWithApp,
                    options.OutputFolder,
                    options.Force);

                // Run app and pass arguments as parameters. Environment variables in paths (#31) follow the setting.
                var app = new Runner();
                keycutArgs.UseEnvironmentVariables = app.GetUseEnvironmentVariables();
                result = app.Run(keycutArgs);

                if (result == ExitCode.FileAlreadyExists)
                {
                    Console.WriteLine($"Shortcut file {options.Shortcut} already exists.");
                    Console.Write("Overwrite? (y / n)  ");
                    var overwriteInput = Console.ReadKey().KeyChar.ToString();
                    Console.WriteLine();

                    if (overwriteInput.ToLower() == "y")
                    {
                        // Force overwrite
                        keycutArgs.Force = true;
                        result = app.Run(keycutArgs);
                    }
                    else
                    {
                        // Declining is a choice, not a failure (#65)
                        Console.WriteLine("Kept the existing shortcut.");
                        result = ExitCode.Success;
                    }
                }
            }
            else
            {
                // Show help text, listing both verbs
                Parser.Default.ParseArguments<Options, PasteOptions>(new string[] { "--help" });
                result = ExitCode.Success;
            }

            return result;
        }
    }
}
