using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class FormLogic
    {
        public static void OpenSettings(SettingsWindow settings)
        {
            settings.Show();
        }

        public static void OpenOutputFolder()
        {
            var defaultFolder = RegistryStuff.GetOutputFolder(Runner.DefaultOutputFolder);

            Process.Start(defaultFolder);
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
