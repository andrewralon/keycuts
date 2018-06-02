using keycuts.CLI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class UI
    {
        public static void CreateRightClickContextMenu()
        {
            RegistryStuff.CreateRightClickContextMenu();
        }

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
