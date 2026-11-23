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
            System.Diagnostics.Process.Start(GuiLauncher.StartInfo(path))?.Dispose();
        }

        public static ExitCode Run(string[] args)
        {
            // Before the -h rewrite and the parser: what follows an open's name is the keycut's, dashes and all
            if (KeycutCommands.TryReadOpen(args, out var openName, out var openArguments))
            {
                return RunManage(context => KeycutCommands.Open(context, openName, openArguments));
            }

            // "create" is the default verb, so the original "keycuts -t … -s …" still works
            // -h is the usual short form of --help; the parser only knows the long one
            args = args.Select(arg => arg == "-h" ? "--help" : arg).ToArray();
            var result = Usage.CreateParser().ParseArguments<Options, PasteOptions, ListOptions, RemoveOptions, RenameOptions, OpenOptions, PathOptions, InstallShellIntegrationOptions, UninstallShellIntegrationOptions>(args);
            return result.MapResult(
                (Options parsed) => RunCreate(parsed),
                (PasteOptions parsed) => RunPaste(parsed),
                (ListOptions parsed) => RunList(parsed),
                (RemoveOptions parsed) => RunManage(context => KeycutCommands.Remove(context, parsed.Name)),
                (RenameOptions parsed) => RunManage(context => KeycutCommands.Rename(context, parsed.OldName, parsed.NewName, parsed.Force)),
                (OpenOptions parsed) => RunManage(context => KeycutCommands.Open(context, parsed.Name, KeycutCommands.JoinArguments((parsed.Arguments ?? Enumerable.Empty<string>()).ToArray(), 0))),
                (PathOptions parsed) => RunPath(parsed.Action),
                (InstallShellIntegrationOptions parsed) => RunShellIntegration(true),
                (UninstallShellIntegrationOptions parsed) => RunShellIntegration(false),
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
                    Launch = Desktops.Current.RunKeycut
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


        /// <summary>
        /// <c>keycuts install-shell-integration</c> and <c>uninstall-shell-integration</c> (Phase 7): what Settings' right-click toggle
        /// does, for the package's post-install and for scripts. The Quick Action on macOS, the Explorer verbs on Windows.
        /// </summary>
        private static ExitCode RunShellIntegration(bool install)
        {
            var name = OperatingSystem.IsMacOS() ? "Finder Quick Action" : "File Explorer menu";

            if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("keycuts: shell integration isn't supported on this platform.");
                Usage.MarkErrorWritten();
                return ExitCode.CannotChangeShellIntegration;
            }

            if (!RegistryStuff.SetRightClickContextMenu(install))
            {
                Console.Error.WriteLine($"keycuts: couldn't {(install ? "add" : "remove")} the {name}.");
                Usage.MarkErrorWritten();
                return ExitCode.CannotChangeShellIntegration;
            }

            Console.WriteLine(install ? $"Added \"keycut this!\" ({name})." : $"Removed \"keycut this!\" ({name}).");
            return ExitCode.Success;
        }

        /// <summary>
        /// <c>keycuts path [status|add|remove]</c> (#101): on macOS the block in the shell profile (<see cref="KeycutPathCommand"/>);
        /// on Windows the user PATH, which creates already keep up to date.
        /// </summary>
        private static ExitCode RunPath(string action)
        {
            var folder = new Runner().GetOutputFolder();

            if (OperatingSystem.IsMacOS())
            {
                var result = KeycutPathCommand.Run(action, folder, MacShellPath.CreateDefault(), Console.Out, Console.Error);
                if (result != ExitCode.Success)
                {
                    Usage.MarkErrorWritten();
                }

                return result;
            }

            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("keycuts: PATH isn't supported on this platform.");
                Usage.MarkErrorWritten();
                return ExitCode.CannotUpdatePath;
            }

            switch (string.IsNullOrEmpty(action) ? "status" : action.ToLowerInvariant())
            {
                case "status":
                    Console.WriteLine(UserPathUpdater.IsOnPath(folder) ? $"{folder} is on PATH, so Win+R runs your keycuts by name." : $"{folder} isn't on PATH. 'keycuts path add' adds it.");
                    return ExitCode.Success;
                case "add":
                case "remove":
                    var add = action.Equals("add", StringComparison.OrdinalIgnoreCase);
                    if (!new UserPathUpdater().AddToOrReplace(add ? null : folder, add ? folder : null))
                    {
                        Console.Error.WriteLine("keycuts: couldn't change the user PATH.");
                        Usage.MarkErrorWritten();
                        return ExitCode.CannotUpdatePath;
                    }

                    Console.WriteLine(add ? $"Added {folder} to the user PATH." : $"Took {folder} out of the user PATH.");
                    return ExitCode.Success;
                default:
                    Console.Error.WriteLine($"keycuts: path takes {string.Join(", ", KeycutPathCommand.Actions)}.");
                    Usage.MarkErrorWritten();
                    return ExitCode.BadArguments;
            }
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

            Action<string> setClipboard = OperatingSystem.IsWindows()
                ? WindowsClipboard.SetText
                : OperatingSystem.IsMacOS() ? MacClipboard.SetText : _ => throw new PlatformNotSupportedException("The clipboard isn't supported on this platform.");

            try
            {
                return KeycutPaste.Run(
                    parsed.Name,
                    KeycutPaste.GetDefaultFolders(),
                    System.IO.File.Exists,
                    System.IO.File.ReadAllLines,
                    System.IO.File.ReadAllText,
                    PathVariables.Expand,
                    setClipboard,
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
                else if (result == ExitCode.NotStarted && OperatingSystem.IsMacOS() && IsFinderAlias(options.Target))
                {
                    // Phase 4.4: an alias needs the Finder to read it, so it's refused with the reason
                    Console.Error.WriteLine($"keycuts: \"{options.Target}\" is a Finder alias, which a keycut can't open. Use the original file or folder.");
                    Usage.MarkErrorWritten();
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

        private static bool IsFinderAlias(string target)
        {
            try
            {
                return MacTargets.IsFinderAlias(System.IO.Path.GetFullPath(target));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }
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

            return NameCheck.Run(ScriptFormats.NameOf(batPath), System.IO.Path.GetDirectoryName(batPath),
                askBeforeReplacing, environment);
        }
    }
}
