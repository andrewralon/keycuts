using CommandLine;
using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace keycuts.CLI
{
    public class Program
    {
        public static string CurrentDirectory { get; set; }

        public static int Main(string[] args)
        {
            // A bare "keycuts" (Win+R, Start) opens the app; any argument, even --help, stays the command line
            var launched = GuiLauncher.TryLaunch(args, AppContext.BaseDirectory, System.IO.File.Exists, StartGui);
            if (launched == ExitCode.CannotFindFile)
            {
                // A CLI-only copy has no GUI beside it, so show the help it used to
                return (int)Run(new[] { "--help" });
            }

            var result = launched ?? Run(args);
            if (result != ExitCode.Success && !Usage.WroteError)
            {
                // stderr, so a failed `keycuts paste` leaves stdout empty for a script that reads it
                Console.Error.WriteLine($"Error: {result}");
            }

            return (int)result;
        }

        private static void StartGui(string path)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }

        public static ExitCode Run(string[] args)
        {
            // "create" is the default verb, so the original "keycuts -d … -s …" still works
            // -h is the usual short form of --help; the parser only knows the long one
            args = args.Select(arg => arg == "-h" ? "--help" : arg).ToArray();
            var result = Usage.CreateParser().ParseArguments<Options, PasteOptions>(args);
            return result.MapResult(
                (Options parsed) => RunCreate(parsed),
                (PasteOptions parsed) => RunPaste(parsed),
                errors => Usage.Show(result, errors, Console.Out, Console.Error));
        }

        /// <summary><c>keycuts paste &lt;name&gt;</c>, which the bin\keypaste.cmd shim also runs (#97).</summary>
        private static ExitCode RunPaste(PasteOptions parsed)
        {
            // The library logs with Console.WriteLine (the parser does while reading a keycut). Send that to stderr, so
            // stdout holds only the copied text and `keycuts paste db > file` or `for /f` gets just the destination.
            var output = Console.Out;
            Console.SetOut(Console.Error);

            try
            {
                return KeycutPaste.Run(
                    parsed.Name,
                    KeycutPaste.GetDefaultFolders(),
                    System.IO.File.Exists,
                    System.IO.File.ReadAllLines,
                    System.IO.File.ReadAllText,
                    Environment.ExpandEnvironmentVariables,
                    WindowsClipboard.SetText,
                    output,
                    Console.Error);
            }
            finally
            {
                Console.SetOut(output);
            }
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

                // Replacing a keycut and a name that other commands use both ask first, unless -f (#153)
                var askBeforeReplacing = app.GetAskBeforeReplacing();
                var check = options.Force ? null : CheckName(app, options, askBeforeReplacing);
                var decision = CreateConfirmation.Decide(check, options.Destination, options.Force, askBeforeReplacing,
                    Console.IsInputRedirected, () => Console.ReadKey().KeyChar, Console.Out, out var overwrite);

                switch (decision)
                {
                    case CreateDecision.Declined:
                        // Declining is a choice, not a failure (#65)
                        return ExitCode.Success;
                    case CreateDecision.ConfirmationNeeded:
                        return ExitCode.ConfirmationNeeded;
                }

                keycutArgs.Force = overwrite;
                result = app.Run(keycutArgs);

                if (result == ExitCode.FileAlreadyExists)
                {
                    Console.WriteLine($"Shortcut file {options.Shortcut} already exists. Run it again with -f to replace it.");
                }
            }
            else
            {
                // Say what is missing on stderr, and fail, so a script notices
                Usage.WriteMissingArguments(Console.Error);
                result = ExitCode.BadArguments;
            }

            return result;
        }

        /// <summary>
        /// What the shortcut's name runs into, in the folder it will be written to (as <see cref="Shortcut"/> reads it),
        /// against PATH as it will be after the create: <c>-o</c> moves the output folder, and its PATH entry with it.
        /// </summary>
        private static NameCheck CheckName(Runner app, Options options, bool askBeforeReplacing)
        {
            // The Debug KEYCUTS_OUTPUT_FOLDER override pins the folder, so Runner ignores -o and leaves PATH alone
            var currentFolder = app.GetOutputFolder();
            var outputFolder = DebugOverrides.GetOutputFolder() != null ? currentFolder : options.OutputFolder ?? currentFolder;
            var environment = NameConflictEnvironment.GetDefault().WithOutputFolderMoved(currentFolder, outputFolder);
            // Check the exact file the create will write, so a replace shows that file's destination
            var target = Shortcut.GetFullPath(options.Shortcut, outputFolder);

            return NameCheck.Run(System.IO.Path.GetFileNameWithoutExtension(target), System.IO.Path.GetDirectoryName(target),
                askBeforeReplacing, environment);
        }
    }
}
