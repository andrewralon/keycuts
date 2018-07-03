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
        public string Shortcut { get; set; }
        public string Destination { get; set; }
        public ShortcutType ShortcutType { get; set; }
        public string OpenWithApp { get; set; }

        public Bat(string shortcut, string destination, ShortcutType shortcutType, string openWithApp = "")
        {
            Shortcut = shortcut;
            Destination = destination;
            ShortcutType = shortcutType;
            OpenWithApp = openWithApp;
        }
    }
}
