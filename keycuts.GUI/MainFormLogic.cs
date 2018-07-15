using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class MainFormLogic
    {
        public void OpenSettings(SettingsWindow settingsWindow)
        {
            settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }

        public void SaveSettings(Settings settings)
        {
            settings?.SaveSettings();
        }

        public void OpenBatmanager()
        {
            Runner.OpenBatmanager();
        }

        public void ActivateShortcutTextbox(MainWindow mainWindow, string file)
        {
            // Follow the link (if it exists) and set the path textbox
            Shortcut.GetShortcutType(file, out string newFile);
            mainWindow.Destination = newFile;

            // Focus on the shortcut name textbox
            mainWindow.TextboxShortcut.Focus();
        }

        public void ActivateThisWindow()
        {
            var process = Process.GetCurrentProcess();
            var hwnd = process.MainWindowHandle;
            SetForegroundWindow(hwnd);
        }

        #region External Methods

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        #endregion External Methods
    }
}
