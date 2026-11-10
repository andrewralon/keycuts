using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>What another command with a keycut's name does to the keycut, or the keycut to it.</summary>
    public enum NameConflictEffect
    {
        /// <summary>Found first, so the keycut never runs from Win+R.</summary>
        Hidden,

        /// <summary>Found after the keycut, so Win+R opens the keycut instead of it.</summary>
        Shadows
    }

    /// <summary>Where the other command was found, in the order <c>ShellExecuteEx</c> searches.</summary>
    public enum NameConflictSource
    {
        WindowsFolder,
        SystemFolder,
        PathBefore,
        OutputFolder,
        PathAfter,
        AppPaths
    }

    /// <summary>A program, script or other file that a keycut's name already resolves to.</summary>
    public class NameConflict
    {
        public NameConflict(string path, NameConflictEffect effect, NameConflictSource source)
        {
            Path = path;
            Effect = effect;
            Source = source;
        }

        /// <summary>The full path of what the name opens besides the keycut.</summary>
        public string Path { get; }

        public NameConflictEffect Effect { get; }

        public NameConflictSource Source { get; }

        public override string ToString()
        {
            return $"{Effect} {Source} {Path}";
        }
    }

    /// <summary>
    /// The inputs of <see cref="NameConflicts.Find"/>: the folders, PATH, PATHEXT, App Paths and file checks. Tests set
    /// all of them, so they never read the registry or the real disk; <see cref="GetDefault"/> reads this machine.
    /// </summary>
    public class NameConflictEnvironment
    {
        public string WindowsFolder { get; set; }

        public string SystemFolder { get; set; }

        /// <summary>The machine PATH, which Explorer puts before the user PATH. May hold %VARS%.</summary>
        public string MachinePath { get; set; }

        /// <summary>The user PATH as stored in the registry. May hold %VARS%.</summary>
        public string UserPath { get; set; }

        /// <summary>Null or empty uses <see cref="NameConflicts.DefaultPathExt"/>.</summary>
        public string PathExt { get; set; }

        public Func<string, string> ExpandVariables { get; set; } = value => value;

        public Func<string, bool> FileExists { get; set; } = _ => false;

        /// <summary>
        /// The program registered under App Paths for a file name such as <c>winword.exe</c> (HKCU first, then HKLM),
        /// or null.
        /// </summary>
        public Func<string, string> FindAppPath { get; set; } = _ => null;

        private const string EnvironmentKey = "Environment";

        private const string AppPathsKey = @"Software\Microsoft\Windows\CurrentVersion\App Paths\";

        /// <summary>
        /// This machine, as Win+R sees it: PATH and PATHEXT from the registry (this process's own environment can be
        /// stale), and App Paths. Anything that can't be read counts as empty.
        /// </summary>
        public static NameConflictEnvironment GetDefault()
        {
            return new NameConflictEnvironment
            {
                WindowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                SystemFolder = Environment.SystemDirectory,
                MachinePath = ReadMachineVariable("PATH"),
                UserPath = ReadUserVariable("Path"),
                PathExt = NullIfEmpty(ReadUserVariable("PATHEXT")) ?? ReadMachineVariable("PATHEXT"),
                ExpandVariables = Environment.ExpandEnvironmentVariables,
                FileExists = SafeFileExists,
                FindAppPath = FindRegisteredAppPath
            };
        }

        private static string ReadUserVariable(string name)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(EnvironmentKey);

                return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return null;
            }
        }

        private static string ReadMachineVariable(string name)
        {
            try
            {
                return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return null;
            }
        }

        private static string FindRegisteredAppPath(string fileName)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey(AppPathsKey + fileName);

                    if (key?.GetValue(null) is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
                catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
                {
                    // Try the next hive
                }
            }

            return null;
        }

        private static bool SafeFileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>
    /// Finds what else a keycut's name opens from Win+R (#153). A keycut is a .bat in a PATH folder, so its name competes
    /// with every command Windows finds by name. <c>ShellExecuteEx</c> looks in the Windows folder, System32, each PATH
    /// folder (machine, then user), and last in App Paths; in each folder it tries the PATHEXT extensions in order.
    /// Whatever comes before the keycut hides it, and whatever comes after is shadowed by it. The keycut's own .bat in the
    /// output folder isn't a conflict (replacing it is the overwrite check's job), but a keycut in another folder is.
    /// cmd built-ins (dir, start) are out of scope: they only matter in a cmd prompt, not in Win+R.
    /// </summary>
    public static class NameConflicts
    {
        /// <summary>Windows' default PATHEXT, used when none can be read.</summary>
        public const string DefaultPathExt = ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC";

        /// <summary>
        /// Everything <paramref name="name"/> resolves to besides the keycut <paramref name="name"/>.bat in
        /// <paramref name="outputFolder"/>, in search order. An output folder that isn't on PATH is treated as the last
        /// PATH entry, which is where adding it puts it. An empty or unusable name has no conflicts.
        /// </summary>
        public static IReadOnlyList<NameConflict> Find(string name, string outputFolder, NameConflictEnvironment environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            name = name?.Trim();

            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return Array.Empty<NameConflict>();
            }

            var finder = new Finder(name, environment);

            finder.CheckFolder(environment.WindowsFolder, NameConflictEffect.Hidden, NameConflictSource.WindowsFolder);
            finder.CheckFolder(environment.SystemFolder, NameConflictEffect.Hidden, NameConflictSource.SystemFolder);

            var entries = SplitPath(environment.MachinePath, environment)
                .Concat(SplitPath(environment.UserPath, environment))
                .ToList();
            var outputIndex = entries.FindIndex(entry => IsSameFolder(entry, outputFolder));

            for (var i = 0; i < entries.Count; i++)
            {
                if (i == outputIndex)
                {
                    finder.CheckOutputFolder(entries[i]);
                }
                else if (outputIndex < 0 || i < outputIndex)
                {
                    finder.CheckFolder(entries[i], NameConflictEffect.Hidden, NameConflictSource.PathBefore);
                }
                else
                {
                    finder.CheckFolder(entries[i], NameConflictEffect.Shadows, NameConflictSource.PathAfter);
                }
            }

            if (outputIndex < 0 && IsUsableFolder(outputFolder))
            {
                finder.CheckOutputFolder(outputFolder);
            }

            finder.CheckAppPaths();

            return finder.Results;
        }

        /// <summary>The conflict that matters most: the first one that hides the keycut, else the first it shadows.</summary>
        public static NameConflict GetMain(IReadOnlyList<NameConflict> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                return null;
            }

            return conflicts.FirstOrDefault(c => c.Effect == NameConflictEffect.Hidden) ?? conflicts[0];
        }

        /// <summary>The PATHEXT extensions in order, upper case with a leading dot, without duplicates.</summary>
        public static IReadOnlyList<string> ParsePathExt(string pathExt)
        {
            var value = string.IsNullOrWhiteSpace(pathExt) ? DefaultPathExt : pathExt;

            return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ext => (ext.StartsWith(".") ? ext : "." + ext).ToUpperInvariant())
                .Distinct()
                .ToList();
        }

        private static IEnumerable<string> SplitPath(string pathValue, NameConflictEnvironment environment)
        {
            foreach (var raw in (pathValue ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var entry = environment.ExpandVariables(raw.Trim('"')) ?? string.Empty;

                if (IsUsableFolder(entry))
                {
                    yield return entry;
                }
            }
        }

        private static bool IsUsableFolder(string folder)
        {
            return !string.IsNullOrWhiteSpace(folder) &&
                folder.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
                Path.IsPathFullyQualified(folder);
        }

        internal static bool IsSameFolder(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
                string.Equals(Path.TrimEndingDirectorySeparator(left.Trim()), Path.TrimEndingDirectorySeparator(right.Trim()),
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Collects the matches, each file once.</summary>
        private sealed class Finder
        {
            private readonly string _name;

            private readonly NameConflictEnvironment _environment;

            private readonly IReadOnlyList<string> _extensions;

            private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            private readonly HashSet<string> _seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public Finder(string name, NameConflictEnvironment environment)
            {
                _name = name;
                _environment = environment;
                _extensions = ParsePathExt(environment.PathExt);
            }

            public List<NameConflict> Results { get; } = new List<NameConflict>();

            public void CheckFolder(string folder, NameConflictEffect effect, NameConflictSource source)
            {
                if (!StartFolder(folder))
                {
                    return;
                }

                foreach (var candidate in Candidates(folder))
                {
                    Add(candidate.Path, effect, source);
                }
            }

            /// <summary>
            /// The keycut's own folder: an extension tried before .bat (.com, .exe) hides the keycut, one tried after it
            /// is shadowed, and the keycut's own .bat is skipped.
            /// </summary>
            public void CheckOutputFolder(string folder)
            {
                if (!StartFolder(folder))
                {
                    return;
                }

                var batIndex = IndexOf(ShortcutFile.Extension);

                foreach (var candidate in Candidates(folder))
                {
                    if (candidate.Index == batIndex && candidate.Index >= 0 && candidate.IsAppended)
                    {
                        continue;
                    }

                    var hides = batIndex < 0 || candidate.Index < batIndex || !candidate.IsAppended;
                    Add(candidate.Path, hides ? NameConflictEffect.Hidden : NameConflictEffect.Shadows, NameConflictSource.OutputFolder);
                }
            }

            /// <summary>App Paths registers programs by file name, so <c>winword</c> looks up <c>winword.exe</c>.</summary>
            public void CheckAppPaths()
            {
                var fileName = _name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? _name : _name + ".exe";
                var registered = _environment.FindAppPath(fileName);

                if (string.IsNullOrWhiteSpace(registered))
                {
                    return;
                }

                var path = _environment.ExpandVariables(registered.Trim().Trim('"')) ?? string.Empty;

                if (path.Length > 0 && _environment.FileExists(path))
                {
                    Add(path, NameConflictEffect.Shadows, NameConflictSource.AppPaths);
                }
            }

            /// <summary>False for a folder that is missing, unusable, or already searched (it can be on PATH twice).</summary>
            private bool StartFolder(string folder)
            {
                return IsUsableFolder(folder) && _seenFolders.Add(Path.TrimEndingDirectorySeparator(folder.Trim()));
            }

            /// <summary>
            /// The files the name can mean in a folder: the name as typed when it already ends with a PATHEXT extension
            /// (<c>tool.exe</c>), then the name plus each extension in order.
            /// </summary>
            private IEnumerable<(string Path, int Index, bool IsAppended)> Candidates(string folder)
            {
                var ownIndex = IndexOf(Path.GetExtension(_name));

                if (ownIndex >= 0)
                {
                    var asTyped = Path.Combine(folder, _name);

                    if (_environment.FileExists(asTyped))
                    {
                        yield return (asTyped, ownIndex, false);
                    }
                }

                for (var i = 0; i < _extensions.Count; i++)
                {
                    var path = Path.Combine(folder, _name + _extensions[i].ToLowerInvariant());

                    if (_environment.FileExists(path))
                    {
                        yield return (path, i, true);
                    }
                }
            }

            private int IndexOf(string extension)
            {
                if (string.IsNullOrEmpty(extension))
                {
                    return -1;
                }

                for (var i = 0; i < _extensions.Count; i++)
                {
                    if (string.Equals(_extensions[i], extension, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return -1;
            }

            private void Add(string path, NameConflictEffect effect, NameConflictSource source)
            {
                if (_seen.Add(path))
                {
                    Results.Add(new NameConflict(path, effect, source));
                }
            }
        }
    }
}
