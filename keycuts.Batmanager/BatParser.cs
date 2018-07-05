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
        public static string FILE = "START \"\" /B \"";
        //var start = "START \"\" /B \"{0}\"";

        public static Bat Parse(string batFile)
        {
            var bat = new Bat();

            var lines = File.ReadAllLines(batFile).
                Where(t => (!t.StartsWith("@ECHO") &&
                    !t.StartsWith("@echo") &&
                    !t.StartsWith("REM") &&
                    !t.StartsWith("rem") &&
                    !t.StartsWith("EXIT") &&
                    !t.StartsWith("exit") &&
                    !string.IsNullOrEmpty(t))) // ||
                    //(t.Contains("<") &&
                    //t.Contains(">")))
                .ToList();

            var patterns = new List<string>
            {
                @"<type>[A-Za-z]+<\/type>", // ShortcutType
                @"<shortcut>[A-Za-z0-9:\\.]+<\/type>",
                @"<file>[A-Za-z0-9:\\.]+<\/file>"
            };

            if (lines.Any())
            {
                bat.Shortcut = Path.GetFileNameWithoutExtension(batFile);
                bat.Destination = lines[0];
                bat.ShortcutType = ShortcutType.Unknown;

                //foreach (var line in lines)
                //{
                //    foreach (var pattern in patterns)
                //    {
                //        var value = GetValue(line, pattern);
                //        if (value != "")
                //        {
                //            if (Enum.TryParse(value, out ShortcutType shortcutType))
                //            {
                //                bat.ShortcutType = shortcutType;
                //            }
                //        }
                //    }
                //}

                //bat.Destination = "";
                bat.OpenWithApp = "";
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
