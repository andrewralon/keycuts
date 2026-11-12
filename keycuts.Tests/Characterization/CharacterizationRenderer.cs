using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using keycuts.Common;

namespace keycuts.Tests.Characterization
{
    /// <summary>
    /// Renders what the legacy emitter and parser do today as plain text, so a behavior change shows up as a readable
    /// diff against a committed snapshot. Pins current behavior, bugs included; it is not a statement of what is right.
    /// No xUnit and no disk writes, so the same code can regenerate snapshots outside the test runner.
    /// </summary>
    public static class CharacterizationRenderer
    {
        public const string CorpusRelativePath = "tests/corpus/legacy";

        /// <summary>Known wrong answers in the parser snapshot, keyed by corpus path. Each names its bug and issue.</summary>
        private static readonly Dictionary<string, string> ParserNotes = new Dictionary<string, string>
        {
            ["edge-cases/command-type.bat"] = "The REM lines make it Command. Its launch line is the old emitter's START \"\" /B \"<command>\", which the fallback would read as File: the file pattern matches any quoted START (#93).",
            ["edge-cases/empty.bat"] = "No command line, so Unknown.",
            ["edge-cases/header-content-drift.bat"] = "The REM url wins over the Chrome launch line, by design: REM type and target beat the launch command. Command keeps the real line (#91).",
            ["edge-cases/openwithapp.bat"] = "Read from the REM lines, so OpenWithApp is lost: the fallback has no File-with-app form, and without REM lines it would read the app and the file as one File target (the line between its first and last quote), which re-emits the same line (#89).",
            ["new-format/keypaste.bat"] = "The REM target wins, so the hand-added %* is only in Command, not Target (#91). Re-emitting from these fields would drop it.",
            ["new-format/kp.bat"] = "The REM target wins, so the hand-added %* is only in Command, not Target (#91). Re-emitting from these fields would drop it.",
            ["not-keycuts/dequote.bat"] = "Not a keycut. No START or explorer.exe line, so Unknown.",
            ["not-keycuts/ss.bat"] = "Not a keycut. The explorer.exe argument is unquoted, so no pattern matches.",
            ["old-format/gg.bat"] = "Header says url, but the command is a hand-edited Chrome launcher; only the command line is read (#91).",
            ["old-format/pm.bat"] = "A real keycut, but the command line starts with a quoted path rather than START, so it is never read: Unknown, with no target, and the manager doesn't list it.",
        };

        /// <summary>
        /// Launch-line round-trip failures that are accepted, keyed by corpus path. These are hand-written scripts, not keycuts: their
        /// %VAR% text is meant as variables, and the emitter escapes a URL's % and &amp; so they open literally, so
        /// re-emitting one changes what it does whatever the parser reads.
        /// </summary>
        private static readonly Dictionary<string, string> ExpectedRoundTripFailures = new Dictionary<string, string>
        {
            ["not-keycuts/jira.bat"] = "Not a keycut: the emitter escapes its %VAR% variables to literal text (#91 step g).",
            ["not-keycuts/newjira.bat"] = "Not a keycut: the emitter escapes its %VAR% variables and ^& to literal text (#91 step g).",
            ["not-keycuts/swaphosts.bat"] = "Not a keycut: the emitter escapes its %VAR% variables and %* to literal text (#91 step g).",
        };

        #region Parser

        /// <summary>One block per corpus file, sorted by path: what <see cref="ShortcutFile"/> reads back.</summary>
        public static string RenderParser(string repoRoot)
        {
            var builder = new StringBuilder();
            builder.Append("# ShortcutFile parse of every file in ").Append(CorpusRelativePath).Append('\n');
            builder.Append("# Values are wrapped in | | so leading and trailing spaces show.\n");
            builder.Append("# Notes below cover the known wrong answers.\n");

            foreach (var (relativePath, lines) in ReadCorpus(repoRoot))
            {
                var parsed = new ShortcutFile(relativePath, lines);

                builder.Append('\n').Append("## ").Append(relativePath).Append('\n');
                AppendParsed(builder, parsed);

                if (ParserNotes.TryGetValue(relativePath, out var note))
                {
                    builder.Append("Note:        ").Append(note).Append('\n');
                }
            }

            return builder.ToString();
        }

