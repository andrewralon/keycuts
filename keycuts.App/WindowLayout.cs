using System;
using Avalonia;

namespace keycuts.GUI
{
    /// <summary>
    /// Window sizes per shell mode (in DIPs) and the on-screen clamping used when the window grows (WPF's WindowLayout).
    /// </summary>
    public static class WindowLayout
    {
        /// <summary>The Creator's width: what the tab bar needs plus about 60 of empty bar. A test keeps it equal to <c>MainWidth</c> in Tokens.axaml.</summary>
        public const double CreatorWidth = 600;

        /// <summary>The Manager and Settings open 768 wide (the owner's pick, 2026-11-17): just wide enough for the Manager's Runs column.</summary>
        public const double ManagerDefaultWidth = 768;

        /// <summary>The smallest height.</summary>
        public const double ManagerDefaultHeight = ManagerMinHeight;

        public const double ManagerMinWidth = 720;

        /// <summary>470, not 480: the Manager's table is the header and 5 whole rows with 24 under them.</summary>
        public const double ManagerMinHeight = 470;

        /// <summary>Shrinks the bounds to fit the work area, then slides them so they lie fully inside it.</summary>
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
