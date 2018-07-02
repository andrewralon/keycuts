using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class Settings
    {
        private Runner runner;

        public string OutputFolder { get; set; }

        public bool ForceOverwrite { get; set; }

        public bool RightClickContextMenus { get; set; }

        public Settings()
        {
            runner = new Runner();
        }

        public void LoadSettings()
        {
            OutputFolder = runner.GetOutputFolder();
            ForceOverwrite = runner.GetForceOverwrite();
            RightClickContextMenus = runner.GetRightClickContextMenu();
        }

        public void SaveSettings()
        {
            runner.SetOutputFolder(OutputFolder);
            runner.SetForceOverwrite(ForceOverwrite);
            runner.SetRightClickContextMenu(RightClickContextMenus);
        }
    }
}