        private static void AppendParsed(StringBuilder builder, ShortcutFile parsed)
        {
            builder.Append("Shortcut:    ").Append(Show(parsed.Shortcut)).Append('\n');
            builder.Append("Type:        ").Append(parsed.Type).Append('\n');
            builder.Append("Target:      ").Append(Show(parsed.Target)).Append('\n');
            builder.Append("OpenWithApp: ").Append(Show(parsed.OpenWithApp)).Append('\n');
            builder.Append("Command:     ").Append(Show(parsed.Command)).Append('\n');
            builder.Append("Description: ").Append(Show(parsed.Description)).Append('\n');
        }

        #endregion Parser

        #region Emitter

        private sealed class EmitterCase
        {
            public string Name;
            public ShortcutType Type;
            public string Target;
            public string OpenWithAppPath;
            public string Description = "";
            public string Note;
        }

        private static readonly EmitterCase[] EmitterCases =
        {
            new EmitterCase { Name = "url", Type = ShortcutType.Url, Target = "https://mail.google.com/mail/u/0/#inbox" },
            new EmitterCase { Name = "url-with-ampersand-and-percent", Type = ShortcutType.Url, Target = "https://example.com/search?q=a&b=50%&c=%%d" },
            new EmitterCase { Name = "url-with-double-ampersand", Type = ShortcutType.Url, Target = "https://example.com/?a=1&&b=2" },
            new EmitterCase { Name = "url-with-open-with-app", Type = ShortcutType.Url, Target = "https://example.com", OpenWithAppPath = @"C:\Program Files\Mozilla Firefox\firefox.exe" },
            new EmitterCase { Name = "file", Type = ShortcutType.File, Target = @"C:\Program Files\Notepad++\notepad++.exe" },
            new EmitterCase { Name = "file-with-ampersand-and-percent", Type = ShortcutType.File, Target = @"C:\R&D\100% done.txt", Note = "Not escaped for cmd, unlike URLs." },
            new EmitterCase { Name = "file-with-quote", Type = ShortcutType.File, Target = "C:\\odd\\a\"b.txt" },
            new EmitterCase { Name = "file-with-open-with-app", Type = ShortcutType.File, Target = @"C:\Shortcuts\README.txt", OpenWithAppPath = @"C:\Program Files\Notepad++\notepad++.exe" },
            new EmitterCase { Name = "file-unc", Type = ShortcutType.File, Target = @"\\fileserver\share\report.xlsx" },
            new EmitterCase { Name = "hostsfile", Type = ShortcutType.HostsFile, Target = @"C:\Windows\System32\drivers\etc\hosts" },
            new EmitterCase { Name = "folder", Type = ShortcutType.Folder, Target = @"C:\Users\me\Downloads" },
            new EmitterCase { Name = "folder-with-variable", Type = ShortcutType.Folder, Target = @"%USERPROFILE%\Google Drive" },
            new EmitterCase { Name = "clsidkey", Type = ShortcutType.CLSIDKey, Target = "::{645FF040-5081-101B-9F08-00AA002F954E}" },
            new EmitterCase { Name = "clsidkey-nested", Type = ShortcutType.CLSIDKey, Target = @"::{26EE0668-A00A-44D7-9371-BEB064C98683}\0\::{7B81BE6A-CE2B-4676-A29E-EB907A5126C5}" },
            new EmitterCase { Name = "command", Type = ShortcutType.Command, Target = "ipconfig /all" },
            new EmitterCase { Name = "unknown", Type = ShortcutType.Unknown, Target = @"C:\missing.txt", Note = "Runner.Run never emits Unknown; there is no launch line to write." },
            new EmitterCase { Name = "description", Type = ShortcutType.Url, Target = "https://example.com", Description = "  Line one\r\nline two  " },
            new EmitterCase { Name = "description-null", Type = ShortcutType.Url, Target = "https://example.com", Description = null },
            new EmitterCase { Name = "description-special-characters", Type = ShortcutType.Url, Target = "https://example.com", Description = "Opens %~dp0 & 100% | <b> ^ >c %1 %* %PATH:~0,3%", Note = "Every % is doubled, because cmd expands % before it reads REM and a %~ stops the script (#162). The other characters are inert on a REM line." },
        };

