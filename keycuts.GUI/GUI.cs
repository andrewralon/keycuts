using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class GUI
    {
        public static void ActivateThisWindow()
        {
            Process process = Process.GetCurrentProcess();
            IntPtr hwnd = process.MainWindowHandle;
            SetForegroundWindow(hwnd);
        }

        #region External Methods

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        #endregion External Methods
    }
}
