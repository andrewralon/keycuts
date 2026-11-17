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
            // "create" is the default verb, so the original "keycuts -t … -s …" still works
            // -h is the usual short form of --help; the parser only knows the long one
            args = args.Select(arg => arg == "-h" ? "--help" : arg).ToArray();
            var result = Usage.CreateParser().ParseArguments<Options, PasteOptions, ListOptions, RemoveOptions, RenameOptions, OpenOptions>(args);
            return result.MapResult(
                (Options parsed) => RunCreate(parsed),
                (PasteOptions parsed) => RunPaste(parsed),
                (ListOptions parsed) => RunList(parsed),
                (RemoveOptions parsed) => RunManage(context => KeycutCommands.Remove(context, parsed.Name)),
                (RenameOptions parsed) => RunManage(context => KeycutCommands.Rename(context, parsed.OldName, parsed.NewName, parsed.Force)),
                (OpenOptions parsed) => RunManage(context => KeycutCommands.Open(context, parsed.Name, JoinArguments(parsed.Arguments))),
                errors => Usage.Show(result, errors, Console.Out, Console.Error));
        }

        /// <summary>
        /// <c>rm</c>, <c>rename</c> and <c>open</c> (#173), through <see cref="KeycutCommands"/> on the keycuts folder, as the Manager
        /// works on it: deletes go to the Recycle Bin, a rename checks the new name like the Manager's does, and a keycut opens
        /// through the shell, as from Win+R. The library's parser logging goes to stderr, so stdout is only the verb's own lines.
        /// </summary>
        private static ExitCode RunManage(Func<KeycutCommandContext, ExitCode> verb)
        {
            var output = Console.Out;
            Console.SetOut(Console.Error);

            try
            {
                var repository = KeycutRepository.CreateDefault();
                var context = new KeycutCommandContext(repository, output, Console.Error)
                {
                    IsInputRedirected = Console.IsInputRedirected,
                    ReadKey = () => Console.ReadKey().KeyChar,
                    CheckName = name => NameCheck.Run(name, repository.OutputFolder, false, NameConflictEnvironment.GetDefault()),
                    Launch = (path, arguments) => System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(path, arguments) { UseShellExecute = true })?.Dispose()
                };

                var result = verb(context);
                if (result != ExitCode.Success)
                {
                    // The verb said what went wrong
                    Usage.MarkErrorWritten();
                }

                return result;
            }
            finally
            {
                Console.SetOut(output);
            }
        }

        /// <summary>The arguments after a keycut's name, joined for the .bat, each quoted when it holds a space.</summary>
        private static string JoinArguments(IEnumerable<string> arguments)
        {
            return string.Join(" ", (arguments ?? Enumerable.Empty<string>())
                .Select(argument => argument.IndexOfAny(new[] { ' ', '\t' }) >= 0 && !argument.StartsWith("\"") ? "\"" + argument + "\"" : argument));
        }

        /// <summary>
        /// <c>keycuts list</c> (#173, #237): the keycuts folder's keycuts, read like the Manager reads them, with this user's run
        /// counts. A missing folder lists nothing.
        /// </summary>
        private static ExitCode RunList(ListOptions parsed)
        {
            if (!KeycutListing.TryParseSort(parsed.Sort, out var sort))
            {
                Console.Error.WriteLine($"keycuts: --sort must be one of {string.Join(", ", KeycutListing.SortNames)}.");
                Console.Error.WriteLine("Run 'keycuts list --help' for usage.");
                Usage.MarkErrorWritten();
                return ExitCode.BadArguments;
            }

            // The parser logs while it reads a keycut; keep stdout to the listing, for a script that reads it
            var output = Console.Out;
            Console.SetOut(Console.Error);
            IReadOnlyList<Keycut> keycuts;

            try
            {
                keycuts = KeycutRepository.CreateDefault().LoadAllAsync().GetAwaiter().GetResult();
            }
            catch (KeycutRepositoryException ex)
            {
                // An invalid or unreadable folder: say which, on stderr
                Console.Error.WriteLine($"keycuts: {ex.Message}");
                Usage.MarkErrorWritten();
                return ExitCode.CannotFindPath;
            }
            finally
            {
                Console.SetOut(output);
            }

            var listed = KeycutListing.Sort(KeycutListing.Filter(keycuts, parsed.Search), sort);

            if (parsed.Json)
            {
                output.WriteLine(KeycutListing.ToJson(listed));
                return ExitCode.Success;
            }

            foreach (var line in KeycutListing.Format(listed, parsed.Usage))
            {
                output.WriteLine(line);
            }

            return ExitCode.Success;
        }

        /// <summary><c>keycuts paste &lt;name&gt;</c>, which the keypaste.cmd shim also runs (#97).</summary>
        private static ExitCode RunPaste(PasteOptions parsed)
        {
            // The library logs with Console.WriteLine (the parser does while reading a keycut). Send that to stderr, so
            // stdout holds only the copied text and `keycuts paste db > file` or `for /f` gets just the target.
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
                Target = parsed.Target?.Trim(),
                Shortcut = parsed.Shortcut?.Trim(),
                OpenWithApp = parsed.OpenWithApp?.Trim(),
                OutputFolder = parsed.OutputFolder?.Trim(),
                Description = parsed.Description?.Trim(),
                Force = parsed.Force
            };

            if (options.Target != null && options.Shortcut != null)
            {
                var keycutArgs = new KeycutArgs(
                    options.Target,
                    options.Shortcut,
                    options.OpenWithApp,
                    options.OutputFolder,
                    options.Force);

                // Run app and pass arguments as parameters. Environment variables in paths (#31) follow the setting.
                var app = new Runner { Usage = UsageLog.CreateDefault() };
                keycutArgs.UseEnvironmentVariables = app.GetUseEnvironmentVariables();
                keycutArgs.Description = options.Description ?? "";

                // Replacing a keycut and a name that other commands use both ask first, unless -f (#153)
                var askBeforeReplacing = app.GetAskBeforeReplacing();
                var check = options.Force ? null : CheckName(app, options, askBeforeReplacing);
                var decision = CreateConfirmation.Decide(check, options.Target, options.Force, askBeforeReplacing,
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
            // Check the exact file the create will write, so a replace shows that file's target
            var batPath = Shortcut.GetFullPath(options.Shortcut, outputFolder);

            return NameCheck.Run(System.IO.Path.GetFileNameWithoutExtension(batPath), System.IO.Path.GetDirectoryName(batPath),
                askBeforeReplacing, environment);
        }
    }
}
