using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace keycuts.GUI.ViewModels
{
    /// <summary>The WPF app's clipboard for <see cref="SystemKeycutActions"/> (docs/macos-port.md, correction 15).</summary>
    public partial class SystemKeycutActions
    {
        private const int ClipboardAttempts = 5;

        private const int ClipboardRetryDelayMs = 50;

        private static partial void SetClipboardText(string text)
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
    }
}
