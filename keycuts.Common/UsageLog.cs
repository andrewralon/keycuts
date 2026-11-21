using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>How often a keycut has run and when it last did (#187), as its <see cref="UsageLog"/> file says.</summary>
    public sealed record UsageRecord(int Runs, DateTime LastRun);

    /// <summary>A keycut's usage file as it was before a delete, so an Undo can put the count back. See <see cref="UsageLog.Remove"/>.</summary>
    public sealed record UsageFile(byte[] Contents, DateTime LastWrite);

    /// <summary>
    /// Run counts (#187). keycuts isn't running when a keycut runs: cmd runs the .bat, so the .bat records the run itself
    /// with <see cref="BatLines"/>, which add one line to <c>%LOCALAPPDATA%\keycuts\usage\&lt;name&gt;.runs</c> after the
    /// launch line. The count is the number of lines and the last run is the file's last write time, so cmd parses
    /// nothing (no locale-dependent <c>%DATE%</c>), two runs at once both count, and any garbage in the file still counts.
    /// <c>%~n0</c> is the .bat's own name, so a keycut renamed in Explorer counts under its new name with no rewrite.
    /// Per user and off PATH, so a shared or synced keycuts folder isn't touched by runs.
    /// Every member is best effort: usage is a nicety, and nothing here throws for a file it can't read or write.
    /// </summary>
    public class UsageLog
    {
        /// <summary>Where the .bat writes, as cmd expands it; <see cref="DefaultFolder"/> is the same place for this process.</summary>
        public const string BatFolder = @"%LOCALAPPDATA%\keycuts\usage";

        public const string Extension = ".runs";

        /// <summary>
        /// The lines every emitted keycut has between its launch line and <c>EXIT</c>, so they never delay the launch. Pure
        /// cmd, so they keep working with keycuts uninstalled. The folder is made if it's gone, and a folder or file that
        /// can't be written only means the run isn't counted: both lines send their errors to NUL. The parentheses on the
        /// second make <c>2&gt;NUL</c> cover the <c>&gt;&gt;</c> redirection itself, which fails before a <c>2&gt;NUL</c> after it applies.
        /// The .bat exits with the launch's code, as it did before counting: ECHO and a failed redirection leave ERRORLEVEL
        /// alone, and MD sets it (0 or 1), so after MD a child cmd exits with the code from before it. <c>%ERRORLEVEL%</c> is
        /// expanded when cmd reads the IF, before MD runs. That cmd only starts when the folder is missing: the first run.
        /// </summary>
        public static readonly IReadOnlyList<string> BatLines = new[]
        {
            $"IF NOT EXIST \"{BatFolder}\\\" (MD \"{BatFolder}\" 2>NUL & \"%ComSpec%\" /C EXIT %ERRORLEVEL%)",
            $"(>>\"{BatFolder}\\%~n0{Extension}\" ECHO 1) 2>NUL"
        };

        /// <summary>
        /// The macOS keycut's twin of <see cref="BatLines"/>, after its launch line: keep the launch's status, add a line to
        /// <c>&lt;name&gt;.runs</c> in the same folder as <see cref="DefaultFolder"/> (<c>${0##*/}</c> is the script's own name, so
        /// a rename in Finder keeps counting), silently, then exit with that status.
        /// </summary>
        public static readonly IReadOnlyList<string> ShLines = new[]
        {
            "s=$?",
            "u=\"$HOME/Library/Application Support/keycuts/usage\"; { mkdir -p \"$u\" && echo 1 >>\"$u/${0##*/}" + Extension + "\"; } 2>/dev/null",
            "exit $s"
        };

        /// <summary>The per-user folder the .bat writes to: <see cref="BatFolder"/> expanded.</summary>
        public static readonly string DefaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "keycuts", "usage");

        public UsageLog(string folder)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public string Folder { get; }

        /// <summary>This user's log. Never in tests: they pass a temp folder to the constructor.</summary>
        public static UsageLog CreateDefault()
        {
            return new UsageLog(DefaultFolder);
        }

        #region Reading

        /// <summary>True if <paramref name="lines"/> hold the line that counts a run, wherever it is: the keycut counts its runs.</summary>
        public static bool IsCounted(IEnumerable<string> lines)
        {
            return lines.Any(IsUsageLine);
        }

        /// <summary>True for a line exactly as <see cref="BatLines"/> or <see cref="ShLines"/> writes it (surrounding blanks aside).</summary>
        public static bool IsUsageLine(string line)
        {
            var trimmed = (line ?? "").Trim();

            return BatLines.Concat(ShLines).Any(usage => string.Equals(trimmed, usage, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The record for the keycut named <paramref name="name"/> (any case), or null if it has none.</summary>
        public UsageRecord Read(string name)
        {
            return ReadFile(PathFor(name));
        }

        /// <summary>Every record in the folder, by keycut name (any case). Empty if the folder is missing or can't be read.</summary>
        public IReadOnlyDictionary<string, UsageRecord> ReadAll()
        {
            var records = new Dictionary<string, UsageRecord>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!Directory.Exists(Folder))
                {
                    return records;
                }

                foreach (var file in Directory.EnumerateFiles(Folder, "*" + Extension))
                {
                    var record = ReadFile(file);

                    if (record != null)
                    {
                        records[Path.GetFileNameWithoutExtension(file)] = record;
                    }
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't read run counts in \"{Folder}\": {ex.Message}");
            }

            return records;
        }

        /// <summary>When counting started on this machine: when the folder was made. Null if it doesn't exist yet.</summary>
        public DateTime? CountingSince()
        {
            try
            {
                return Directory.Exists(Folder) ? Directory.GetCreationTime(Folder) : null;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return null;
            }
        }

        /// <summary>The number of runs in a file's bytes: its lines, a last line without a line break included.</summary>
        public static int CountRuns(byte[] contents)
        {
            var runs = 0;

            foreach (var b in contents)
            {
                if (b == (byte)'\n')
                {
                    runs++;
                }
            }

            if (contents.Length > 0 && contents[contents.Length - 1] != (byte)'\n')
            {
                runs++;
            }

            return runs;
        }

        private static UsageRecord ReadFile(string path)
        {
            try
            {
                if (path == null || !File.Exists(path))
                {
                    return null;
                }

                // Shared, since a keycut may be appending to it right now
                byte[] contents;

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    contents = memory.ToArray();
                }

                return new UsageRecord(CountRuns(contents), File.GetLastWriteTime(path));
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't read run count \"{path}\": {ex.Message}");
                return null;
            }
        }

        #endregion Reading

        #region Writing

        /// <summary>Makes the folder, so a keycut's first run is counted and <see cref="CountingSince"/> has a date.</summary>
        public void EnsureFolder()
        {
            try
            {
                Directory.CreateDirectory(Folder);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't make \"{Folder}\": {ex.Message}");
            }
        }

        /// <summary>
        /// Moves a renamed keycut's record to its new name, so its history follows it. Whatever the new name already had
        /// is replaced: the rename was only allowed because no keycut has that name, so it's left from a deleted one.
        /// </summary>
        public void Move(string oldName, string newName)
        {
            var from = PathFor(oldName);
            var to = PathFor(newName);

            if (from == null || to == null || string.Equals(from, to, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                if (!File.Exists(from))
                {
                    // Nothing to carry over, and a record the new name had belongs to some other keycut
                    Forget(newName);
                    return;
                }

                // A change of case only is the same file, which Move renames in place
                File.Move(from, to, !string.Equals(from, to, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't move run count \"{from}\" to \"{to}\": {ex.Message}");
            }
        }

        /// <summary>Deletes the keycut's record and returns it as it was, for <see cref="Restore"/>; null if it had none or it can't be read.</summary>
        public UsageFile Remove(string name)
        {
            var path = PathFor(name);

            try
            {
                if (path == null || !File.Exists(path))
                {
                    return null;
                }

                var kept = new UsageFile(File.ReadAllBytes(path), File.GetLastWriteTime(path));
                File.Delete(path);

                return kept;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't remove run count \"{path}\": {ex.Message}");
                return null;
            }
        }

        /// <summary>Puts back what <see cref="Remove"/> returned, with its last run time. Runs since the delete are lost (rare, and fine).</summary>
        public void Restore(string name, UsageFile usage)
        {
            var path = PathFor(name);

            if (usage == null || path == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(path, usage.Contents);
                File.SetLastWriteTime(path, usage.LastWrite);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't restore run count \"{path}\": {ex.Message}");
            }
        }

        /// <summary>Deletes the record for a name, so a new keycut doesn't start with the count of an old one deleted outside keycuts.</summary>
        public void Forget(string name)
        {
            var path = PathFor(name);

            try
            {
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.WriteLine($"keycuts: couldn't forget run count \"{path}\": {ex.Message}");
            }
        }

        #endregion Writing

        /// <summary>The record's path for a keycut name; null for a name that can't be a file name.</summary>
        private string PathFor(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }

            return Path.Combine(Folder, name + Extension);
        }

        private static bool IsFileError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException ||
                ex is System.Security.SecurityException;
        }
    }
}
