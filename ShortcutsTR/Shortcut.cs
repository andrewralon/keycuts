using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Shortcut
    {
        public string Destination { get; private set; }

        public string Name { get; private set; }

        public ShortcutType Type { get; private set; } = ShortcutType.Unknown;

        public Shortcut()
        {
        }

        public Shortcut(string destination, string name)
        {
            Destination = destination;
            Name = name;
        }
    }
}
