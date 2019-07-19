using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public class Bat
    {
        private readonly string explorer = "\\explorer.exe\"";
        private readonly string start = "START ";

        public string Path { get; set; }
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Destination { get; set; }
        public string OpenWithApp { get; set; }
        public string Command { get; set; }

        public Bat()
        {
        }

        public Bat(string batFile)
        {
            var allLines = File.ReadAllLines(batFile);
            var lines = allLines
                .Where(x => x.Contains(explorer) ||
                            x.StartsWith(start, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            Path = batFile;
            Shortcut = System.IO.Path.GetFileNameWithoutExtension(batFile);

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
                else if (ShortcutFile.IsFile(line, out string file))
                {
                    Destination = file;
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
    }
}
