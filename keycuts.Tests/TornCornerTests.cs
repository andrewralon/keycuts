using System.Linq;
using System.Windows;
using keycuts.GUI;
using keycuts.GUI.Controls;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The window's torn bottom-right corner (docs/mockups/torn-corner-v2, B2).</summary>
    public class TornCornerTests
    {
        [Fact]
        public void Edge_RunsFromTheBottomEdgeToTheRightEdge()
        {
            var edge = TornCornerShape.Default.Edge;

            Assert.Equal(new Point(-TornCornerShape.TearWidth, 0), edge.First());
            Assert.Equal(new Point(0, -TornCornerShape.TearHeight), edge.Last());
        }

        [Fact]
        public void Edge_StaysInsideTheWindowAndNearTheCorner()
        {
            // The jitter may push a point a little past the tear's box inwards, never out of the window
            foreach (var point in TornCornerShape.Default.Edge)
            {
                Assert.InRange(point.X, -TornCornerShape.TearWidth - 10, 0);
                Assert.InRange(point.Y, -TornCornerShape.TearHeight - 10, 0);
            }
        }

        [Fact]
        public void Shape_IsTheSameEveryTime()
        {
            // Seeded, like the mockup: every window tears the same way
            var shape = TornCornerShape.Default;
            Assert.Equal(27, shape.Edge.Count);
            Assert.Equal(18, shape.Threads.Count);
            Assert.All(shape.Threads, thread => Assert.InRange(thread.Opacity, .6, 1));
        }

        [Fact]
        public void WindowClip_LeavesOutTheCornerAndKeepsTheRest()
        {
            var size = new Size(600, 300);
            var clip = TornCornerShape.Default.GetWindowClip(size);

            Assert.False(clip.FillContains(new Point(599, 299)));
            Assert.False(clip.FillContains(new Point(590, 290)));
            Assert.True(clip.FillContains(new Point(1, 1)));
            Assert.True(clip.FillContains(new Point(599, 1)));
            Assert.True(clip.FillContains(new Point(1, 299)));
            // Well above the rip on the right edge, and well left of it on the bottom
            Assert.True(clip.FillContains(new Point(599, 300 - TornCornerShape.TearHeight - 12)));
            Assert.True(clip.FillContains(new Point(600 - TornCornerShape.TearWidth - 12, 299)));
        }

        [Theory]
        [InlineData(ThemeVariant.Light, true)]
        [InlineData(ThemeVariant.Dark, true)]
        [InlineData(ThemeVariant.HighContrast, false)]
        public void HighContrast_KeepsThePlainRectangle(ThemeVariant variant, bool torn)
        {
            Assert.Equal(torn, MainWindow.ShowsTear(variant));
        }
    }
}
