using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// What a new keycut's name would run into (#153): an existing keycut it would replace, and other commands with the
    /// same name (<see cref="NameConflicts"/>). Each is a warning to confirm, not an error.
    /// </summary>
    public class NameCheck
    {
        public NameCheck(string name, Keycut existing, IReadOnlyList<NameConflict> conflicts)
        {
            Name = name;
            Existing = existing;
            Conflicts = conflicts ?? Array.Empty<NameConflict>();
        }

        /// <summary>The name that was checked, trimmed.</summary>
        public string Name { get; }

        /// <summary>
        /// The keycut with this name that creating would replace, or null. Also null when "Ask before replacing a keycut"
        /// is off, since replacing then needs no confirmation.
        /// </summary>
        public Keycut Existing { get; }

        public IReadOnlyList<NameConflict> Conflicts { get; }

        /// <summary>The conflict to show first (<see cref="NameConflicts.GetMain"/>), or null.</summary>
        public NameConflict MainConflict => NameConflicts.GetMain(Conflicts);

        public bool HasWarnings => Existing != null || Conflicts.Count > 0;

        public static NameCheck None(string name)
        {
            return new NameCheck(name, null, null);
        }

        /// <summary>
        /// Checks <paramref name="name"/> for a new keycut in <paramref name="outputFolder"/>. Name conflicts are always
        /// reported; an existing keycut only when <paramref name="askBeforeReplacing"/> is on.
        /// </summary>
        public static NameCheck Run(string name, string outputFolder, bool askBeforeReplacing, NameConflictEnvironment environment)
        {
            name = name?.Trim() ?? string.Empty;

            var existing = askBeforeReplacing && !string.IsNullOrWhiteSpace(outputFolder)
                ? new KeycutRepository(outputFolder).Find(name)
                : null;

            var conflicts = NameConflicts.Find(name, outputFolder, environment).Select(AsKeycutIfOne).ToList();

            return new NameCheck(name, existing, conflicts);
        }

        // A conflict that is a keycut (another keycuts folder on PATH) is described as one, with what it opens
        private static NameConflict AsKeycutIfOne(NameConflict conflict)
        {
            if (!conflict.Path.EndsWith(ShortcutFile.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return conflict;
            }

            // The repository's parser is lenient (a headerless launcher is listed in the manager), but an unrelated script that happens
            // to have a START line is a program here, not a keycut
            var keycut = HasKeycutsHeader(conflict.Path) ? KeycutRepository.TryParse(conflict.Path) : null;

            return keycut == null ? conflict : new NameConflict(conflict.Path, conflict.Effect, conflict.Source, keycut);
        }

        // Every version of keycuts writes "REM keycuts <version>" among the first lines
        private static bool HasKeycutsHeader(string path)
        {
            try
            {
                return File.ReadLines(path).Take(5).Any(line => line.TrimStart().StartsWith("REM keycuts ", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
