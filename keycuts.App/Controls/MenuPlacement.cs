using Avalonia.Controls;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// Where a menu opens under the control that opens it (WPF's Controls/MenuPlacement, ported). WPF needed a custom placement
    /// callback, because a PC that drops menus to the left of the hand (Tablet PC Settings) moved Placement="Bottom". Avalonia's
    /// edge-aligned placements aren't changed by that setting, and they flip above the target when there's no room below, so this
    /// only names them.
    /// </summary>
    public static class MenuPlacement
    {
        /// <summary>Under the target, their right edges lined up, <paramref name="gap"/> below it (above it when there's no room below).</summary>
        public static void UnderRightEdge(ContextMenu menu, double gap)
        {
            menu.Placement = PlacementMode.BottomEdgeAlignedRight;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = gap;
        }

        /// <summary>Under the target with their left edges lined up, moved right by <paramref name="indent"/>, <paramref name="gap"/> below it.</summary>
        public static void UnderLeftEdge(ContextMenu menu, double indent, double gap)
        {
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.HorizontalOffset = indent;
            menu.VerticalOffset = gap;
        }
    }
}
