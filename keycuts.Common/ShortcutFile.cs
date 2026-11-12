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
                else if (ShortcutFile.IsStartInFolder(line, out string fileInFolder))
                {
                    Target = fileInFolder;
                    Type = ShortcutType.File;
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

        /// <summary>Returns the trimmed text of the first well-formed description line, or empty.</summary>
        public static string ParseDescription(string[] lines)
        {
            foreach (var line in lines)
            {
                var match = regexDescription.Match(line);

                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
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
        /// Reads the 0.1.x launch line for a program, <c>START "" /D "&lt;folder&gt;" "&lt;program&gt;" ["&lt;argument&gt;" ...]</c>.
        /// The folder and program are joined into one path (just the program when the folder is empty), and any quoted
        /// arguments are kept after it, unquoted like a File target: <c>C:\Tools\app.exe" "arg</c>. The /B line the
        /// emitter writes for that reads back the same. A line with an unquoted program, or any unquoted argument (even
        /// one between quoted arguments), is left to the other patterns.
        /// </summary>
        public static bool IsStartInFolder(string line, out string file)
        {
            file = "";
            var match = regexStartInFolder.Match(line);

            if (!match.Success)
            {
                return false;
            }

            var folder = match.Groups[1].Value;
            var program = regexProgramAndArguments.Match(match.Groups[2].Value);

            if (!program.Success)
            {
                return false;
            }

            var arguments = program.Groups[2].Value;
            var path = folder.Length == 0 ? program.Groups[1].Value : folder + "\\" + program.Groups[1].Value;
            file = Unquote("\"" + path + "\"" + arguments);
            return true;
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
