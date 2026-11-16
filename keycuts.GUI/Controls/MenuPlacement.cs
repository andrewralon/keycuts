using System.Windows;
using System.Windows.Controls.Primitives;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// Where a button's menu opens: under the button, their right edges lined up (above it when there's no room below), the
    /// popup's VerticalOffset away. Use it with Placement="Custom".
    /// </summary>
    /// <remarks>
    /// Not Placement="Bottom" with a HorizontalOffset of (button width - menu width): on a PC whose menus open to the left of
    /// the hand ("right-handed" in Tablet PC Settings, SystemParameters.MenuDropAlignment, common with touch screens) WPF
    /// already lines up the right edges for Bottom, so the offset threw the menu a menu's width too far to the left. A custom
    /// placement isn't changed by that setting.
    /// </remarks>
    public static class MenuPlacement
    {
        public static readonly CustomPopupPlacementCallback UnderRightEdge = PlaceUnderRightEdge;

        /// <summary>Under the target with their left edges lined up, moved right by the popup's HorizontalOffset (above it when there's no room below).</summary>
        public static readonly CustomPopupPlacementCallback UnderLeftEdge = PlaceUnderLeftEdge;

        private static CustomPopupPlacement[] PlaceUnderLeftEdge(Size popupSize, Size targetSize, Point offset)
        {
            return new[]
            {
                new CustomPopupPlacement(new Point(offset.X, targetSize.Height + offset.Y), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(offset.X, -popupSize.Height - offset.Y), PopupPrimaryAxis.Horizontal)
            };
        }

        private static CustomPopupPlacement[] PlaceUnderRightEdge(Size popupSize, Size targetSize, Point offset)
        {
            var left = targetSize.Width - popupSize.Width;
            return new[]
            {
                new CustomPopupPlacement(new Point(left, targetSize.Height + offset.Y), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(left, -popupSize.Height - offset.Y), PopupPrimaryAxis.Horizontal)
            };
        }
    }
}
