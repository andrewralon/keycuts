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
        public static void OpenSettings(SettingsWindow settingsWindow)
        {
            settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }

        public static void SaveSettings(Settings settings)
        {
            settings?.SaveSettings();
        }

        public static void OpenOutputFolder()
        {
            var defaultFolder = RegistryStuff.GetOutputFolder(Runner.DefaultOutputFolder);
            Process.Start(defaultFolder);
        }

        public static void ActivateShortcutTextbox(MainWindow mainWindow, string file)
        {
            // Follow the link (if it exists) and set the path textbox
            Shortcut.GetShortcutType(file, out string newFile);
            mainWindow.Destination = newFile;

            // Focus on the shortcut name textbox
            mainWindow.TextboxShortcut.Focus();
        }

        public static void ActivateThisWindow()
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
