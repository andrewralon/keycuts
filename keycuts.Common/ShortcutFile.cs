using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace keycuts.Common
{
    public class ShortcutFile
    {
        private readonly string explorer = "\\explorer.exe\"";
        private readonly string start = "START ";

        private static readonly string patternCLSID = "\".+explorer.exe\" \"[shell:]?(::{.+})\"";
        private static readonly string patternFolder = "\".+explorer.exe\" (\".+\")";
        private static readonly string patternHostsFile = "START \"\" \\/[BD] (\".+\") \"(.+hosts)\"$";
        private static readonly string patternFile = "START \"\" \\/[BD] (\".+\")( \"(.+)\")?$";
        private static readonly string patternCommand = "START \"\" \\/[BD] (\".+)?";

        // The emitter's Command line (Runner.BuildCommand): everything between the cmd /C quotes, inner quotes included.
        private static readonly Regex regexCmdCommand = new Regex("^START \"\" /B cmd /C \"(.*)\"$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // The 0.1.x program line: START "" /D "<folder>" <rest>, where <rest> is a quoted program, then any arguments.
        private static readonly Regex regexStartInFolder = new Regex("^START \"\" /D \"([^\"]*)\" (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // A quoted program followed only by quoted arguments, each after one space.
        private static readonly Regex regexProgramAndArguments = new Regex("^\"([^\"]*)\"((?: \"[^\"]*\")*)$", RegexOptions.Compiled);
        private static readonly string patternUrl = "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

        private static readonly Regex regexDescription = new Regex(@"^\s*REM\s+<description>(.*)</description>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex regexRemType = new Regex(@"^\s*REM\s+<type>(?<value>.*)</type>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // <target> is what the emitter writes (#195). Files written before that call the same thing <destination>, and users
        // still have them, so both are read; the closing tag has to match the opening one.
        private static readonly Regex regexRemTarget = new Regex(@"^\s*REM\s+<(?<tag>target|destination)>(?<value>.*)</\k<tag>>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly string Extension = ".bat";

        public string Path { get; set; }
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Target { get; set; }
        public string OpenWithApp { get; set; }
        public string Command { get; set; }

        public string Description { get; set; } = "";

        /// <summary>
        /// For a File: the path of the program the launch line runs, without its arguments, as written (it may hold
        /// <c>%VARS%</c> and may be a bare name such as <c>chrome</c>). Empty for other types, and for a File whose launch
        /// line the parser can't take apart (a target with a quote in it that isn't one of the forms it knows): then
        /// nothing is known about where the program is. <see cref="Target"/> is the same text for a plain File.
        /// </summary>
        public string Program { get; set; } = "";

        /// <summary>For a File: what follows <see cref="Program"/> on the launch line, as written (quotes included). Empty if nothing does, or it can't be told.</summary>
        public string Arguments { get; set; } = "";

        public ShortcutFile()
        {
        }

        public ShortcutFile(string file)
            : this(file, File.ReadAllLines(file))
        {
        }

        /// <summary>Parses lines already read from <paramref name="file"/>, which is only used for its name.</summary>
        public ShortcutFile(string file, string[] allLines)
        {
            var lines = allLines
                .Where(x => !IsRemLine(x) &&
                            (x.Contains(explorer) ||
                             x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();

            Description = ParseDescription(allLines);

            Path = file;
            Shortcut = System.IO.Path.GetFileNameWithoutExtension(file);

            // Without this a file with no launch line would keep the enum's default, Command.
            Type = ShortcutType.Unknown;

            // What the launch line says about the program, when the pattern that read it knows (see Program)
            string launchTarget = null;
            string launchProgram = null;
            string launchArguments = null;

            foreach (var line in lines)
            {
                Command = line;

                if (ShortcutFile.IsEmittedCommand(line, out string emittedCommand))
                {
                    Target = emittedCommand;
                    Type = ShortcutType.Command;
                    break;
                }
                else if (ShortcutFile.IsCLSIDKey(line, out string clsidKey))
                {
                    Target = clsidKey;
                    Type = ShortcutType.CLSIDKey;
                    break;
                }
                else if (ShortcutFile.IsFolder(line, out string folder))
                {
                    Target = Unquote(folder);
                    Type = ShortcutType.Folder;
                    break;
                }
                else if (ShortcutFile.IsHostsFile(line, out string hostsFile, out string openWithApp))
                {
                    Target = hostsFile;
                    Type = ShortcutType.HostsFile;
                    OpenWithApp = Unquote(openWithApp);
                    break;
                }
                else if (ShortcutFile.TryReadStartInFolder(line, out string fileInFolder, out string programInFolder, out string argumentsInFolder))
                {
                    Target = fileInFolder;
                    Type = ShortcutType.File;
                    launchTarget = fileInFolder;
                    launchProgram = programInFolder;
                    launchArguments = argumentsInFolder;
                    break;
                }
                else if (ShortcutFile.IsFile(line, out string newFile))
                {
                    Target = Unquote(newFile);
                    Type = ShortcutType.File;
                    break;
                }
                else if (ShortcutFile.IsCommand(line, out string command))
                {
                    Target = command;
                    Type = ShortcutType.Command;
                    break;
                }
                else if (ShortcutFile.IsValidUrl(line, out string url))
                {
                    Target = url;
                    Type = ShortcutType.Url;
                    break;
                }

                Type = ShortcutType.Unknown;
            }

            // The comment lines hold the type and target as the emitter was given them (only surrounding
            // whitespace is trimmed: Windows paths can't start or end with it), so they beat the regexes above,
            // which are the fallback for files that don't have them (the 0.1.x format).
            // They win even when the launch command disagrees (hand-edited %* arguments, a changed program, a legacy
            // file whose REM holds the expanded path). That is deliberate: this parser only feeds display and checks,
            // the .bat still runs its own command, and Command keeps the full launch line. Don't re-emit a file from
            // these fields without keeping Command, or hand-added arguments are lost.
            if (TryParseRemTypeAndTarget(allLines, out var remType, out var remTarget))
            {
                Type = remType;
                Target = remTarget;
            }

            if (Type == ShortcutType.File && Target != null)
            {
                if (launchProgram != null && string.Equals(Target, launchTarget, StringComparison.Ordinal))
                {
                    Program = launchProgram;
                    Arguments = launchArguments;
                }
                else if (Target.IndexOf('"') < 0)
                {
                    // A path can't hold a quote, so this is the program alone
                    Program = Target;
                }
            }
        }

        /// <summary>
        /// Reads the first well-formed <c>REM &lt;type&gt;</c> line and the first well-formed <c>REM &lt;target&gt;</c> line
        /// (or <c>REM &lt;destination&gt;</c>, which files written before #195 have; whichever comes first in the file).
        /// Both must be present, the type must be a known one other than Unknown, and the target must not be empty, or
        /// neither is used.
        /// </summary>
        public static bool TryParseRemTypeAndTarget(string[] lines, out ShortcutType type, out string target)
        {
            type = ShortcutType.Unknown;
            target = null;

            var typeText = FirstRemValue(lines, regexRemType);
            var targetText = FirstRemValue(lines, regexRemTarget);

            // The name must match a defined value exactly (ignoring case): Enum.TryParse alone also accepts numbers
            // ("3" is Folder, "-1" is an undefined value whose name is "-1") and lists ("file,folder").
            if (typeText == null || string.IsNullOrEmpty(targetText) ||
                !Enum.TryParse(typeText, true, out ShortcutType parsed) ||
                !Enum.IsDefined(typeof(ShortcutType), parsed) ||
                !string.Equals(parsed.ToString(), typeText, StringComparison.OrdinalIgnoreCase) ||
                parsed == ShortcutType.Unknown)
            {
                return false;
            }

            type = parsed;
            target = targetText;
            return true;
        }

        private static string FirstRemValue(string[] lines, Regex regex)
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

        /// <summary>
        /// Removes one leading and one trailing quote. The File and Folder patterns capture the launch line from its first
        /// quote to its last, and the emitter wraps the target in one pair of quotes, so this is its exact inverse.
        /// That holds even when the value has quotes inside (a program followed by quoted arguments, in a hand-written
        /// file): re-emitting it gives back the original line. A path itself can't contain quotes on Windows.
        /// </summary>
        public static string Unquote(string value)
        {
            if (value != null && value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        public static bool IsRemLine(string line)
        {
            return line.TrimStart().StartsWith("REM ", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the trimmed text of the first well-formed description line, or empty. A doubled <c>%%</c> reads as one
        /// <c>%</c>: the emitter doubles them so cmd doesn't expand them (<c>Runner.SanitizeDescription</c>), and cmd
        /// itself reads <c>%%</c> in a batch file as one.
        /// </summary>
        public static string ParseDescription(string[] lines)
        {
            foreach (var line in lines)
            {
                var match = regexDescription.Match(line);

                if (match.Success)
                {
                    return match.Groups[1].Value.Trim().Replace("%%", "%");
                }
            }

            return "";
        }

        public static bool IsCLSIDKey(string line, out string clsidKey)
        {
            return MatchesRegex(patternCLSID, line, ShortcutType.CLSIDKey, out clsidKey);
        }

        public static bool IsFolder(string line, out string folder)
        {
            return MatchesRegex(patternFolder, line, ShortcutType.Folder, out folder);
        }

        public static bool IsHostsFile(string line, out string hostsFile, out string openWithApp)
        {
            return MatchesRegex(patternHostsFile, line, ShortcutType.HostsFile, out hostsFile, out openWithApp);
        }

        public static bool IsFile(string line, out string file)
        {
            return MatchesRegex(patternFile, line, ShortcutType.File, out file);
        }

        /// <summary>
        /// Matches the emitter's Command line exactly. Try it before the Explorer patterns, which aren't anchored and
        /// would match an explorer.exe call inside the command.
        /// </summary>
        public static bool IsEmittedCommand(string line, out string command)
        {
            var match = regexCmdCommand.Match(line);
            command = match.Success ? match.Groups[1].Value : "";
            return match.Success;
        }

        /// <summary>
        /// Reads the launch line for a program that starts in a folder, <c>START "" /D "&lt;folder&gt;" ...</c>, which is
        /// the 0.1.x form and what people write by hand. The folder and program are joined into one path (just the program
        /// when the folder is empty). What follows the program decides what the target holds:
        /// <list type="bullet">
        /// <item>Only quoted arguments (<c>"&lt;program&gt;" ["&lt;argument&gt;" ...]</c>): they are kept after the program,
        /// unquoted like a File target, <c>C:\Tools\app.exe" "arg</c>. The /B line the emitter writes for that reads back
        /// the same.</item>
        /// <item>Anything else, an unquoted program or an argument that isn't quoted (<c>chrome.exe --profile-directory="Profile 2"</c>):
        /// the target is the program alone, <c>C:\Tools\chrome.exe</c>. The arguments don't fit in a target that the
        /// emitter wraps in one pair of quotes, so they are only on <see cref="Arguments"/> (see
        /// <see cref="TryReadStartInFolder"/>).</item>
        /// </list>
        /// A line whose program starts with <c>/</c> (a START switch such as <c>/MIN</c>) is left to the other patterns.
        /// </summary>
        public static bool IsStartInFolder(string line, out string file)
        {
            return TryReadStartInFolder(line, out file, out _, out _);
        }

        /// <summary>
        /// <see cref="IsStartInFolder"/> that also says where the program ends: <paramref name="program"/> is the folder
        /// and program joined (the path to look for), and <paramref name="arguments"/> is what follows it on the line,
        /// trimmed and as written (empty if there is nothing). <paramref name="file"/> is the target: for quoted arguments
        /// the program and arguments in one string, as always; otherwise the same as <paramref name="program"/>.
        /// </summary>
        public static bool TryReadStartInFolder(string line, out string file, out string program, out string arguments)
        {
            file = "";
            program = "";
            arguments = "";
            var match = regexStartInFolder.Match(line);

            if (!match.Success)
            {
                return false;
            }

            var folder = match.Groups[1].Value;
            var rest = match.Groups[2].Value;
            var quoted = regexProgramAndArguments.Match(rest);

            if (quoted.Success)
            {
                var quotedArguments = quoted.Groups[2].Value;
                program = JoinFolder(folder, quoted.Groups[1].Value);
                arguments = quotedArguments.Trim();
                file = Unquote("\"" + program + "\"" + quotedArguments);
                return true;
            }

            // More than one blank after the folder, or only blanks, is no program
            rest = rest.Trim();

            if (rest.Length == 0)
            {
                return false;
            }

            string name;
            string tail;

            if (rest[0] == '"')
            {
                // A quoted program followed by something that isn't a quoted argument
                var close = rest.IndexOf('"', 1);

                if (close < 0 || (close + 1 < rest.Length && !char.IsWhiteSpace(rest[close + 1])))
                {
                    return false;
                }

                name = rest.Substring(1, close - 1);
                tail = rest.Substring(close + 1);
            }
            else
            {
                // An unquoted program ends at the first blank, as START reads it
                var end = rest.IndexOfAny(new[] { ' ', '\t' });
                name = end < 0 ? rest : rest.Substring(0, end);
                tail = end < 0 ? "" : rest.Substring(end);

                if (name.Contains('"'))
                {
                    return false;
                }
            }

            if (name.Length == 0 || name[0] == '/')
            {
                return false;
            }

            program = JoinFolder(folder, name);
            arguments = tail.Trim();
            file = program;
            return true;
        }

        private static string JoinFolder(string folder, string program)
        {
            return folder.Length == 0 ? program : folder + "\\" + program;
        }

        public static bool IsCommand(string line, out string command)
        {
            return MatchesRegex(patternCommand, line, ShortcutType.Command, out command);
        }

        public static bool IsValidUrl(string line, out string url)
        {
            return MatchesRegex(patternUrl, line, ShortcutType.Url, out url);
        }

        public static bool MatchesRegex(string pattern, string line, ShortcutType shortcutType, out string result)
        {
            result = "";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(line);

            if (match.Success)
            {
                result = match.Groups[1].Value;
            }

            return match.Success;
        }

        public static bool MatchesRegex(string pattern, string line, ShortcutType shortcutType, out string result, out string openWithApp)
        {
            result = "";
            openWithApp = "";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(line);

            if (match.Success)
            {
                if (shortcutType == ShortcutType.HostsFile)
                {
                    if (match.Groups[1].Value != "" && match.Groups[2].Value != "")
                    {
                        openWithApp = match.Groups[1].Value;
                        result = match.Groups[2].Value;
                    }
                }
                else
                {
                    result = match.Groups[1].Value;
                }
            }

            return match.Success;
        }
    }
}
