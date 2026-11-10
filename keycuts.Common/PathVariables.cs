using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>An environment variable whose value is a folder, used to save paths that work on any machine or account.</summary>
    public sealed class PathVariable
    {
        public PathVariable(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        /// <summary>The expanded folder, without a trailing separator.</summary>
        public string Value { get; }
    }

    /// <summary>
    /// Replaces a known folder at the start of a path with its environment variable (#31), so
    /// <c>C:\Users\you\Desktop</c> is saved as <c>%USERPROFILE%\Desktop</c>. The .bat expands it when it runs.
    /// </summary>
    public static class PathVariables
    {
        /// <summary>Checked first and in this order, so a tie (two names for the same folder) goes to the earlier one.</summary>
        public static readonly string[] KnownNames =
        {
            "LOCALAPPDATA",
            "APPDATA",
            "OneDrive",
            "OneDriveCommercial",
            "OneDriveConsumer",
            "USERPROFILE",
            "ProgramFiles(x86)",
            "ProgramFiles",
            "ProgramData",
            "SystemRoot",
        };

        // Never used: temp folders aren't keycut destinations, and these duplicate a known name or aren't full folders
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TEMP", "TMP", "windir", "ALLUSERSPROFILE", "HOMEDRIVE", "HOMEPATH", "SystemDrive", "Path", "PSModulePath"
        };

        /// <summary>Only folders and files are paths; the hosts file is a file too. URLs, commands and shell keys aren't.</summary>
        public static bool AppliesTo(ShortcutType type)
        {
            return type == ShortcutType.File || type == ShortcutType.Folder || type == ShortcutType.HostsFile;
        }

        /// <summary>
        /// The known variables, then custom ones from the user and machine environment whose value is an existing folder
        /// (a DROPBOX variable, say), sorted by name. Reads the environment and the registry, so tests pass their own list.
        /// </summary>
        public static IReadOnlyList<PathVariable> GetDefault()
        {
            var result = new List<PathVariable>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in KnownNames)
            {
                Add(result, seen, name, Environment.GetEnvironmentVariable(name), requireFolder: false);
            }

            foreach (var name in CustomNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                Add(result, seen, name, Environment.GetEnvironmentVariable(name), requireFolder: true);
            }

            return result;
        }

        /// <summary>
        /// Replaces the longest variable value that <paramref name="path"/> starts with (ending at a folder boundary) with
        /// <c>%NAME%</c>. Returns the path unchanged if none matches, or if it already starts with a variable.
        /// </summary>
        public static string Substitute(string path, IEnumerable<PathVariable> variables)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith("%", StringComparison.Ordinal) || variables == null)
            {
                return path;
            }

            PathVariable best = null;

            foreach (var variable in variables)
            {
                var value = variable.Value;
                var matches = path.StartsWith(value, StringComparison.OrdinalIgnoreCase) &&
                              (path.Length == value.Length || path[value.Length] == Path.DirectorySeparatorChar);

                if (matches && (best == null || value.Length > best.Value.Length))
                {
                    best = variable;
                }
            }

            return best == null ? path : "%" + best.Name + "%" + path.Substring(best.Value.Length);
        }

        private static void Add(List<PathVariable> result, HashSet<string> seen, string name, string value, bool requireFolder)
        {
            if (string.IsNullOrWhiteSpace(value) || Excluded.Contains(name) || !seen.Add(name))
            {
                return;
            }

            var folder = Path.TrimEndingDirectorySeparator(value.Trim());

            if (folder.IndexOf(';') >= 0 || !Path.IsPathFullyQualified(folder) || (requireFolder && !Directory.Exists(folder)))
            {
                return;
            }

            // The root of a drive ("C:\") would turn every path on it into a variable
            if (string.Equals(Path.GetPathRoot(folder), folder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            result.Add(new PathVariable(name, folder));
        }

        private static IEnumerable<string> CustomNames()
        {
            var names = new List<string>();

            foreach (var (root, subKey) in new[] { (Registry.CurrentUser, "Environment"), (Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment") })
            {
                try
                {
                    using var key = root.OpenSubKey(subKey);

                    if (key != null)
                    {
                        names.AddRange(key.GetValueNames());
                    }
                }
                catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
                {
                    // Custom variables are optional; the known ones still work
                }
            }

            return names.Where(n => !KnownNames.Contains(n, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
