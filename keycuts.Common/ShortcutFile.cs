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
        private static readonly string patternUrl = "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

        private static readonly Regex regexDescription = new Regex(@"^\s*REM\s+<description>(.*)</description>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex regexRemType = new Regex(@"^\s*REM\s+<type>(.*)</type>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex regexRemDestination = new Regex(@"^\s*REM\s+<destination>(.*)</destination>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly string Extension = ".bat";

        public string Path { get; set; }
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Destination { get; set; }
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

            foreach (var line in lines)
            {
                Command = line;

                if (ShortcutFile.IsEmittedCommand(line, out string emittedCommand))
                {
                    Destination = emittedCommand;
                    Type = ShortcutType.Command;
                    break;
                }
                else if (ShortcutFile.IsCLSIDKey(line, out string clsidKey))
                {
                    Destination = clsidKey;
                    Type = ShortcutType.CLSIDKey;
                    break;
                }
                else if (ShortcutFile.IsFolder(line, out string folder))
                {
                    Destination = Unquote(folder);
                    Type = ShortcutType.Folder;
                    break;
                }
                else if (ShortcutFile.IsHostsFile(line, out string hostsFile, out string openWithApp))
                {
                    Destination = hostsFile;
                    Type = ShortcutType.HostsFile;
                    OpenWithApp = openWithApp;
                    break;
                }
                else if (ShortcutFile.IsFile(line, out string newFile))
                {
                    Destination = Unquote(newFile);
                    Type = ShortcutType.File;
                    break;
                }
                else if (ShortcutFile.IsCommand(line, out string command))
                {
                    Destination = command;
                    Type = ShortcutType.Command;
                    break;
                }
                else if (ShortcutFile.IsValidUrl(line, out string url))
                {
                    Destination = url;
                    Type = ShortcutType.Url;
                    break;
                }

                Type = ShortcutType.Unknown;
            }

            if (!string.IsNullOrEmpty(OpenWithApp))
            {
                OpenWithApp = $"\"{OpenWithApp}\"";
            }

            // The comment lines hold the type and destination as the emitter was given them (only surrounding
            // whitespace is trimmed: Windows paths can't start or end with it), so they beat the regexes above,
            // which are the fallback for files that don't have them (the 0.1.x format).
            // They win even when the launch command disagrees (hand-edited %* arguments, a changed program, a legacy
            // file whose REM holds the expanded path). That is deliberate: this parser only feeds display and checks,
            // the .bat still runs its own command, and Command keeps the full launch line. Don't re-emit a file from
            // these fields without keeping Command, or hand-added arguments are lost.
            if (TryParseRemTypeAndDestination(allLines, out var remType, out var remDestination))
            {
                Type = remType;
                Destination = remDestination;
            }
        }

        /// <summary>
        /// Reads the first well-formed <c>REM &lt;type&gt;</c> and <c>REM &lt;destination&gt;</c> lines. Both must be present,
        /// the type must be a known one other than Unknown, and the destination must not be empty, or neither is used.
        /// </summary>
        public static bool TryParseRemTypeAndDestination(string[] lines, out ShortcutType type, out string destination)
        {
            type = ShortcutType.Unknown;
            destination = null;

            var typeText = FirstRemValue(lines, regexRemType);
            var destinationText = FirstRemValue(lines, regexRemDestination);

            // The name must match a defined value exactly (ignoring case): Enum.TryParse alone also accepts numbers
            // ("3" is Folder, "-1" is an undefined value whose name is "-1") and lists ("file,folder").
            if (typeText == null || string.IsNullOrEmpty(destinationText) ||
                !Enum.TryParse(typeText, true, out ShortcutType parsed) ||
                !Enum.IsDefined(typeof(ShortcutType), parsed) ||
                !string.Equals(parsed.ToString(), typeText, StringComparison.OrdinalIgnoreCase) ||
                parsed == ShortcutType.Unknown)
            {
                return false;
            }

            type = parsed;
            destination = destinationText;
            return true;
        }

        private static string FirstRemValue(string[] lines, Regex regex)
        {
            foreach (var line in lines)
            {
                var match = regex.Match(line);

                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Removes one leading and one trailing quote. The File and Folder patterns capture the launch line from its first
        /// quote to its last, and the emitter wraps the destination in one pair of quotes, so this is its exact inverse.
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
            // Not through MatchesRegex: its quote splicing would rewrite a command with quoted arguments.
            var match = regexCmdCommand.Match(line);
            command = match.Success ? match.Groups[1].Value : "";
            return match.Success;
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

                if (result.Contains("\" \""))
                {
                    Console.WriteLine("(raw):   " + result);

                    var quote1 = result.IndexOf('"', 1);
                    var quote2 = result.IndexOf('"', quote1 + 1);
                    var part1 = result.Substring(0, quote1);
                    var part2 = result.Substring(quote2 + 1);

                    //Console.WriteLine("quote1:  " + quote1);
                    //Console.WriteLine("quote2:  " + quote2);
                    //Console.WriteLine("part1:   " + part1);
                    //Console.WriteLine("part2:   " + part2);

                    result = part1 + "\\" + part2;
                }

                Console.WriteLine("result:  " + result);
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
