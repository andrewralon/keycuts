using System;
using System.Windows;
using System.Windows.Interop;

namespace keycuts.GUI
{
    /// <summary>
    /// Keeps Windows from painting its own caption over the tab bar. The window draws its whole frame itself (WindowChrome with no
    /// glass frame), but Windows still sends it the undocumented <c>WM_NCUAHDRAWCAPTION</c> and <c>WM_NCUAHDRAWFRAME</c>, and
    /// <c>DefWindowProc</c> answers them by painting the classic (unthemed) caption buttons and a frame line straight onto the window,
    /// over the tabs. WPF doesn't know those pixels changed, so they stay until it next redraws that part, even across a change of tab.
    /// It showed after a drag of the Manager from the bar, most often one that reached the top of the screen (on Windows 10, 4 runs in 5;
    /// 0 in 5 with this hook); <c>WM_NCUAHDRAWCAPTION</c> arrives with the <c>WM_NCLBUTTONDOWN</c> that starts the drag. Nothing here
    /// wants the system's caption, so both are answered without it; Chromium does the same for its own frame.
    /// </summary>
    public sealed class NoSystemCaptionHook
    {
        public const int WmNcUahDrawCaption = 0x00AE;

        public const int WmNcUahDrawFrame = 0x00AF;

        // Kept for as long as this object lives, which its window holds on to
        private readonly HwndSourceHook hook = WndProc;

        public NoSystemCaptionHook(Window window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                source.AddHook(hook);
            }
            else
            {
                window.SourceInitialized += (sender, e) =>
                {
                    if (PresentationSource.FromVisual(window) is HwndSource initialized)
                    {
                        initialized.AddHook(hook);
                    }
                };
            }
        }

        /// <summary>Whether a message asks the window to paint the system's caption or frame.</summary>
        public static bool IsSystemCaptionPaint(int msg)
        {
            return msg == WmNcUahDrawCaption || msg == WmNcUahDrawFrame;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (IsSystemCaptionPaint(msg))
            {
                handled = true;
            }

            return IntPtr.Zero;
        }
    }
}
