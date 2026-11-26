using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// Where a window's page is as the user sees it: on Windows, the top-left corner of the client area, in screen pixels, where
    /// the page starts. A resizable window has invisible resize borders between this and the window's rectangle, which come and
    /// go with it. (The bounds the compositor reports, DWMWA_EXTENDED_FRAME_BOUNDS, are not used: they follow whether the system
    /// draws the window's frame, see <see cref="FrameGlass"/>.) Null elsewhere, and without a native window (headless tests), where
    /// there are no such borders to make up for.
    /// </summary>
    public static class VisibleFrame
    {
        /// <summary>The top-left corner of the page, in screen pixels.</summary>
        public static PixelPoint? TopLeft(Window window)
        {
            return window.TryGetPlatformHandle()?.Handle is { } handle ? TopLeft(handle) : null;
        }

        /// <summary>The top-left corner of the page of the native window <paramref name="hwnd"/>, in screen pixels.</summary>
        public static PixelPoint? TopLeft(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            {
                return null;
            }

            var origin = new NativePoint();

            return ClientToScreen(hwnd, ref origin) ? new PixelPoint(origin.X, origin.Y) : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
    }
}
