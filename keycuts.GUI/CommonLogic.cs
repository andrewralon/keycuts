using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class CommonLogic
    {
        public void SetOutputFolder(string outputFolder)
        {
            var runner = new Runner();
            runner.SetOutputFolder(outputFolder);
        }
    }
}
