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
    /// is seen (<see cref="VisibleFrame"/>). The next move or resize is changed on its way in (<c>WM_WINDOWPOSCHANGING</c>) to land
    /// where the window is seen in the same place, so the new size and the corrected position are one update: a correction after the
    /// fact (<c>WM_WINDOWPOSCHANGED</c>) is a second one, which the compositor sometimes draws between, as a shake of 7 px. That
    /// later correction stays as a safety net for when the prediction was off. Windows only; elsewhere there are no such borders.
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

        private const int SmCxSizeFrame = 32;

        private const int SmCxPaddedBorder = 92;

        private PixelPoint? seenBefore;

        /// <summary>Whether the style the window is changing to has resize borders.</summary>
        private bool turningResizable;

        /// <summary>How far what is drawn is inside the window's rectangle while it has resize borders: measured, else worked out.</summary>
        private PixelPoint? resizableInset;

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

        /// <summary>Where the window has to go for what is seen to be back at <paramref name="seen"/>.</summary>
        public static PixelPoint MoveBack(PixelPoint windowTopLeft, PixelPoint seenNow, PixelPoint seen)
        {
            return new PixelPoint(windowTopLeft.X + seen.X - seenNow.X, windowTopLeft.Y + seen.Y - seenNow.Y);
        }

        /// <summary>
        /// Where the window's rectangle has to start for what is drawn to start at <paramref name="seen"/>, when what is drawn is
        /// <paramref name="inset"/> inside it (zero without resize borders).
        /// </summary>
        public static PixelPoint WindowTopLeftFor(PixelPoint seen, PixelPoint inset)
        {
            return new PixelPoint(seen.X - inset.X, seen.Y - inset.Y);
        }

        /// <summary>
        /// The invisible border of a resizable window on one side: the sizing frame and its padding, less the 1 px line that is drawn.
        /// Only a first guess; the real one is measured from the window as soon as it has been seen resizable.
        /// </summary>
        public static int InvisibleBorder(int sizeFrame, int paddedBorder)
        {
            return Math.Max(0, sizeFrame + paddedBorder - 1);
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

                    // Leaving resizable: this is the exact inset, for the next time the window turns resizable
                    if (HasResizeBorders(oldStyle) && seenBefore is { } seen && GetWindowRect(hwnd, out var rect))
                    {
                        resizableInset = new PixelPoint(seen.X - rect.Left, seen.Y - rect.Top);
                    }
                }
            }
            else if (msg == WmWindowPosChanging && seenBefore is { } wanted && !moving && !IsIconic(hwnd) && !IsZoomed(hwnd))
            {
                // Before the move or resize that follows a style change is applied: have it land with what is drawn where it was
                var inset = turningResizable ? resizableInset ?? FirstGuess(hwnd) : new PixelPoint(0, 0);
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
                    if (turningResizable)
                    {
                        resizableInset = new PixelPoint(now.X - rect.Left, now.Y - rect.Top);
                    }

                    // The safety net: what is drawn is somewhere else after all, so one more move puts it back
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

        private static PixelPoint FirstGuess(IntPtr hwnd)
        {
            var dpi = (int)GetDpiForWindow(hwnd);
            return dpi == 0
                ? new PixelPoint(0, 0)
                : new PixelPoint(InvisibleBorder(GetSystemMetricsForDpi(SmCxSizeFrame, (uint)dpi), GetSystemMetricsForDpi(SmCxPaddedBorder, (uint)dpi)), 0);
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
