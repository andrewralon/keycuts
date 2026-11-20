using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using keycuts.Common;
using static keycuts.Tests.Characterization.CharacterizationRenderer;

namespace keycuts.Tests.Characterization
{
    /// <summary>The macOS twin of <see cref="CharacterizationRenderer"/>: what <see cref="ShFormat"/> writes and reads, on <c>tests/corpus/macos</c>.</summary>
    public static class ShCharacterizationRenderer
    {
        public const string CorpusRelativePath = "tests/corpus/macos";

        private static readonly Dictionary<string, string> ParserNotes = new Dictionary<string, string>
        {
            ["drift"] = "The header's url wins over the Firefox launch line, as the REM lines do in a .bat. Command keeps the real line, and OpenWithApp comes from it.",
            ["no-header"] = "No header: the launch line alone says Url.",
            ["no-header-app"] = "No header: a path reads as a File (a Folder needs the header). \"$HOME/…\" is kept as text, not expanded.",
            ["not-a-keycut"] = "No open or /bin/sh -c line, so Unknown.",
            ["unclosed"] = "The quote never closes, so it isn't a launch line: Unknown.",
        };

        private static readonly (string Name, ShortcutType Type, string Target, string App, string Description, string Note)[] EmitterCases =
        {
            ("url", ShortcutType.Url, "https://mail.google.com/mail/u/0/#inbox", null, "", null),
            ("url-with-quote-dollar-backtick", ShortcutType.Url, "https://example.com/?q=it's+$5&x=`a`", null, "", "Single quotes: nothing in them expands; ' is written '\\''."),
            ("url-with-open-with-app", ShortcutType.Url, "https://example.com", "/Applications/Firefox.app", "", null),
            ("file", ShortcutType.File, "/Users/me/notes.txt", null, "", null),
            ("file-app-bundle", ShortcutType.File, "/Applications/Safari.app", null, "", "open launches an app bundle."),
            ("file-with-open-with-app", ShortcutType.File, "/Users/me/notes.txt", "/System/Applications/TextEdit.app", "", null),
            ("file-nasty", ShortcutType.File, "/Users/me/it's \"odd\" $HOME `x` café ☕.txt", null, "", null),
            ("folder", ShortcutType.Folder, "/Users/me/Downloads", null, "", null),
            ("command", ShortcutType.Command, "say 'hi there'", null, "", "Never produced by create, as on Windows."),
            ("description", ShortcutType.Url, "https://example.com", null, "  Line one\r\nline two  ", null),
            ("description-null", ShortcutType.Url, "https://example.com", null, null, null),
            ("description-special-characters", ShortcutType.Url, "https://example.com", null, "100% \"done\" $HOME `rm -rf` 'x' # <b> \\", "Kept as is: nothing in a sh comment is special."),
            ("target-with-line-break", ShortcutType.File, "/Users/me/a\nb.txt", null, "", "The rest of the header line would run as a command."),
            ("clsidkey", ShortcutType.CLSIDKey, "::{645FF040-5081-101B-9F08-00AA002F954E}", null, "", "Windows only."),
            ("hostsfile", ShortcutType.HostsFile, "/etc/hosts", null, "", "Windows only."),
            ("unknown", ShortcutType.Unknown, "/missing.txt", null, "", null),
        };

        public static string RenderParser(string repoRoot)
        {
            var builder = new StringBuilder();
            builder.Append("# ShFormat parse of every file in ").Append(CorpusRelativePath).Append('\n');
            builder.Append("# Values are wrapped in | | so leading and trailing spaces show.\n");

            foreach (var (relativePath, lines) in ReadShCorpus(repoRoot))
            {
                builder.Append('\n').Append("## ").Append(relativePath).Append('\n');
                AppendParsed(builder, new ShFormat().Parse(relativePath, lines));

                if (ParserNotes.TryGetValue(relativePath, out var note))
                {
                    builder.Append("Note:        ").Append(note).Append('\n');
                }
            }

            return builder.ToString();
        }

