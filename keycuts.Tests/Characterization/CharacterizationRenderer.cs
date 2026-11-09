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
            ["edge-cases/command-type.bat"] = "Parsed as File, not Command: the file pattern is tried first and matches any quoted START (#93).",
            ["edge-cases/empty.bat"] = "No command line, so the type is never set and stays default(ShortcutType), which is Command, not Unknown.",
            ["edge-cases/header-content-drift.bat"] = "Only the command line is read; the header's type and destination are ignored (#91).",
            ["edge-cases/hostsfile-type.bat"] = "OpenWithApp comes back inside two pairs of quotes.",
            ["edge-cases/openwithapp.bat"] = "The app and the destination are joined into one path with a backslash by the quote handling in MatchesRegex, which exists for the old START /D form (#89).",
            ["new-format/keypaste.bat"] = "Hand-edited %* after the closing quote fails the file pattern, so it falls through to Command.",
            ["new-format/kp.bat"] = "Hand-edited %* after the closing quote fails the file pattern, so it falls through to Command.",
            ["not-keycuts/dequote.bat"] = "Not a keycut. No START or explorer.exe line, so the type stays default(ShortcutType), which is Command.",
            ["not-keycuts/ss.bat"] = "Not a keycut. The explorer.exe argument is unquoted, so no pattern matches.",
            ["old-format/gg.bat"] = "Header says url, but the command is a hand-edited Chrome launcher; only the command line is read (#91).",
            ["old-format/pm.bat"] = "The command line starts with a quoted path rather than START, so it is never read: the type stays default(ShortcutType), which is Command, with no destination.",
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
            builder.Append("Destination: ").Append(Show(parsed.Destination)).Append('\n');
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
            public string Destination;
            public string OpenWithAppPath;
            public string Description = "";
            public string Note;
        }

        private static readonly EmitterCase[] EmitterCases =
        {
            new EmitterCase { Name = "url", Type = ShortcutType.Url, Destination = "https://mail.google.com/mail/u/0/#inbox" },
            new EmitterCase { Name = "url-with-ampersand-and-percent", Type = ShortcutType.Url, Destination = "https://example.com/search?q=a&b=50%&c=%%d" },
            new EmitterCase { Name = "url-with-double-ampersand", Type = ShortcutType.Url, Destination = "https://example.com/?a=1&&b=2" },
            new EmitterCase { Name = "url-with-open-with-app", Type = ShortcutType.Url, Destination = "https://example.com", OpenWithAppPath = @"C:\Program Files\Mozilla Firefox\firefox.exe" },
            new EmitterCase { Name = "file", Type = ShortcutType.File, Destination = @"C:\Program Files\Notepad++\notepad++.exe" },
            new EmitterCase { Name = "file-with-ampersand-and-percent", Type = ShortcutType.File, Destination = @"C:\R&D\100% done.txt", Note = "Not escaped for cmd, unlike URLs." },
            new EmitterCase { Name = "file-with-quote", Type = ShortcutType.File, Destination = "C:\\odd\\a\"b.txt" },
            new EmitterCase { Name = "file-with-open-with-app", Type = ShortcutType.File, Destination = @"C:\Shortcuts\README.txt", OpenWithAppPath = @"C:\Program Files\Notepad++\notepad++.exe" },
            new EmitterCase { Name = "file-unc", Type = ShortcutType.File, Destination = @"\\fileserver\share\report.xlsx" },
            new EmitterCase { Name = "hostsfile", Type = ShortcutType.HostsFile, Destination = @"C:\Windows\System32\drivers\etc\hosts" },
            new EmitterCase { Name = "folder", Type = ShortcutType.Folder, Destination = @"C:\Users\me\Downloads" },
            new EmitterCase { Name = "folder-with-variable", Type = ShortcutType.Folder, Destination = @"%USERPROFILE%\Google Drive" },
            new EmitterCase { Name = "clsidkey", Type = ShortcutType.CLSIDKey, Destination = "::{645FF040-5081-101B-9F08-00AA002F954E}" },
            new EmitterCase { Name = "clsidkey-nested", Type = ShortcutType.CLSIDKey, Destination = @"::{26EE0668-A00A-44D7-9371-BEB064C98683}\0\::{7B81BE6A-CE2B-4676-A29E-EB907A5126C5}" },
            new EmitterCase { Name = "command", Type = ShortcutType.Command, Destination = "ipconfig /all" },
            new EmitterCase { Name = "unknown", Type = ShortcutType.Unknown, Destination = @"C:\missing.txt", Note = "Runner.Run never emits Unknown; there is no launch line to write." },
            new EmitterCase { Name = "description", Type = ShortcutType.Url, Destination = "https://example.com", Description = "  Line one\r\nline two  " },
            new EmitterCase { Name = "description-null", Type = ShortcutType.Url, Destination = "https://example.com", Description = null },
        };

        /// <summary>One block per case: the lines <see cref="Runner.BuildShortcutLines"/> writes. The version is masked.</summary>
        public static string RenderEmitter()
        {
            var builder = new StringBuilder();
            builder.Append("# Runner.BuildShortcutLines output. <version> stands for Runner.Version.\n");

            foreach (var item in EmitterCases)
            {
                var shortcut = Shortcut.FromParts(item.Type, item.Destination, $@"C:\Shortcuts\{item.Name}.bat", item.OpenWithAppPath);

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
        /// REM &lt;destination&gt; lines. Those lines copy whatever the first parse returned, so with them the round trip
        /// passes even when the emitted launch line is broken. Without them, only the launch line carries the keycut.
        /// </summary>
        public static string RenderLaunchLineRoundTrip(string repoRoot)
        {
            var header =
                "# Launch-line round trip: Parse(Emit(Parse(f))) compared with Parse(f), field by field, where the second\n" +
                "# parse doesn't see the emitted REM <type> and REM <destination> lines. This checks that the emitted launch\n" +
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
                var shortcut = Shortcut.FromParts(first.Type, first.Destination, $@"C:\Shortcuts\{name}.bat", openWithApp);
                var emitted = Runner.BuildShortcutLines(shortcut, first.Description).ToArray();
                var reparsed = launchLineOnly ? emitted.Where(line => !IsTypeOrDestinationRem(line)).ToArray() : emitted;
                var second = new ShortcutFile(relativePath, reparsed);

                var differences = Compare(first, second).ToList();

                if (differences.Count == 0)
                {
                    passed++;
                    body.Append(relativePath).Append(": ok\n");
                }
                else
                {
                    failed++;
                    body.Append(relativePath).Append(": FAILS\n");

                    foreach (var difference in differences)
                    {
                        body.Append("    ").Append(difference).Append('\n');
                    }

                    body.Append("    emitted: ").Append(Show(emitted[emitted.Length - 2])).Append('\n');
                }
            }

            builder.Append($"# {passed} ok, {failed} fail, {skipped} skipped\n\n");
            builder.Append(body);

            return builder.ToString();
        }

        // The tag must come right after REM, so a description that mentions <type> is kept
        private static readonly Regex TypeOrDestinationRem = new Regex(@"^\s*REM\s+<(type|destination)>", RegexOptions.IgnoreCase);

        private static bool IsTypeOrDestinationRem(string line)
        {
            return TypeOrDestinationRem.IsMatch(line);
        }

        private static IEnumerable<string> Compare(ShortcutFile first, ShortcutFile second)
        {
            if (first.Type != second.Type)
            {
                yield return $"Type:        {first.Type} -> {second.Type}";
            }

            if (first.Destination != second.Destination)
            {
                yield return $"Destination: {Show(first.Destination)} -> {Show(second.Destination)}";
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
