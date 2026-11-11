using keycuts.Common;
using keycuts.GUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace keycuts.GUI
{
    public class MainFormLogic
    {
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
            // Follow the link (if it exists) and set the destination. A bad path shows as a field error, not a crash.
            var result = DestinationResolver.Resolve(destination);
            creator.Destination = result.Destination;
            creator.DestinationKind = result.Kind;
            creator.DestinationError = result.Error;
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