        /// <summary>One block per case: the lines <see cref="Runner.BuildShortcutLines"/> writes. The version is masked.</summary>
        public static string RenderEmitter()
        {
            var builder = new StringBuilder();
            builder.Append("# Runner.BuildShortcutLines output. <version> stands for Runner.Version.\n");

            foreach (var item in EmitterCases)
            {
                var shortcut = Shortcut.FromParts(item.Type, item.Target, $@"C:\Shortcuts\{item.Name}.bat", item.OpenWithAppPath);

                builder.Append('\n').Append("## ").Append(item.Name).Append('\n');

                if (item.Note != null)
                {
                    builder.Append("# Note: ").Append(item.Note).Append('\n');
                }

                List<string> lines;
                try
                {
                    lines = Runner.BuildShortcutLines(shortcut, item.Description);
                }
                catch (InvalidOperationException ex)
                {
                    builder.Append("throws ").Append(nameof(InvalidOperationException)).Append(": ").Append(ex.Message).Append('\n');
                    continue;
                }

                foreach (var line in lines)
                {
                    builder.Append(Show(MaskVersion(line))).Append('\n');
                }
            }

            return builder.ToString();
        }

        #endregion Emitter

        #region Round trip

        /// <summary>
        /// For every corpus file: parse it, emit a keycut from what was parsed, parse that again, and report whether
        /// the second parse matches the first. Each mismatch is a case the format can't carry today (#89, #91).
        /// </summary>
        public static string RenderRoundTrip(string repoRoot)
        {
            var header =
                "# Round trip: Parse(Emit(Parse(f))) compared with Parse(f), field by field.\n" +
                "# Files that parse as Unknown have nothing to emit and are listed as skipped.\n";

            return RenderRoundTrip(repoRoot, header, launchLineOnly: false);
        }

        /// <summary>
        /// Same as <see cref="RenderRoundTrip(string)"/>, but the second parse doesn't see the emitted REM &lt;type&gt; and
        /// REM &lt;target&gt; lines. Those lines copy whatever the first parse returned, so with them the round trip
        /// passes even when the emitted launch line is broken. Without them, only the launch line carries the keycut.
        /// </summary>
        public static string RenderLaunchLineRoundTrip(string repoRoot)
        {
            var header =
                "# Launch-line round trip: Parse(Emit(Parse(f))) compared with Parse(f), field by field, where the second\n" +
                "# parse doesn't see the emitted REM <type> and REM <target> lines. This checks that the emitted launch\n" +
                "# line alone carries the keycut, which RoundTrip.verified.txt can't: there the REM lines win.\n" +
                "# Files that parse as Unknown have nothing to emit and are listed as skipped.\n";

            return RenderRoundTrip(repoRoot, header, launchLineOnly: true);
        }

