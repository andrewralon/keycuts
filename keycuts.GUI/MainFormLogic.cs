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

        public void ClearDestination(MainWindow mainWindow)
        {
            mainWindow.TextboxDestination.Clear();
        }

        public void HandleNewDestination(MainWindow mainWindow, string destination)
        {
            SetDestination(mainWindow, destination);
            ActivateShortcutTextbox(mainWindow);
            ActivateThisWindow();
        }

        #region Private Methods

        private void SetDestination(MainWindow mainWindow, string destination)
        {
            // Follow the link (if it exists) and set the destination
            Shortcut.GetShortcutType(destination, out string newDestination);
            mainWindow.TextboxDestination.Text = newDestination;
            mainWindow.TextboxDestination.ScrollToEnd();
        }

        private void ActivateShortcutTextbox(MainWindow mainWindow)
        {
            // Focus on the shortcut name textbox
            mainWindow.TextboxShortcut.Focus();
        }

        private void ActivateThisWindow()
        {
            // Activate this window (normally keeps focus on whatever was previously active)
            var process = Process.GetCurrentProcess();
            var hwnd = process.MainWindowHandle;
            SetForegroundWindow(hwnd);
        }

        #endregion Private Methods

        #region External Methods

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        #endregion External Methods
    }
}
