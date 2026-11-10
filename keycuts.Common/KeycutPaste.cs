using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// <c>keycuts paste &lt;name&gt;</c> (#97, formerly <c>keypaste</c>): finds a keycut or text file by name and returns the
    /// text to put on the clipboard. A keycut's .bat gives its destination; any other file gives its contents.
    /// </summary>
    public static class KeycutPaste
    {
        /// <summary>Extensions tried after the name as given, when the name doesn't already end in one of them.</summary>
        public static readonly string[] Extensions = { ".txt", ShortcutFile.Extension };

        /// <summary>
        /// The folders searched, in order: the working folder, the keycuts folder, then the folder keycuts.exe is in.
        /// Reads the registry, so tests pass their own.
        /// </summary>
        public static IReadOnlyList<string> GetDefaultFolders()
        {
            return GetDefaultFolders(DebugOverrides.GetOutputFolder(), () => RegistryStuff.GetOutputFolder(Runner.DefaultOutputFolder));
        }

        /// <summary>
        /// <see cref="GetDefaultFolders()"/> with its inputs passed in. The keycuts folder is the Debug
        /// <c>KEYCUTS_OUTPUT_FOLDER</c> override when there is one, as for creating and the manager, and only otherwise
        /// the configured folder (<paramref name="getConfiguredFolder"/>, which isn't called then).
        /// </summary>
        public static IReadOnlyList<string> GetDefaultFolders(string overrideFolder, Func<string> getConfiguredFolder)
        {
            return new[]
            {
                Environment.CurrentDirectory,
                overrideFolder ?? getConfiguredFolder(),
                AppContext.BaseDirectory
            };
        }

        /// <summary>
        /// Every path tried for <paramref name="name"/>, in order: in each folder, the name as given, then with each of
        /// <see cref="Extensions"/> unless it already ends in one. A full path is tried as is, and only once.
        /// </summary>
        public static IEnumerable<string> Candidates(string name, IEnumerable<string> folders)
        {
            var names = new List<string> { name };
            var extension = Path.GetExtension(name);

            if (!Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                names.AddRange(Extensions.Select(e => name + e));
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in folders.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                foreach (var candidate in names)
                {
                    string path;

                    try
                    {
                        path = Path.GetFullPath(Path.Combine(folder, candidate));
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                    {
                        continue;
                    }

                    if (seen.Add(path))
                    {
                        yield return path;
                    }
                }
            }
        }

        /// <summary>The first of <see cref="Candidates"/> that exists, or null.</summary>
        public static string TryResolve(string name, IEnumerable<string> folders, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return Candidates(name.Trim(), folders).FirstOrDefault(fileExists);
        }

        /// <summary>
        /// The text to copy from <paramref name="path"/>: a keycut's destination (from its REM line or launch command), or
        /// a text file's contents. Null for a .bat that isn't a keycut, so there's nothing to copy.
        /// </summary>
        /// <param name="expandVariables">
        /// Expands <c>%NAME%</c> variables (<see cref="Environment.ExpandEnvironmentVariables"/>; tests pass their own). Used
        /// only for a file, folder or hosts keycut, the types #31 saves with variables (#108), so the copied path works when
        /// pasted into Explorer or a terminal. A URL or command, and a text file's contents, are copied as written: a
        /// <c>%20</c> in a URL or a <c>%VAR%</c> in a snippet isn't a variable to expand.
        /// </param>
        /// <exception cref="IOException">The file can't be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The file can't be read.</exception>
        public static string GetText(string path, Func<string, string[]> readLines, Func<string, string> readText, Func<string, string> expandVariables)
        {
            if (string.Equals(Path.GetExtension(path), ShortcutFile.Extension, StringComparison.OrdinalIgnoreCase))
            {
                var file = new ShortcutFile(path, readLines(path));
                var destination = file.Destination;

                if (string.IsNullOrEmpty(destination))
                {
                    return null;
                }

                return PathVariables.AppliesTo(file.Type) ? expandVariables(destination) : destination;
            }

            return readText(path);
        }

        /// <summary>
        /// Runs <c>keycuts paste</c>: resolves the name, copies its text with <paramref name="setClipboard"/>, and writes the
        /// copied text to <paramref name="output"/> (so it can be used in a script too) or the reason it failed to <paramref name="error"/>.
        /// </summary>
        public static ExitCode Run(
            string name,
            IEnumerable<string> folders,
            Func<string, bool> fileExists,
            Func<string, string[]> readLines,
            Func<string, string> readText,
            Func<string, string> expandVariables,
            Action<string> setClipboard,
            TextWriter output,
            TextWriter error)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                error.WriteLine("Name a keycut or text file to copy, for example: keycuts paste db");
                return ExitCode.BadArguments;
            }

            var folderList = folders.ToList();
            var path = TryResolve(name, folderList, fileExists);

            if (path == null)
            {
                error.WriteLine($"Can't find \"{name.Trim()}\". Looked in:");

                foreach (var folder in folderList.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    error.WriteLine("  " + folder);
                }

                return ExitCode.CannotFindFile;
            }

            string text;

            try
            {
                text = GetText(path, readLines, readText, expandVariables);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error.WriteLine($"Can't read \"{path}\": {ex.Message}");
                return ExitCode.CannotOpenFile;
            }

            if (text == null)
            {
                error.WriteLine($"\"{path}\" isn't a keycut, so there's no destination to copy.");
                return ExitCode.CannotOpenFile;
            }

            try
            {
                setClipboard(text);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                error.WriteLine("Can't copy to the clipboard: " + ex.Message);
                return ExitCode.CannotSetClipboard;
            }

            output.WriteLine(text);
            return ExitCode.Success;
        }
    }
}
