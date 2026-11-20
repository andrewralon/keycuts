using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// Where a window is as the user sees it: on Windows, the bounds the desktop compositor draws (DWMWA_EXTENDED_FRAME_BOUNDS),
    /// without the invisible resize borders a resizable window has around them. Null elsewhere, and without a native window
    /// (headless tests), where there are no such borders to make up for.
    /// </summary>
    public static class VisibleFrame
    {
        private const int DwmwaExtendedFrameBounds = 9;

        /// <summary>The top-left corner of what is drawn, in screen pixels.</summary>
        public static PixelPoint? TopLeft(Window window)
        {
            return window.TryGetPlatformHandle()?.Handle is { } handle ? TopLeft(handle) : null;
        }

        /// <summary>The top-left corner of what is drawn for the native window <paramref name="hwnd"/>, in screen pixels.</summary>
        public static PixelPoint? TopLeft(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            {
                return null;
            }

            return DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var bounds, Marshal.SizeOf<NativeRect>()) == 0
                ? new PixelPoint(bounds.Left, bounds.Top)
                : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);
    }
}
