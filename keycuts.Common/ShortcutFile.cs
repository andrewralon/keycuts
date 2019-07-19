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

        private static readonly string patternCLSID =       "\".+explorer.exe\" \"[shell:]?::({.+})\"";
        private static readonly string patternFolder =      "\".+explorer.exe\" (\".+\")";
        private static readonly string patternHostsFile =   "START \"\" \\/[BD] (\".+\") \"(.+hosts)\"$";
        private static readonly string patternFile =        "START \"\" \\/[BD] (\".+\")( \"(.+)\")?$";
        private static readonly string patternCommand =     "START \"\" \\/[BD] (\".+)?";
        private static readonly string patternUrl =         "START [\"\" ]?[ \\/{BD}]?[ \"]?(.+)[\"]?$";

        public string Path { get; set; }
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Destination { get; set; }
        public string OpenWithApp { get; set; }
        public string Command { get; set; }

        public ShortcutFile()
        {
        }

        public ShortcutFile(string file)
        {
            var allLines = File.ReadAllLines(file);
            var lines = allLines
                .Where(x => x.Contains(explorer) ||
                            x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            Path = file;
            Shortcut = System.IO.Path.GetFileNameWithoutExtension(file);

            foreach (var line in lines)
            {
                Command = line;

                if (ShortcutFile.IsCLSIDKey(line, out string clsidKey))
                {
                    Destination = clsidKey;
                    Type = ShortcutType.CLSIDKey;
                    break;
                }
                else if (ShortcutFile.IsFolder(line, out string folder))
                {
                    Destination = folder;
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
                    Destination = newFile;
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
