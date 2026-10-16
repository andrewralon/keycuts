using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The side effects behind the manager's row actions (launching processes, the clipboard, deleting files).
    /// Tests replace it with a fake so nothing real is launched, copied or deleted.
    /// </summary>
    public interface IKeycutActions
    {
        /// <summary>Runs the keycut's .bat file.</summary>
        void Launch(string path);

        /// <summary>Opens the keycut's .bat file in Notepad.</summary>
        void Edit(string path);

        /// <summary>Puts the text on the clipboard. Throws if the clipboard can't be used.</summary>
        void CopyText(string text);

        /// <summary>Deletes the keycut file. Throws <see cref="KeycutRepositoryException"/> if that fails.</summary>
        Task DeleteAsync(Keycut keycut);
    }

    /// <summary>The production implementation, which uses the shell, the clipboard and the configured output folder.</summary>
    public class SystemKeycutActions : IKeycutActions
    {
        private const int ClipboardAttempts = 5;

        private const int ClipboardRetryDelayMs = 50;

        public void Launch(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public void Edit(string path)
        {
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });
        }

        public void CopyText(string text)
        {
            // Another process can hold the clipboard open for a moment.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return;
                }
                catch (COMException) when (attempt < ClipboardAttempts)
                {
                    Thread.Sleep(ClipboardRetryDelayMs);
                }
            }
        }

        public Task DeleteAsync(Keycut keycut)
        {
            return KeycutRepository.CreateDefault().DeleteAsync(keycut);
        }
    }
}
