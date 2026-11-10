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
        public const double CreatorWidth = 440;

        public const double ManagerDefaultWidth = 960;

        public const double ManagerDefaultHeight = 700;

        public const double ManagerMinWidth = 720;

        public const double ManagerMinHeight = 480;

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

        #region External Methods

        private const int MonitorDefaultToNearest = 2;

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
