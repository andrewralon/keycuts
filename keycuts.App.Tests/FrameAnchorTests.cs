using Avalonia;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The arithmetic of <see cref="FrameAnchor"/>: where the window's rectangle goes so what is drawn stays put when the resize
    /// borders come or go. The Win32 messages themselves need a real window, so they were checked by polling one (docs/internals.md).
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
        public void BecomingResizable_StartsTheRectangleTheInsetBeforeWhatIsDrawn()
        {
            // What is drawn at 659 with a 7 px invisible border: the rectangle starts at 652 (the Manager and Settings)
            Assert.Equal(new PixelPoint(652, 45), FrameAnchor.WindowTopLeftFor(new PixelPoint(659, 45), new PixelPoint(7, 0)));
        }

        [Fact]
        public void BecomingFixed_StartsTheRectangleWhereWhatIsDrawnStarts()
        {
            // No resize borders, no inset: the rectangle is what is drawn (the Creator)
            Assert.Equal(new PixelPoint(659, 45), FrameAnchor.WindowTopLeftFor(new PixelPoint(659, 45), new PixelPoint(0, 0)));
        }

        [Fact]
        public void TheFirstGuessOfTheBorder_IsTheSizingFramePlusItsPadding_LessTheDrawnLine()
        {
            // 100 %: SM_CXSIZEFRAME 4 and SM_CXPADDEDBORDER 4, the 7 px measured on Windows 10
            Assert.Equal(7, FrameAnchor.InvisibleBorder(4, 4));
            Assert.Equal(11, FrameAnchor.InvisibleBorder(6, 6));
            Assert.Equal(0, FrameAnchor.InvisibleBorder(0, 0));
        }

        [Fact]
        public void TheSafetyNet_MovesTheWindowByHowFarWhatIsDrawnMoved()
        {
            // Drawn at 666 instead of 659: the window goes 7 px left
            Assert.Equal(new PixelPoint(652, 45), FrameAnchor.MoveBack(new PixelPoint(659, 45), new PixelPoint(666, 45), new PixelPoint(659, 45)));
        }
    }
}
