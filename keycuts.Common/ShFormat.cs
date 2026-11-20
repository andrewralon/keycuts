using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace keycuts.Common
{
    /// <summary>
    /// The macOS keycut (docs/macos-port.md, "A keycut on macOS"): a <c>#!/bin/sh</c> script with no extension, mode 755,
    /// whose <c># </c> header lines mirror the <c>.bat</c>'s <c>REM</c> ones. Every target is single-quoted, so nothing in
    /// it is expanded, except <c>$HOME</c> (<see cref="QuoteTarget"/>). Url, File and Folder open with <c>open</c>, a File with an app with <c>open -a</c>, and a Command
    /// (parsed, never created) runs through <c>/bin/sh -c</c>. CLSID and hosts-file keycuts are Windows only.
    /// </summary>
    public sealed class ShFormat : IScriptFormat
    {
        public const string Shebang = "#!/bin/sh";

        public string Extension => "";

        public string CommentPrefix => "#";

        public bool NeedsExecutableBit => true;

        private static readonly Regex HeaderType = new Regex(@"^\s*#\s*<type>(?<value>.*)</type>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HeaderTarget = new Regex(@"^\s*#\s*<target>(?<value>.*)</target>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HeaderDescription = new Regex(@"^\s*#\s*<description>(?<value>.*)</description>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public List<string> EmitLines(Shortcut shortcut, string description = "")
        {
            // A line break would end the header line early and run the rest as a command
            foreach (var value in new[] { shortcut.FullPath, shortcut.Target, shortcut.OpenWithAppPath })
            {
                if (value != null && value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                {
                    throw new InvalidOperationException("A keycut's name, target or app can't hold a line break.");
                }
            }

            var lines = new List<string>
            {
                Shebang,
                $"# {Runner.AppName} {Runner.Version}",
                $"# <shortcut>{shortcut.FullPath}</shortcut>",
                $"# <type>{shortcut.Type.ToString().ToLower()}</type>",
                $"# <target>{shortcut.Target}</target>",
                $"# <description>{SanitizeDescription(description)}</description>",
                BuildCommand(shortcut),
            };

            // After the launch line, so counting the run never delays it; the script exits with the launch's status
            lines.AddRange(UsageLog.ShLines);

            return lines;
        }

        /// <summary>The launch line. No discard arm, as in <c>Runner.BuildCommand</c>: a new type is a build error (CS8509).</summary>
        private static string BuildCommand(Shortcut shortcut)
        {
            if (shortcut.OpenWithApp)
            {
                return $"open -a {Quote(shortcut.OpenWithAppPath)} {QuoteTarget(shortcut.Target)}";
            }

#pragma warning disable CS8524
            return shortcut.Type switch
            {
                ShortcutType.Url or ShortcutType.File or ShortcutType.Folder => $"open {QuoteTarget(shortcut.Target)}",
                ShortcutType.Command => $"/bin/sh -c {Quote(shortcut.Target)}",
                ShortcutType.CLSIDKey or ShortcutType.HostsFile or ShortcutType.Unknown =>
                    throw new InvalidOperationException($"Can't write a keycut of type {shortcut.Type} on macOS."),
            };
#pragma warning restore CS8524
        }

        public ShortcutFile Parse(string file, string[] lines)
        {
            var parsed = new ShortcutFile
            {
                Path = file,
                Shortcut = System.IO.Path.GetFileName(file),
                Type = ShortcutType.Unknown,
                Description = FirstValue(lines, HeaderDescription) ?? "",
            };

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal) || !TryReadLaunchLine(line, out var type, out var target, out var app))
                {
                    continue;
                }

                parsed.Command = line;
                parsed.Type = type;
                parsed.Target = target;
                parsed.OpenWithApp = app;
                break;
            }

            // The header lines win, as the REM lines do in a .bat
            if (ShortcutFile.TryParseTypeAndTarget(FirstValue(lines, HeaderType), FirstValue(lines, HeaderTarget), out var headerType, out var headerTarget))
            {
                parsed.Type = headerType;
                parsed.Target = headerTarget;
            }

            if (parsed.Type == ShortcutType.File)
            {
                parsed.Program = parsed.Target;
            }

            return parsed;
        }

        /// <summary>
        /// <c>open &lt;target&gt;</c>, <c>open -a &lt;app&gt; &lt;target&gt;</c> or <c>/bin/sh -c &lt;command&gt;</c>, each argument one
        /// sh word. Without the header, a target that isn't a path reads as a Url and a path as a File (a Folder needs the header).
        /// </summary>
        private static bool TryReadLaunchLine(string line, out ShortcutType type, out string target, out string app)
        {
            type = ShortcutType.Unknown;
            target = null;
            app = null;
            var words = SplitWords(line);

            if (words == null || words.Count < 2)
            {
                return false;
            }

            if (words[0] == "/bin/sh" && words.Count == 3 && words[1] == "-c")
            {
                type = ShortcutType.Command;
                target = words[2];
                return true;
            }

            if (words[0] != "open")
            {
                return false;
            }

            if (words.Count == 4 && words[1] == "-a")
            {
                app = words[2];
            }
            else if (words.Count != 2)
            {
                return false;
            }

            target = words[words.Count - 1];
            type = target.StartsWith("/", StringComparison.Ordinal) || target.StartsWith("$HOME", StringComparison.Ordinal)
                ? ShortcutType.File
                : ShortcutType.Url;
            return true;
        }

        private static string FirstValue(string[] lines, Regex regex)
        {
            foreach (var line in lines)
            {
                var match = regex.Match(line);

                if (match.Success)
                {
                    return match.Groups["value"].Value.Trim();
                }
            }

            return null;
        }

        #region Quoting

        /// <summary>A description on its comment line: on one line, trimmed. Nothing in a sh comment is special.</summary>
        internal static string SanitizeDescription(string description)
        {
            return (description ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>One sh word that is <paramref name="value"/> exactly: single quotes, with each <c>'</c> written <c>'\''</c>.</summary>
        public static string Quote(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }

        /// <summary>
        /// A target as one word: <see cref="Quote"/>, except a path saved with <c>$HOME</c> (<see cref="PathVariables.ForMac"/>),
        /// which goes in double quotes so the variable expands, with <c>\ " $</c> and backticks in the rest escaped.
        /// </summary>
        public static string QuoteTarget(string target)
        {
            const string home = "$HOME";

            if (target != home && !target.StartsWith(home + "/", StringComparison.Ordinal))
            {
                return Quote(target);
            }

            return "\"" + home + EscapeForDoubleQuotes(target.Substring(home.Length)) + "\"";
        }

        /// <summary>Text for inside double quotes, where it stays as is: <c>\ " $</c> and backticks escaped.</summary>
        public static string EscapeForDoubleQuotes(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
        }

        /// <summary>
        /// The words of a line as sh reads them, for the forms the emitter writes: single quotes, double quotes (where
        /// <c>\</c> escapes <c>\ " $ `</c> and a <c>$</c> stays as text, so <c>"$HOME/x"</c> reads <c>$HOME/x</c>) and
        /// backslashes. Null for an unclosed quote. Nothing is expanded.
        /// </summary>
        public static List<string> SplitWords(string line)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            var inWord = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == ' ' || c == '\t')
                {
                    if (inWord)
                    {
                        words.Add(word.ToString());
                        word.Clear();
                        inWord = false;
                    }

                    continue;
                }

                inWord = true;

                if (c == '\'')
                {
                    var close = line.IndexOf('\'', i + 1);

                    if (close < 0)
                    {
                        return null;
                    }

                    word.Append(line, i + 1, close - i - 1);
                    i = close;
                }
                else if (c == '"')
                {
                    for (i++; ; i++)
                    {
                        if (i >= line.Length)
                        {
                            return null;
                        }

                        if (line[i] == '"')
                        {
                            break;
                        }

                        if (line[i] == '\\' && i + 1 < line.Length && "\\\"$`".IndexOf(line[i + 1]) >= 0)
                        {
                            i++;
                        }

                        word.Append(line[i]);
                    }
                }
                else if (c == '\\' && i + 1 < line.Length)
                {
                    word.Append(line[++i]);
                }
                else
                {
                    word.Append(c);
                }
            }

            if (inWord)
            {
                words.Add(word.ToString());
            }

            return words;
        }

        #endregion Quoting
    }
}
