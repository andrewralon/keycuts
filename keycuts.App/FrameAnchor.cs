using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// Keeps the window where it is seen when it turns resizable or back (the Manager and Settings, the Creator). A resizable window
    /// has resize borders (8 px a side at 100 %) between its rectangle and its page, a fixed one only the 1 px border, and Windows
    /// keeps the window's position when they come or go, so the page would jump 7 px. When the resize-border style changes, this
    /// notes where the page is (<see cref="VisibleFrame"/>). The next move or resize is changed on its way in
    /// (<c>WM_WINDOWPOSCHANGING</c>) to land where the page stays in the same place, so the new size and the corrected position are
    /// one update: a correction after the fact (<c>WM_WINDOWPOSCHANGED</c>) is a second one, which the compositor sometimes draws
    /// between, as a shake of 7 px. That later correction stays as a safety net for when the prediction was off. Windows only;
    /// elsewhere there are no such borders.
    /// </summary>
    public sealed class FrameAnchor
    {
        private const uint WmStyleChanging = 0x007C;

        private const uint WmWindowPosChanging = 0x0046;

        private const uint WmWindowPosChanged = 0x0047;

        private const int GwlStyle = -16;

        private const int WsThickFrame = 0x00040000;

        private const uint SwpNoSize = 0x0001;

        private const uint SwpNoMove = 0x0002;

        private const uint SwpNoZOrder = 0x0004;

        private const uint SwpNoActivate = 0x0010;

        private const int SmCxBorder = 5;

        private const int SmCyBorder = 6;

        private const int SmCxSizeFrame = 32;

        private const int SmCxPaddedBorder = 92;

        private PixelPoint? seenBefore;

        /// <summary>Whether the style the window is changing to has resize borders.</summary>
        private bool turningResizable;

        /// <summary>How far the page is inside the window's rectangle while it has resize borders: measured, else worked out.</summary>
        private PixelPoint? resizableInset;

        /// <summary>The same without them.</summary>
        private PixelPoint? fixedInset;

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

        /// <summary>Whether a style has the resize borders.</summary>
        public static bool HasResizeBorders(int style)
        {
            return (style & WsThickFrame) != 0;
        }

        /// <summary>Where the window has to go for the page to be back at <paramref name="seen"/>.</summary>
        public static PixelPoint MoveBack(PixelPoint windowTopLeft, PixelPoint seenNow, PixelPoint seen)
        {
            return new PixelPoint(windowTopLeft.X + seen.X - seenNow.X, windowTopLeft.Y + seen.Y - seenNow.Y);
        }

        /// <summary>
        /// Where the window's rectangle has to start for the page to start at <paramref name="seen"/>, when the page is
        /// <paramref name="inset"/> inside it.
        /// </summary>
        public static PixelPoint WindowTopLeftFor(PixelPoint seen, PixelPoint inset)
        {
            return new PixelPoint(seen.X - inset.X, seen.Y - inset.Y);
        }

        /// <summary>
        /// How far the page is inside a window's rectangle, from the system's metrics: with resize borders the sizing frame and its
        /// padding at the sides, otherwise the 1 px border; the top is the thin border either way (the page extends into the rest).
        /// Only a first guess; the real one is measured from the window as soon as it has been seen in that state.
        /// </summary>
        public static PixelPoint ClientInset(bool resizable, int border, int sizeFrame, int paddedBorder, int borderTop)
        {
            return new PixelPoint(resizable ? sizeFrame + paddedBorder : border, borderTop);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmStyleChanging && wParam.ToInt64() == GwlStyle)
            {
                // STYLESTRUCT: the old style, then the new one
                var oldStyle = Marshal.ReadInt32(lParam);
                var newStyle = Marshal.ReadInt32(lParam, sizeof(int));
                if (TogglesResizeBorders(oldStyle, newStyle))
                {
                    seenBefore = VisibleFrame.TopLeft(hwnd);
                    turningResizable = HasResizeBorders(newStyle);

                    // Leaving a state: this is its exact inset, for the next time the window comes back to it
                    if (seenBefore is { } seen && GetWindowRect(hwnd, out var rect))
                    {
                        Remember(HasResizeBorders(oldStyle), new PixelPoint(seen.X - rect.Left, seen.Y - rect.Top));
                    }
                }
            }
            else if (msg == WmWindowPosChanging && seenBefore is { } wanted && !moving && !IsIconic(hwnd) && !IsZoomed(hwnd))
            {
                // Before the move or resize that follows a style change is applied: have it land with the page where it was
                var inset = (turningResizable ? resizableInset : fixedInset) ?? Guess(hwnd, turningResizable);
                var target = WindowTopLeftFor(wanted, inset);
                var offset = IntPtr.Size * 2;
                Marshal.WriteInt32(lParam, offset, target.X);
                Marshal.WriteInt32(lParam, offset + sizeof(int), target.Y);
                Marshal.WriteInt32(lParam, offset + 4 * sizeof(int), Marshal.ReadInt32(lParam, offset + 4 * sizeof(int)) & ~(int)SwpNoMove);
            }
            else if (msg == WmWindowPosChanged && seenBefore is { } seen && !moving)
            {
                seenBefore = null;
                if (VisibleFrame.TopLeft(hwnd) is { } now && GetWindowRect(hwnd, out var rect))
                {
                    Remember(turningResizable, new PixelPoint(now.X - rect.Left, now.Y - rect.Top));

                    // The safety net: the page is somewhere else after all, so one more move puts it back
                    if (now != seen)
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
            }

            return IntPtr.Zero;
        }

        private void Remember(bool resizable, PixelPoint inset)
        {
            if (resizable)
            {
                resizableInset = inset;
            }
            else
            {
                fixedInset = inset;
            }
        }

        private static PixelPoint Guess(IntPtr hwnd, bool resizable)
        {
            var dpi = GetDpiForWindow(hwnd);
            if (dpi == 0)
            {
                return new PixelPoint(0, 0);
            }

            return ClientInset(resizable, GetSystemMetricsForDpi(SmCxBorder, dpi), GetSystemMetricsForDpi(SmCxSizeFrame, dpi),
                GetSystemMetricsForDpi(SmCxPaddedBorder, dpi), GetSystemMetricsForDpi(SmCyBorder, dpi));
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

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    }
}
