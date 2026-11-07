using System;
using System.Windows;

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
    }
}
