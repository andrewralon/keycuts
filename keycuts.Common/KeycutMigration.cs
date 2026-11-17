using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace keycuts.Common
{
    /// <summary>What <see cref="KeycutMigration.Run"/> did to a folder.</summary>
    public sealed class MigrationResult
    {
        /// <summary>The keycuts rewritten, by file name.</summary>
        public List<string> Migrated { get; } = new List<string>();

        /// <summary>How many had their <c>REM &lt;destination&gt;</c> line renamed to <c>&lt;target&gt;</c> (#196).</summary>
        public int TagsUpdated { get; set; }

        /// <summary>How many Folder or CLSID launch lines got <c>START "" /B</c> in front (#207).</summary>
        public int LaunchLinesUpdated { get; set; }

        /// <summary>Where the originals were copied before any was changed; null when nothing changed.</summary>
        public string BackupFolder { get; set; }

        /// <summary>Files that need it but couldn't be done now (locked, read-only, changed while read): the next start tries again.</summary>
        public List<string> Skipped { get; } = new List<string>();

        /// <summary>Files that aren't valid UTF-8. They are never rewritten, so they don't stop the folder counting as done.</summary>
        public List<string> NotUtf8 { get; } = new List<string>();

        /// <summary>True when nothing is left for a later start to do.</summary>
        public bool IsComplete => Skipped.Count == 0;
    }

    /// <summary>
    /// Brings keycuts written by older versions to the current format, in one pass over the folder (#196, #207):
    /// <list type="bullet">
    /// <item><c>REM &lt;destination&gt;…&lt;/destination&gt;</c> becomes <c>REM &lt;target&gt;…&lt;/target&gt;</c> (#195 renamed it; the parser
    /// still reads both).</item>
    /// <item>A Folder or CLSID launch line exactly in the old emitter's form, <c>"%SystemRoot%\explorer.exe" "&lt;target&gt;"</c>, gets
    /// <c>START "" /B </c> in front, so cmd doesn't wait for Explorer and the console goes in ~70 ms instead of ~600 (#160). A
    /// commented-out copy, an unquoted argument or anything else isn't touched.</item>
    /// </list>
    /// Nothing else in a file changes: only those lines are edited, on the text decoded as strict UTF-8 with the file's own line
    /// breaks and BOM, so a hand-edited keycut keeps its edits. Every file that will change is copied to
    /// <c>&lt;folder&gt;\.keycuts-backup\&lt;yyyyMMdd-HHmmss&gt;\</c> first (off PATH, since PATH isn't recursive; a backup folder is never
    /// overwritten). Each is written through a temp file and <c>File.Replace</c>, so its permissions, streams and creation time stay,
    /// and its last write and access times are put back, so the Manager doesn't show 60 keycuts as edited today. A second run finds
    /// nothing to do. PATH and the registry are never touched; the caller records a folder as done when <see cref="MigrationResult.IsComplete"/>.
    /// </summary>
    public static class KeycutMigration
    {
        public const string BackupFolderName = ".keycuts-backup";

        private static readonly Regex DestinationLine = new Regex(
            @"^(?<pre>\s*REM\s+<)destination(?<mid>>.*</)destination(?<post>>\s*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // The old emitter's line and nothing else: the program, one space, one quoted argument, maybe trailing blanks
        private static readonly Regex OldExplorerLine = new Regex(
            @"^""%SystemRoot%\\explorer\.exe"" ""[^""]*""\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const string StartPrefix = "START \"\" /B ";

        /// <summary>The lines of a keycut as it should be now, and what changed: null when nothing needs to.</summary>
        public static (string Text, bool Tag, bool Launch)? Upgrade(string text)
        {
            var spans = KeycutRepository.ScanLines(text);
            var updated = new System.Text.StringBuilder(text.Length + 16);
            var position = 0;
            bool tag = false, launch = false;

            foreach (var (start, length) in spans)
            {
                var line = text.Substring(start, length);
                string replacement = null;

                var match = DestinationLine.Match(line);
                if (match.Success)
                {
                    replacement = match.Groups["pre"].Value + "target" + match.Groups["mid"].Value + "target" + match.Groups["post"].Value;
                    tag = true;
                }
                else if (OldExplorerLine.IsMatch(line))
                {
                    replacement = StartPrefix + line;
                    launch = true;
                }

                if (replacement != null)
                {
                    updated.Append(text, position, start - position).Append(replacement);
                    position = start + length;
                }
            }

            if (!tag && !launch)
            {
                return null;
            }

            updated.Append(text, position, text.Length - position);
            return (updated.ToString(), tag, launch);
        }

        /// <summary>
        /// <see cref="Run"/> unless <paramref name="isDone"/> says the folder was done, then <paramref name="markDone"/> once it is
        /// complete and the folder exists. Without them (the Debug folder override, which never writes the registry) it runs every
        /// time, and a second run finds nothing to do. Returns an empty result for a folder already done.
        /// </summary>
        public static MigrationResult RunOnce(string folder, DateTime now, Func<string, bool> isDone, Action<string> markDone)
        {
            if (isDone != null && isDone(folder))
            {
                return new MigrationResult();
            }

            var result = Run(folder, now);

            if (markDone != null && result.IsComplete && !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                markDone(folder);
            }

            return result;
        }

        /// <summary>Upgrades every keycut in <paramref name="folder"/> that needs it; <paramref name="now"/> names the backup folder.</summary>
        public static MigrationResult Run(string folder, DateTime now)
        {
            var result = new MigrationResult();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return result;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*" + ShortcutFile.Extension);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                result.Skipped.Add(folder);
                return result;
            }

            // Work out every change first, so the backup holds all of them before any file is touched
            var plans = new List<Plan>();
            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var plan = PlanFor(file);
                    if (plan == null)
                    {
                        continue;
                    }

                    if (plan.NotUtf8)
                    {
                        result.NotUtf8.Add(Path.GetFileName(file));
                        continue;
                    }

                    plans.Add(plan);
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    result.Skipped.Add(Path.GetFileName(file));
                }
            }

            if (plans.Count == 0)
            {
                return result;
            }

            string backup;
            try
            {
                backup = MakeBackupFolder(folder, now);
                foreach (var plan in plans)
                {
                    // Copy keeps the bytes and the last write time
                    File.Copy(plan.Path, Path.Combine(backup, Path.GetFileName(plan.Path)), false);
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                // No backup, no changes: every file waits for the next start
                Debug.WriteLine($"keycuts: couldn't back up \"{folder}\" before updating it: {ex.Message}");
                result.Skipped.AddRange(plans.Select(p => Path.GetFileName(p.Path)));
                return result;
            }

            result.BackupFolder = backup;

            foreach (var plan in plans)
            {
                var name = Path.GetFileName(plan.Path);
                try
                {
                    if (Write(plan))
                    {
                        result.Migrated.Add(name);
                        result.TagsUpdated += plan.Tag ? 1 : 0;
                        result.LaunchLinesUpdated += plan.Launch ? 1 : 0;
                    }
                    else
                    {
                        result.Skipped.Add(name);
                    }
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    Debug.WriteLine($"keycuts: couldn't update \"{plan.Path}\": {ex.Message}");
                    result.Skipped.Add(name);
                }
            }

            return result;
        }

        private sealed class Plan
        {
            public string Path;
            public byte[] Original;
            public byte[] Updated;
            public bool Tag;
            public bool Launch;
            public bool NotUtf8;
        }

        private static Plan PlanFor(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var hasBom = bytes.AsSpan().StartsWith(preamble);
            var start = hasBom ? preamble.Length : 0;
            var strict = new System.Text.UTF8Encoding(false, true);
            string text;

            try
            {
                text = strict.GetString(bytes, start, bytes.Length - start);
            }
            catch (System.Text.DecoderFallbackException)
            {
                // Written back as UTF-8, a byte that isn't valid would be lost: such a file is left as it is, for good. Only one that
                // needed a change is worth reporting.
                var latin = System.Text.Encoding.Latin1.GetString(bytes);
                return Upgrade(latin) == null ? null : new Plan { Path = path, NotUtf8 = true };
            }

            var upgraded = Upgrade(text);
            if (upgraded == null)
            {
                return null;
            }

            var body = strict.GetBytes(upgraded.Value.Text);
            return new Plan
            {
                Path = path,
                Original = bytes,
                Updated = hasBom ? preamble.Concat(body).ToArray() : body,
                Tag = upgraded.Value.Tag,
                Launch = upgraded.Value.Launch
            };
        }

        /// <summary>Writes the new bytes unless the file changed since it was read; keeps its times. False when it was skipped.</summary>
        private static bool Write(Plan plan)
        {
            if ((File.GetAttributes(plan.Path) & FileAttributes.ReadOnly) != 0)
            {
                return false;
            }

            if (!File.ReadAllBytes(plan.Path).AsSpan().SequenceEqual(plan.Original))
            {
                return false;
            }

            var written = File.GetLastWriteTimeUtc(plan.Path);
            var accessed = File.GetLastAccessTimeUtc(plan.Path);

            KeycutRepository.ReplaceFileContent(plan.Path, plan.Updated);

            File.SetLastWriteTimeUtc(plan.Path, written);
            File.SetLastAccessTimeUtc(plan.Path, accessed);
            return true;
        }

        /// <summary>A new, hidden <c>.keycuts-backup\&lt;yyyyMMdd-HHmmss&gt;</c> in the folder; never one that already exists.</summary>
        private static string MakeBackupFolder(string folder, DateTime now)
        {
            var root = Path.Combine(folder, BackupFolderName);
            var created = !Directory.Exists(root);
            Directory.CreateDirectory(root);

            if (created)
            {
                try
                {
                    File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.Hidden);
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    // Shown, then; it still works
                }
            }

            var stamp = now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            for (var i = 1; ; i++)
            {
                var candidate = Path.Combine(root, i == 1 ? stamp : stamp + "-" + i);
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                {
                    Directory.CreateDirectory(candidate);
                    return candidate;
                }
            }
        }

        private static bool IsFileError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException ||
                ex is System.Security.SecurityException;
        }
    }
}
