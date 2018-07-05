using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Batmanager
{
    public class BatParseArg
    {
        public string RegexPattern { get; set; }
        public ShortcutType ShortcutType { get; set; }

        public BatParseArg()
        {
        }

        public BatParseArg(string regexPattern, ShortcutType shortcutType)
        {
            RegexPattern = regexPattern;
            ShortcutType = shortcutType;
        }
    }
}
