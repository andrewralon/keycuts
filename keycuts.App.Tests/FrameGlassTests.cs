using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// <see cref="FrameGlass"/> clears the system's border and shadow again whenever Avalonia changes the frame. The Win32 side needs
    /// a real window, so it was checked by capturing one over a dark backdrop (docs/internals.md).
    /// </summary>
    public class FrameGlassTests
    {
        private const uint SwpNoSize = 0x0001;

        private const uint SwpNoMove = 0x0002;

        private const uint SwpFrameChanged = 0x0020;

        [Fact]
        public void OnlyTheFrameChangedFlagCountsAsAFrameChange()
        {
            Assert.True(FrameGlass.IsFrameChange(SwpFrameChanged));
            Assert.True(FrameGlass.IsFrameChange(SwpFrameChanged | SwpNoMove | SwpNoSize));
            Assert.False(FrameGlass.IsFrameChange(SwpNoMove | SwpNoSize));
            Assert.False(FrameGlass.IsFrameChange(0));
        }
    }
}
