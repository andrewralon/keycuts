using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                //var args = lines[0].


                bat.Shortcut = Path.GetFileNameWithoutExtension(batFile);
                bat.Destination = "";
            }

            return bat;
        }
    }
}
