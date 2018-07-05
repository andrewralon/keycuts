using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace keycuts.Batmanager
{
    public class BatParser
    {
        public static string alphanumeric = "A-Za-z0-9";
        public static string alphanumericspecial = $"{alphanumeric}:\\.";//_%-";
        public static string alphanumericurl = $@"{alphanumeric}:/.";

        public static string URL = $"START [{alphanumericurl}]+";
        public static string FILE = $"START \"\" /B \"[{alphanumericspecial}]+";
        public static string FOLDER = $"\"%SystemRoot%\\explorer.exe\" \"[{alphanumericspecial}]+\"";
        public static string OPENWITHAPP = $"\"[{alphanumericspecial}]+\" \"[{alphanumericspecial}]+\"";

        public static List<BatParseArg> BatList = new List<BatParseArg>()
        {
            new BatParseArg(URL, ShortcutType.Url),
            new BatParseArg(FILE, ShortcutType.File),
            new BatParseArg(FOLDER, ShortcutType.Folder)
        };

        public static Bat Parse(string batFile)
        {
            var bat = new Bat();

            var lines = File.ReadAllLines(batFile).
                Where(t => !t.StartsWith("@ECHO") &&
                    !t.StartsWith("@echo") &&
                    !t.StartsWith("REM") &&
                    !t.StartsWith("rem") &&
                    !t.StartsWith("EXIT") &&
                    !t.StartsWith("exit") &&
                    !string.IsNullOrEmpty(t))
                .ToList();

            if (lines.Any())
            {
                bat.Shortcut = Path.GetFileNameWithoutExtension(batFile);
                bat.Destination = lines[0];
                //bat.ShortcutType = ShortcutType.Unknown;
                //bat.OpenWithApp = "";

                if (lines[0].Contains("\\explorer.exe"))
                {
                    // It's a folder!
                    bat.ShortcutType = ShortcutType.Folder;
                }
                else if (lines[0].Substring(0, 5).ToUpper() == "START")
                {
                    // It's NOT a folder! -- Could be File, Url, HostsFile, or CLSIDKey


                    bat.ShortcutType = ShortcutType.File;
                }

                //foreach (var line in lines)
                //{
                //    foreach (var arg in BatList)
                //    {
                //        if (HasValue(line, arg.RegexPattern))
                //        {
                //            bat.ShortcutType = arg.ShortcutType;
                //            break;
                //        }

                //        //var value = GetValue(line, arg.RegexPattern);
                //        //if (value != "")
                //        //{
                //        //    if (Enum.TryParse(value, out ShortcutType shortcutType))
                //        //    {
                //        //        bat.ShortcutType = shortcutType;
                //        //    }
                //        //}
                //    }
                //}
            }

            return bat;
        }

        public static bool HasValue(string line, string pattern)
        {
            var regex = new Regex(pattern);
            var match = regex.Match(line);

            return match.Success;
        }

        public static string GetValue(string line, string pattern)
        {
            var value = "";
            var regex = new Regex(pattern);
            var match = regex.Match(line);

            if (match.Success)
            {
                value = match.Value;
            }

            return value;
        }
    }
}
