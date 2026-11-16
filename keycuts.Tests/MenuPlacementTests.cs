using System.Linq;
using System.Windows;
using keycuts.GUI.Controls;
using Xunit;

namespace keycuts.Tests
{
    public class MenuPlacementTests
    {
        [Fact]
        public void UnderRightEdge_LinesUpTheRightEdges_BelowTheButton_ThenAboveIt()
        {
            // A 190 × 80 menu on a 36 × 36 button, 6 away: its left is 154 left of the button's, whichever side Windows drops menus from
            var placements = MenuPlacement.UnderRightEdge(new Size(190, 80), new Size(36, 36), new Point(0, 6));

            Assert.Equal(new[] { new Point(-154, 42), new Point(-154, -86) }, placements.Select(p => p.Point));
        }
    }
}