        private static string RenderRoundTrip(string repoRoot, string header, bool launchLineOnly)
        {
            var builder = new StringBuilder();
            builder.Append(header);

            var passed = 0;
            var failed = 0;
            var skipped = 0;
            var expected = 0;
            var body = new StringBuilder();

            foreach (var (relativePath, lines) in ReadCorpus(repoRoot))
            {
                var first = new ShortcutFile(relativePath, lines);

                if (first.Type == ShortcutType.Unknown)
                {
                    skipped++;
                    body.Append(relativePath).Append(": skipped (parses as Unknown)\n");
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(relativePath);
                var openWithApp = string.IsNullOrEmpty(first.OpenWithApp) ? null : first.OpenWithApp;
                var shortcut = Shortcut.FromParts(first.Type, first.Target, $@"C:\Shortcuts\{name}.bat", openWithApp);
                var emitted = Runner.BuildShortcutLines(shortcut, first.Description).ToArray();
                var reparsed = launchLineOnly ? emitted.Where(line => !IsTypeOrTargetRem(line)).ToArray() : emitted;
                var second = new ShortcutFile(relativePath, reparsed);

                var differences = Compare(first, second).ToList();

                // Only the launch line shows these: in the full round trip the REM lines carry the target unescaped.
                string expectedReason = null;
                var isExpectedFailure = launchLineOnly && ExpectedRoundTripFailures.TryGetValue(relativePath, out expectedReason);

                if (differences.Count == 0)
                {
                    passed++;
                    body.Append(relativePath).Append(isExpectedFailure ? ": ok, but listed as an expected failure; remove it from the list\n" : ": ok\n");
                }
                else
                {
                    failed++;

                    if (isExpectedFailure)
                    {
                        expected++;
                        body.Append(relativePath).Append(": FAILS (expected) ").Append(expectedReason).Append('\n');
                    }
                    else
                    {
                        body.Append(relativePath).Append(": FAILS\n");
                    }

                    foreach (var difference in differences)
                    {
                        body.Append("    ").Append(difference).Append('\n');
                    }

                    body.Append("    emitted: ").Append(Show(emitted[emitted.Length - 2])).Append('\n');
                }
            }

            var expectedText = expected > 0 ? $" ({expected} expected)" : "";
            builder.Append($"# {passed} ok, {failed} fail{expectedText}, {skipped} skipped\n\n");
            builder.Append(body);

            return builder.ToString();
        }

        // The tag must come right after REM, so a description that mentions <type> is kept. <destination> is the tag older
        // files carry; the emitter writes <target> now.
        private static readonly Regex TypeOrTargetRem = new Regex(@"^\s*REM\s+<(type|target|destination)>", RegexOptions.IgnoreCase);

        private static bool IsTypeOrTargetRem(string line)
        {
            return TypeOrTargetRem.IsMatch(line);
        }

        private static IEnumerable<string> Compare(ShortcutFile first, ShortcutFile second)
        {
            if (first.Type != second.Type)
            {
                yield return $"Type:        {first.Type} -> {second.Type}";
            }

            if (first.Target != second.Target)
            {
                yield return $"Target: {Show(first.Target)} -> {Show(second.Target)}";
            }

            if ((first.OpenWithApp ?? "") != (second.OpenWithApp ?? ""))
            {
                yield return $"OpenWithApp: {Show(first.OpenWithApp)} -> {Show(second.OpenWithApp)}";
            }

            if (first.Description != second.Description)
            {
                yield return $"Description: {Show(first.Description)} -> {Show(second.Description)}";
            }
        }

        #endregion Round trip

        #region Helpers

        /// <summary>Corpus files as (path relative to the corpus with forward slashes, lines), sorted ordinally.</summary>
        public static IEnumerable<(string RelativePath, string[] Lines)> ReadCorpus(string repoRoot)
        {
            var corpus = Path.Combine(repoRoot, CorpusRelativePath);

            return Directory.EnumerateFiles(corpus, "*.bat", SearchOption.AllDirectories)
                .Select(file => (RelativePath: Path.GetRelativePath(corpus, file).Replace('\\', '/'), File: file))
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .Select(item => (item.RelativePath, File.ReadAllLines(item.File)))
                .ToList();
        }

        private static string Show(string value)
        {
            return value == null ? "<null>" : $"|{value}|";
        }

        private static string MaskVersion(string line)
        {
            return line.Replace($"{Runner.AppName} {Runner.Version}", $"{Runner.AppName} <version>");
        }

        #endregion Helpers
    }
}
