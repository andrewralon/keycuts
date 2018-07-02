using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public class KeycutArgs
    {
        public string Destination { get; set; }
        public string Shortcut { get; set; }
        public string OpenWithApp { get; set; }
        public string OutputFolder { get; set; }
        public bool Force { get; set; }

        public KeycutArgs(
            string destination = "", 
            string shortcut = "", 
            string openWithApp = "", 
            string outputFolder = "", 
            bool force = false)
        {
            if (destination != "")
            {
                Destination = destination;
            }
            if (shortcut != "")
            {
                Shortcut = shortcut;
            }
            if (openWithApp != "")
            {
                OpenWithApp = openWithApp;
            }
            if (outputFolder != "")
            {
                OutputFolder = outputFolder;
            }
            Force = force;
        }
    }
}
