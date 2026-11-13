using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace keycuts.Common
{
    public class Runner
    {
        public static readonly string AppName = "keycuts";

        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>Per-user, so a non-admin can write to it. The installer's default must match; a test compares the two.</summary>
        public static readonly string DefaultOutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Shortcuts");

        /// <summary>"Ask before replacing a keycut" is on unless the user turned it off (or had Force overwrite on, #153).</summary>
        public static readonly bool DefaultAskBeforeReplacing = true;

        public static readonly string RightClickContextMenuText = "keycut this!";

        /// <summary>The program a Folder or CLSID keycut's launch line starts. Keycuts written before #160 call it directly, without START.</summary>
        internal const string ExplorerProgram = "%SystemRoot%\\explorer.exe";

        private readonly IPathUpdater _pathUpdater = new UserPathUpdater();

        private readonly string _fixedOutputFolder;

        public Runner()
        {
            // Debug builds only: KEYCUTS_OUTPUT_FOLDER pins the folder and leaves the registry and PATH alone
            var overrideFolder = DebugOverrides.GetOutputFolder();

            if (overrideFolder != null)
            {
                _fixedOutputFolder = overrideFolder;
                _pathUpdater = new NoOpPathUpdater();
            }
        }

        /// <summary>
        /// Runner with a fixed output folder (the registry is neither read nor written for it) and a custom PATH updater.
        /// Lets tests avoid the real registry and PATH.
        /// </summary>
        public Runner(string fixedOutputFolder, IPathUpdater pathUpdater)
        {
            _fixedOutputFolder = fixedOutputFolder;
            _pathUpdater = DebugOverrides.IsOverride(fixedOutputFolder)
                ? new NoOpPathUpdater()
                : pathUpdater ?? new UserPathUpdater();
        }

        public ExitCode Run(KeycutArgs args)
        {
            return Run(args.Target, args.Shortcut, args.OutputFolder, args.OpenWithApp, args.Force, args.Description,
                args.UseEnvironmentVariables, args.PathVariables);
        }

        /// <param name="useEnvironmentVariables">Save a file or folder target with environment variables (#31). This
        /// happens after the target is classified, which needs the real path.</param>
        /// <param name="pathVariables">The variables to use; null reads them from this machine.</param>
        public ExitCode Run(string target, string shortcutPath, string outputFolder, string openWithAppPath = null, bool force = false, string description = "",
            bool useEnvironmentVariables = false, IReadOnlyList<PathVariable> pathVariables = null)
        {
            var result = ExitCode.NotStarted;

            string currentOutputFolder;

            if (_fixedOutputFolder != null)
            {
                currentOutputFolder = _fixedOutputFolder;
                outputFolder = _fixedOutputFolder;
            }
            else
            {
                currentOutputFolder = RegistryStuff.GetOutputFolder(DefaultOutputFolder);
                outputFolder = SetOutputFolder(outputFolder, currentOutputFolder);
            }

            var systemPathResult = _pathUpdater.AddToOrReplace(currentOutputFolder, outputFolder);

            if (!systemPathResult)
            {
                result = ExitCode.CannotUpdatePath;
            }
            else
            {
                var shortcut = new Shortcut(target, shortcutPath, outputFolder, openWithAppPath);

                if (useEnvironmentVariables && PathVariables.AppliesTo(shortcut.Type))
                {
                    shortcut.SetTarget(PathVariables.Substitute(shortcut.Target, pathVariables ?? PathVariables.GetDefault()));
                }

                if (shortcut.Type != ShortcutType.Unknown)
                {
                    var createOutputFolderResult = CreateOutputFolder(shortcut.Folder);

                    if (!createOutputFolderResult)
                    {
                        result = ExitCode.CannotCreateOutputFolder;
                    }
                    else
                    {
                        var createShortcutResult = CreateShortcutFile(shortcut, force, description);

                        result = createShortcutResult ?
                            ExitCode.Success :
                            ExitCode.FileAlreadyExists;
                    }
                }
            }

            return result;
        }

        #region Get Methods

        public string GetOutputFolder()
        {
            var overrideFolder = DebugOverrides.GetOutputFolder();

            if (overrideFolder != null)
            {
                return overrideFolder;
            }

            var outputFolder = RegistryStuff.GetOutputFolder(DefaultOutputFolder);

            return outputFolder;
        }

        public bool GetAskBeforeReplacing()
        {
            return RegistryStuff.GetAskBeforeReplacing(DefaultAskBeforeReplacing);
        }

        public bool GetRightClickContextMenu()
        {
            return RegistryStuff.GetRightClickContextMenu();
        }

        public ThemePreference GetThemePreference()
        {
            return RegistryStuff.GetThemePreference();
        }

        public bool GetUseEnvironmentVariables()
        {
            return RegistryStuff.GetUseEnvironmentVariables();
        }

        public bool GetCheckForUpdates()
        {
            return RegistryStuff.GetCheckForUpdates();
        }

        #endregion Get Methods

        #region Set Methods

        public string SetOutputFolder(string outputFolder, string currentOutputFolder = null)
        {
            var overrideFolder = DebugOverrides.GetOutputFolder();

            if (overrideFolder != null)
            {
                // Never persist anything while the debug override is active
                return outputFolder ?? overrideFolder;
            }

            if (currentOutputFolder == null)
            {
                currentOutputFolder = RegistryStuff.GetOutputFolder(DefaultOutputFolder);
            }

            if (outputFolder == null)
            {
                // If not given, use the existing default folder
                outputFolder = currentOutputFolder;
            }

            if (outputFolder != currentOutputFolder)
            {
                // Update the registry key if the output folder has changed
                RegistryStuff.SetOutputFolder(outputFolder);
            }

            return outputFolder;
        }

        public void SetAskBeforeReplacing(bool askBeforeReplacing)
        {
            if (GetAskBeforeReplacing() != askBeforeReplacing)
            {
                RegistryStuff.SetAskBeforeReplacing(askBeforeReplacing);
            }
        }

        public void SetThemePreference(ThemePreference preference)
        {
            if (RegistryStuff.GetThemePreference() != preference)
            {
                RegistryStuff.SetThemePreference(preference);
            }
        }

        public void SetUseEnvironmentVariables(bool useEnvironmentVariables)
        {
            if (RegistryStuff.GetUseEnvironmentVariables() != useEnvironmentVariables)
            {
                RegistryStuff.SetUseEnvironmentVariables(useEnvironmentVariables);
            }
        }

        public void SetCheckForUpdates(bool checkForUpdates)
        {
            if (RegistryStuff.GetCheckForUpdates() != checkForUpdates)
            {
                RegistryStuff.SetCheckForUpdates(checkForUpdates);
            }
        }

        /// <summary>Saves the folder and moves the PATH entry with it, so Win+R keeps finding keycuts. False if PATH couldn't be updated.</summary>
        public bool ChangeOutputFolder(string outputFolder)
        {
            var currentOutputFolder = GetOutputFolder();

            SetOutputFolder(outputFolder, currentOutputFolder);

            return _pathUpdater.AddToOrReplace(currentOutputFolder, outputFolder);
        }

        /// <summary>Adds or removes the Explorer verbs if they differ from what's asked for. False on failure.</summary>
        public bool SetRightClickContextMenu(bool rightClickContextMenu)
        {
            return GetRightClickContextMenu() == rightClickContextMenu ||
                RegistryStuff.SetRightClickContextMenu(rightClickContextMenu);
        }

        #endregion Get Methods

        private bool CreateOutputFolder(string folder)
        {
            var result = true;

            // Create the shortcuts folder if it doesn't exist already
            if (!Directory.Exists(folder))
            {
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
                {
                    result = false;
                }
            }

            return result;
        }

        public bool CreateShortcutFile(Shortcut shortcut, bool overwrite = false, string description = "")
        {
            var result = false;

            // If the shortcut file already exists, let overwrite decide
            if (!File.Exists(shortcut.FullPath) || overwrite)
            {
                var lines = BuildShortcutLines(shortcut, description);

                // Write the file to the given save path
                File.WriteAllLines(shortcut.FullPath, lines.ToArray());

                Console.WriteLine($"Shortcut created: \"{shortcut.FullPath}\"");

                result = true;
            }

            return result;
        }

        /// <summary>The lines of a keycut's .bat file. Touches no files, so tests can pin the output.</summary>
        public static List<string> BuildShortcutLines(Shortcut shortcut, string description = "")
        {
            // Create lines with comments and command based on type (file or folder)
            var shortcutTypeLower = shortcut.Type.ToString().ToLower();

            var lines = new List<string>
            {
                "@ECHO OFF",
                $"REM {AppName} {Version}",
                $"REM <shortcut>{shortcut.FullPath}</shortcut>",
                $"REM <type>{shortcutTypeLower}</type>",
                $"REM <target>{shortcut.Target}</target>",
                $"REM <description>{SanitizeDescription(description)}</description>",
            };

            lines.Add(BuildCommand(shortcut));
            lines.Add("EXIT");

            return lines;
        }

        /// <summary>The launch line. Every <see cref="ShortcutType"/> has a case, so a new one can't silently emit nothing.</summary>
        private static string BuildCommand(Shortcut shortcut)
        {
            // START: "" = Title (empty) of console window
            //  /B = don't create a new window
            //  "{0}" = command/program
            //  "{1}" = parameters
            var start = "START \"\" /B \"{0}\"";
            var startWithArgument = $"{start} \"{{1}}\"";

            if (shortcut.OpenWithApp)
            {
                return string.Format(startWithArgument, shortcut.OpenWithAppPath, shortcut.Target);
            }

            // No discard arm, so a ShortcutType added later without a case here is a build error (CS8509). CS8524
            // only covers integers outside the enum, which throw SwitchExpressionException.
#pragma warning disable CS8524
            return shortcut.Type switch
            {
                ShortcutType.Url => $"START {Shortcut.SanitizeBatEscapeCharacters(shortcut.Target)}",
                ShortcutType.File => string.Format(start, shortcut.Target),
                ShortcutType.HostsFile => string.Format(startWithArgument, @"%windir%\system32\notepad.exe", shortcut.Target),
                // START, so cmd doesn't wait for explorer.exe to hand off to the running shell: called directly, the console
                // stayed up for about 600 ms, and with START it is gone in about 70 ms, like an empty .bat (#160)
                ShortcutType.Folder or ShortcutType.CLSIDKey => string.Format(startWithArgument, ExplorerProgram, shortcut.Target),
                // cmd /C runs the whole line, arguments included, which START "" /B "<line>" would treat as one program name
                ShortcutType.Command => $"START \"\" /B cmd /C \"{shortcut.Target}\"",
                ShortcutType.Unknown => throw new InvalidOperationException($"Can't write a keycut of type {shortcut.Type}."),
            };
#pragma warning restore CS8524
        }

        #region Static Methods

        /// <summary>
        /// A description as it goes on its <c>REM</c> line: on one line, with every <c>%</c> doubled. cmd expands <c>%</c>
        /// before it reads <c>REM</c>, so a <c>%~</c> (as in <c>%~dp0</c>) stops the whole .bat with a syntax error and the
        /// keycut never launches; <c>%%~</c> is inert. Folding newlines also keeps a line ending in <c>^</c> from joining the
        /// next line, which the closing tag does anyway. <see cref="ShortcutFile.ParseDescription"/> halves the <c>%%</c> again.
        /// The other special characters (<c>&amp; | &lt; &gt; ^</c>, a lone <c>%</c>, <c>%1</c>, <c>%*</c>) are inert on a REM line.
        /// </summary>
        internal static string SanitizeDescription(string description)
        {
            return (description ?? "").Replace("\r", " ").Replace("\n", " ").Trim().Replace("%", "%%");
        }

        #endregion Static Methods
    }
}
