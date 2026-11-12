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
        /// Sets the creator's target; the creator view focuses the name field when it is shown with a target set.
        /// </summary>
        public void HandleNewTarget(CreatorViewModel creator, string target)
        {
            SetTarget(creator, target);
            ActivateThisWindow();
        }

        #region Private Methods

        private void SetTarget(CreatorViewModel creator, string target)
        {
            // Follow the link (if it exists) and set the target. A bad path shows as a field error, not a crash.
            var result = TargetResolver.Resolve(target);
            creator.Target = result.Target;
            creator.TargetKind = result.Kind;
            creator.TargetError = result.Error;
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
