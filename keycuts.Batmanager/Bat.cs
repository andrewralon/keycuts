using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Batmanager
{
    public class Bat
    {
        public string Path { get; set; }
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Destination { get; set; }
        public string OpenWithApp { get; set; }
        public string Command { get; set; }

        public Bat()
        {
        }

        public Bat(string path, string shortcut, string command, string destination, ShortcutType type, string openWithApp = "")
        {
            Path = path;
            Shortcut = shortcut;
            Type = type;
            Destination = destination;
            OpenWithApp = openWithApp;
            Command = command;
        }
    }
}