        public static string RenderEmitter()
        {
            var builder = new StringBuilder();
            builder.Append("# ShFormat.EmitLines output. <version> stands for Runner.Version.\n");

            foreach (var item in EmitterCases)
            {
                var shortcut = Shortcut.FromParts(item.Type, item.Target, $"/Users/me/Shortcuts/{item.Name}", item.App);

                builder.Append('\n').Append("## ").Append(item.Name).Append('\n');

                if (item.Note != null)
                {
                    builder.Append("# Note: ").Append(item.Note).Append('\n');
                }

                try
                {
                    foreach (var line in new ShFormat().EmitLines(shortcut, item.Description))
                    {
                        builder.Append(Show(MaskVersion(line))).Append('\n');
                    }
                }
                catch (InvalidOperationException ex)
                {
                    builder.Append("throws ").Append(nameof(InvalidOperationException)).Append(": ").Append(ex.Message).Append('\n');
                }
            }

            return builder.ToString();
        }

        /// <summary>Parse, emit, parse again, with and without the header's type and target lines (the launch line alone).</summary>
        public static string RenderRoundTrip(string repoRoot)
        {
            var builder = new StringBuilder();
            builder.Append("# Round trip: Parse(Emit(Parse(f))) compared with Parse(f) on Type, Target, OpenWithApp and Description.\n");
            builder.Append("# \"launch line\" is the same without the emitted <type> and <target> lines. Unknown files are skipped.\n\n");

            foreach (var (relativePath, lines) in ReadShCorpus(repoRoot))
            {
                var format = new ShFormat();
                var first = format.Parse(relativePath, lines);

                if (first.Type == ShortcutType.Unknown)
                {
                    builder.Append(relativePath).Append(": skipped (parses as Unknown)\n");
                    continue;
                }

                var shortcut = Shortcut.FromParts(first.Type, first.Target, "/Users/me/Shortcuts/" + relativePath, string.IsNullOrEmpty(first.OpenWithApp) ? null : first.OpenWithApp);
                var emitted = format.EmitLines(shortcut, first.Description).ToArray();
                var full = Differences(first, format.Parse(relativePath, emitted));
                var launch = Differences(first, format.Parse(relativePath, emitted.Where(line => !TypeOrTarget.IsMatch(line)).ToArray()));

                builder.Append(relativePath).Append(": ").Append(full.Count == 0 ? "ok" : "FAILS").Append(", launch line ").Append(launch.Count == 0 ? "ok" : "FAILS").Append('\n');

                foreach (var difference in full.Select(d => "full: " + d).Concat(launch.Select(d => "launch line: " + d)))
                {
                    builder.Append("    ").Append(difference).Append('\n');
                }
            }

            return builder.ToString();
        }

        private static readonly Regex TypeOrTarget = new Regex(@"^\s*#\s*<(type|target)>");

        private static List<string> Differences(ShortcutFile first, ShortcutFile second)
        {
            var differences = new List<string>();

            if (first.Type != second.Type)
            {
                differences.Add($"Type: {first.Type} -> {second.Type}");
            }

            if (first.Target != second.Target)
            {
                differences.Add($"Target: {Show(first.Target)} -> {Show(second.Target)}");
            }

            if ((first.OpenWithApp ?? "") != (second.OpenWithApp ?? ""))
            {
                differences.Add($"OpenWithApp: {Show(first.OpenWithApp)} -> {Show(second.OpenWithApp)}");
            }

            if (first.Description != second.Description)
            {
                differences.Add($"Description: {Show(first.Description)} -> {Show(second.Description)}");
            }

            return differences;
        }

        /// <summary>Every corpus file but the README, as (name, lines), sorted ordinally.</summary>
        public static IEnumerable<(string RelativePath, string[] Lines)> ReadShCorpus(string repoRoot)
        {
            var corpus = Path.Combine(repoRoot, CorpusRelativePath);

            return Directory.EnumerateFiles(corpus)
                .Where(file => Path.GetFileName(file) != "README.md")
                .Select(file => (RelativePath: Path.GetFileName(file), File: file))
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .Select(item => (item.RelativePath, File.ReadAllLines(item.File)))
                .ToList();
        }
    }
}
