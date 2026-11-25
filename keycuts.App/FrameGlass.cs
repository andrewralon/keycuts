using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// Takes the system's border and drop shadow off the window while its corner is torn. Windows draws both around the window's
    /// whole rectangle, whatever the page shows inside it, so the torn corner (see <c>TornCornerShape</c>) showed a square outline
    /// and a square shadow around nothing. Avalonia asks for them by keeping the window's non-client rendering enabled; with it
    /// disabled the system draws neither, and the page's own border follows the tear instead. Avalonia sets the policy again each
    /// time it shows the window (hiding to the tray and back, the first show) and ends each of those with a frame-changed
    /// <c>SetWindowPos</c>, so it is set again whenever one arrives. Not while the plain rectangle is wanted (high contrast): there
    /// the system border stays. Windows only.
    /// </summary>
    public sealed class FrameGlass
    {
        private const uint WmWindowPosChanged = 0x0047;

        private const uint SwpFrameChanged = 0x0020;

        private const int DwmwaNcRenderingPolicy = 2;

        private const int NcRenderingDisabled = 1;

        /// <summary>What Avalonia sets for a window that extends into its frame: the system draws the border and shadow.</summary>
        private const int NcRenderingEnabled = 2;

        private bool bare;

        public static FrameGlass Attach(Window window)
        {
            var glass = new FrameGlass();
            if (OperatingSystem.IsWindows())
            {
                Win32Properties.AddWndProcHookCallback(window, glass.WndProc);
            }

            return glass;
        }

        /// <summary>Whether a <c>WINDOWPOS</c> flags value says the frame changed.</summary>
        public static bool IsFrameChange(uint flags)
        {
            return (flags & SwpFrameChanged) != 0;
        }

        /// <summary>
        /// True while the system's border and shadow are off. Set at once on a window that is open, and applied to the window's
        /// first frame otherwise.
        /// </summary>
        public void SetBare(Window window, bool value)
        {
            if (bare == value)
            {
                return;
            }

            bare = value;
            if (OperatingSystem.IsWindows() && window.TryGetPlatformHandle()?.Handle is { } hwnd && hwnd != IntPtr.Zero)
            {
                Apply(hwnd);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // WINDOWPOS: the window, the one it goes after, x, y, cx, cy, then the flags
            if (bare && msg == WmWindowPosChanged && IsFrameChange((uint)Marshal.ReadInt32(lParam, IntPtr.Size * 2 + 4 * sizeof(int))))
            {
                Apply(hwnd);
            }

            return IntPtr.Zero;
        }

        private void Apply(IntPtr hwnd)
        {
            var policy = bare ? NcRenderingDisabled : NcRenderingEnabled;
            DwmSetWindowAttribute(hwnd, DwmwaNcRenderingPolicy, ref policy, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
