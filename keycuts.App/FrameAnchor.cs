using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// Keeps the window where it is seen when it turns resizable or back (the Manager and Settings, the Creator). A resizable window
    /// has invisible resize borders (7 px a side at 100 %) outside what is drawn, and Windows keeps the window's position when
    /// they come or go, so what the user sees would jump 7 px. When the resize-border style changes, this notes where the window
    /// is seen (<see cref="VisibleFrame"/>), and as soon as Windows has applied the new frame, in the same message, before anything
    /// is drawn, moves the window back by the difference. Windows only; elsewhere there are no such borders.
    /// </summary>
    public sealed class FrameAnchor
    {
        private const uint WmStyleChanging = 0x007C;

        private const uint WmWindowPosChanged = 0x0047;

        private const int GwlStyle = -16;

        private const int WsThickFrame = 0x00040000;

        private const uint SwpNoSize = 0x0001;

        private const uint SwpNoZOrder = 0x0004;

        private const uint SwpNoActivate = 0x0010;

        private PixelPoint? seenBefore;

        private bool moving;

        public static void Attach(Window window)
        {
            if (OperatingSystem.IsWindows())
            {
                Win32Properties.AddWndProcHookCallback(window, new FrameAnchor().WndProc);
            }
        }

        /// <summary>Whether a style change adds or takes away the resize borders.</summary>
        public static bool TogglesResizeBorders(int oldStyle, int newStyle)
        {
            return ((oldStyle ^ newStyle) & WsThickFrame) != 0;
        }

        /// <summary>Where the window has to go for what is seen to be back at <paramref name="seen"/>.</summary>
        public static PixelPoint MoveBack(PixelPoint windowTopLeft, PixelPoint seenNow, PixelPoint seen)
        {
            return new PixelPoint(windowTopLeft.X + seen.X - seenNow.X, windowTopLeft.Y + seen.Y - seenNow.Y);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmStyleChanging && wParam.ToInt64() == GwlStyle)
            {
                // STYLESTRUCT: the old style, then the new one
                if (TogglesResizeBorders(Marshal.ReadInt32(lParam), Marshal.ReadInt32(lParam, sizeof(int))))
                {
                    seenBefore = VisibleFrame.TopLeft(hwnd);
                }
            }
            else if (msg == WmWindowPosChanged && seenBefore is { } seen && !moving)
            {
                seenBefore = null;
                if (VisibleFrame.TopLeft(hwnd) is { } now && now != seen && GetWindowRect(hwnd, out var rect))
                {
                    var target = MoveBack(new PixelPoint(rect.Left, rect.Top), now, seen);
                    moving = true;
                    try
                    {
                        SetWindowPos(hwnd, IntPtr.Zero, target.X, target.Y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
                    }
                    finally
                    {
                        moving = false;
                    }
                }
            }

            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
