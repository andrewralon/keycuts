using System;
using System.Collections.Generic;
using System.Text;

namespace keycuts.Common
{
    public class KeycutArgs
    {
        public string Target { get; set; }
        public string Shortcut { get; set; }
        public string OpenWithApp { get; set; }
        public string OutputFolder { get; set; }
        public bool Force { get; set; }
        public string Description { get; set; } = "";

        /// <summary>Save a file or folder target with environment variables (#31), e.g. %USERPROFILE%\Desktop.</summary>
        public bool UseEnvironmentVariables { get; set; }

        /// <summary>The variables to use; null reads them from this machine (<see cref="Common.PathVariables.GetDefault"/>).</summary>
        public IReadOnlyList<PathVariable> PathVariables { get; set; }

        public KeycutArgs(
            string target = "",
            string shortcut = "",
            string openWithApp = "",
            string outputFolder = "",
            bool force = false)
        {
            if (target != "")
            {
                Target = target;
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
