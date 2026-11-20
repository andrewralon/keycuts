using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace keycuts.GUI.ViewModels
{
    /// <summary>The Avalonia app's clipboard for <see cref="SystemKeycutActions"/> (docs/macos-port.md, correction 15).</summary>
    public partial class SystemKeycutActions
    {
        /// <summary>
        /// The main window's clipboard. Avalonia's is asynchronous and the action isn't, so the copy is started and not awaited: a
        /// failure after it starts isn't reported. The Manager's copy (Phase 3.7) decides whether its message needs more.
        /// </summary>
        private static partial void SetClipboardText(string text)
        {
            var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var clipboard = window?.Clipboard ?? throw new InvalidOperationException("The clipboard isn't available.");
            _ = clipboard.SetTextAsync(text);
        }
    }
}
