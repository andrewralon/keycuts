using keycuts.Common;
using keycuts.GUI.ViewModels;
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
        public void SaveSettings(Settings settings)
        {
            settings?.SaveSettings();
        }

        /// <summary>
        /// Sets the creator's destination; the creator view focuses the name field when it is shown with a destination set.
        /// </summary>
        public void HandleNewDestination(CreatorViewModel creator, string destination)
        {
            SetDestination(creator, destination);
            ActivateThisWindow();
        }

        #region Private Methods

        private void SetDestination(CreatorViewModel creator, string destination)
        {
            // Follow the link (if it exists) and set the destination
            var type = Shortcut.GetShortcutTypeFromDestination(destination, out string newDestination);
            creator.Destination = newDestination;
            creator.DestinationKind = KeycutKinds.FromShortcutType(type);
        }

        public void ActivateThisWindow()
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
