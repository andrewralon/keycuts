using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace keycuts.GUI
{
    /// <summary>
    /// Window sizes per shell mode (in DIPs) and the on-screen clamping used when the window grows.
    /// </summary>
    public static class WindowLayout
    {
        /// <summary>The Creator's width, and Settings' once it is a screen: what the tab bar needs plus about 60 of empty bar. A test keeps it equal to <c>MainWidth</c> in Tokens.xaml.</summary>
        public const double CreatorWidth = 600;

        /// <summary>
        /// The Manager and Settings open 768 wide (the owner's pick, 2026-11-17; it was 960 × 544): just wide enough for the
        /// Manager's Runs column (<c>ManagerView.RunsColumnMinTableWidth</c>, 720, plus the 48 around the table).
        /// </summary>
        public const double ManagerDefaultWidth = 768;

        /// <summary>The smallest height.</summary>
        public const double ManagerDefaultHeight = ManagerMinHeight;

        public const double ManagerMinWidth = 720;

        /// <summary>
        /// 470, not 480: the Manager's table is the header and 5 whole rows with 24 under them, so the torn corner bites into a full
        /// last row (480 left 34, a part row's worth more). The table is always whole rows; at other heights the rest goes under it.
        /// </summary>
        public const double ManagerMinHeight = 470;

        /// <summary>
        /// Shrinks the bounds to fit the work area, then slides them so they lie fully inside it.
        /// </summary>
        public static Rect ClampToWorkArea(Rect bounds, Rect workArea)
        {
            var width = Math.Min(bounds.Width, workArea.Width);
            var height = Math.Min(bounds.Height, workArea.Height);
            var left = Math.Max(workArea.Left, Math.Min(bounds.Left, workArea.Right - width));
            var top = Math.Max(workArea.Top, Math.Min(bounds.Top, workArea.Bottom - height));
            return new Rect(left, top, width, height);
        }

        /// <summary>The work area of the monitor the window is on, in DIPs. Falls back to the primary work area.</summary>
        public static Rect GetWorkAreaInDips(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var monitor = handle == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(handle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                return SystemParameters.WorkArea;
            }

            var dpi = VisualTreeHelper.GetDpi(window);
            return new Rect(
                info.Work.Left / dpi.DpiScaleX,
                info.Work.Top / dpi.DpiScaleY,
                (info.Work.Right - info.Work.Left) / dpi.DpiScaleX,
                (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
        }

        /// <summary>
        /// The thickness of the frame Windows gives a resizable window (the sizing border plus its padding), in DIPs, at the window's
        /// DPI: 8 at 100%. <c>SystemParameters.WindowResizeBorderThickness</c> leaves the padding out (4), and so is not what a maximized
        /// window overhangs by.
        /// </summary>
        public static Thickness GetFrameThickness(Window window)
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var pixelsX = (uint)Math.Round(dpi.PixelsPerInchX);
            var pixelsY = (uint)Math.Round(dpi.PixelsPerInchY);
            var horizontal = (GetSystemMetricsForDpi(SmCxSizeFrame, pixelsX) + GetSystemMetricsForDpi(SmCxPaddedBorder, pixelsX)) / dpi.DpiScaleX;
            var vertical = (GetSystemMetricsForDpi(SmCySizeFrame, pixelsY) + GetSystemMetricsForDpi(SmCxPaddedBorder, pixelsY)) / dpi.DpiScaleY;
            return new Thickness(horizontal, vertical, horizontal, vertical);
        }

        #region External Methods

        private const int MonitorDefaultToNearest = 2;

        private const int SmCxSizeFrame = 32;

        private const int SmCySizeFrame = 33;

        private const int SmCxPaddedBorder = 92;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public int Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        #endregion External Methods
    }
}
