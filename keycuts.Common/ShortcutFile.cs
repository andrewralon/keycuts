using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace keycuts.Common
{
    public class ShortcutFile
    {
        private static readonly string explorer = "\\explorer.exe\"";
        private static readonly string start = "START ";

        private static readonly string patternCLSID =       "\".+explorer.exe\" \"[shell:]?::({.+})\"";
        private static readonly string patternFolder =      "\".+explorer.exe\" (\".+\")";
        private static readonly string patternHostsFile =   "START \"\" \\/[BD] (\".+\") \"(.+hosts)\"$";
        private static readonly string patternFile =        "START \"\" \\/[BD] (\".+\")( \"(.+)\")?$";
        private static readonly string patternCommand =     "START \"\" \\/[BD] (\".+)?";
        private static readonly string patternUrl =         "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

        public static ShortcutType GetExistingShortcutTypeAndContents(string file, out string contents)
        {
            contents = file;
            var type = ShortcutType.Unknown;

            var allLines = File.ReadAllLines(file);
            var lines = allLines
                .Where(x => x.Contains(explorer) ||
                            x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            foreach (var line in lines)
            {
                if (IsCLSIDKey(line, out string clsidKey))
                {
                    contents = clsidKey;
                    type = ShortcutType.CLSIDKey;
                    break;
                }
                else if (IsFolder(line, out string folder))
                {
                    contents = folder;
                    type = ShortcutType.Folder;
                    break;
                }
                else if (IsHostsFile(line, out string hostsFile, out string openWithApp))
                {
                    contents = hostsFile;
                    type = ShortcutType.HostsFile;
                    break;
                }
                else if (IsFile(line, out string newFile))
                {
                    contents = newFile;
                    type = ShortcutType.File;
                    break;
                }
                else if (IsCommand(line, out string command))
                {
                    contents = command;
                    type = ShortcutType.Command;
                    break;
                }
                else if (IsValidUrl(line, out string url))
                {
                    contents = url;
                    type = ShortcutType.Url;
                    break;
                }
            }

            return type;
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
