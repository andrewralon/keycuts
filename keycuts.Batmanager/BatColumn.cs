using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Batmanager
{
    public class BatColumn
    {
        public string Shortcut { get; set; }
        public ShortcutType Type { get; set; }
        public string Destination { get; set; }
        public string OpenWithApp { get; set; }

        public BatColumn()
        {
        }

        public BatColumn(Bat bat)
        {
            Shortcut = bat.Shortcut;
            Type = bat.Type;
            Destination = bat.Destination;
            OpenWithApp = bat.OpenWithApp;
        }
    }
}
