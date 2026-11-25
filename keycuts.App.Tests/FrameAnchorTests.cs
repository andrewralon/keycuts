using Avalonia;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The arithmetic of <see cref="FrameAnchor"/>: where the window's rectangle goes so the page stays put when the resize borders
    /// come or go. The Win32 messages themselves need a real window, so they were checked by polling one (docs/internals.md).
    /// </summary>
    public class FrameAnchorTests
    {
        private const int WsThickFrame = 0x00040000;

        private const int WsBorder = 0x00800000;

        [Fact]
        public void OnlyTheThickFrameBitTogglesTheResizeBorders()
        {
            Assert.True(FrameAnchor.TogglesResizeBorders(WsBorder, WsBorder | WsThickFrame));
            Assert.True(FrameAnchor.TogglesResizeBorders(WsBorder | WsThickFrame, WsBorder));
            Assert.False(FrameAnchor.TogglesResizeBorders(WsBorder | WsThickFrame, WsBorder | WsThickFrame));
            Assert.False(FrameAnchor.TogglesResizeBorders(WsBorder, WsBorder | 0x00020000));
            Assert.True(FrameAnchor.HasResizeBorders(WsBorder | WsThickFrame));
            Assert.False(FrameAnchor.HasResizeBorders(WsBorder));
        }

        [Fact]
        public void BecomingResizable_StartsTheRectangleTheInsetBeforeThePage()
        {
            // The page at 660 with 8 px of resize border: the rectangle starts at 652 (the Manager and Settings)
            Assert.Equal(new PixelPoint(652, 44), FrameAnchor.WindowTopLeftFor(new PixelPoint(660, 45), new PixelPoint(8, 1)));
        }

        [Fact]
        public void BecomingFixed_StartsTheRectangleOnlyTheThinBorderBeforeThePage()
        {
            // The Creator: the 1 px border and nothing else
            Assert.Equal(new PixelPoint(659, 44), FrameAnchor.WindowTopLeftFor(new PixelPoint(660, 45), new PixelPoint(1, 1)));
        }

        [Fact]
        public void TheFirstGuessOfTheInset_FollowsTheSystemMetrics()
        {
            // 100 %: SM_CXBORDER 1, SM_CXSIZEFRAME 4, SM_CXPADDEDBORDER 4, SM_CYBORDER 1; measured on Windows 10: 8 and 1
            Assert.Equal(new PixelPoint(8, 1), FrameAnchor.ClientInset(true, 1, 4, 4, 1));
            Assert.Equal(new PixelPoint(1, 1), FrameAnchor.ClientInset(false, 1, 4, 4, 1));
            Assert.Equal(new PixelPoint(12, 2), FrameAnchor.ClientInset(true, 2, 6, 6, 2));
        }

        [Fact]
        public void TheSafetyNet_MovesTheWindowByHowFarThePageMoved()
        {
            // The page at 667 instead of 660: the window goes 7 px left
            Assert.Equal(new PixelPoint(652, 45), FrameAnchor.MoveBack(new PixelPoint(659, 45), new PixelPoint(667, 45), new PixelPoint(660, 45)));
        }
    }
}
