using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Run box shortcut (Phase 6.2): stored, shown and registered forms, and macOS 15's ⌘-or-⌃ rule.</summary>
    public class RunBoxHotkeyTests
    {
        [Fact]
        public void TheDefault_IsShiftCommandSpace()
        {
            var hotkey = RunBoxHotkey.Default;

            Assert.Equal("⇧⌘Space", hotkey.Display);
            Assert.Equal("Shift+Cmd+Space", hotkey.Stored);
            Assert.Equal(49u, hotkey.KeyCode);
            Assert.Equal(RunBoxHotkey.CarbonShift | RunBoxHotkey.CarbonCommand, hotkey.CarbonModifiers);
            Assert.True(hotkey.IsAllowed);
        }

        [Theory]
        [InlineData("Shift+Cmd+Space", "⇧⌘Space")]
        [InlineData("Ctrl+Option+Shift+Cmd+K", "⌃⌥⇧⌘K")]
        [InlineData("ctrl + k", "⌃K")]
        [InlineData("Cmd+F5", "⌘F5")]
        [InlineData("Option+Cmd+/", "⌥⌘/")]
        public void Parse_ReadsTheStoredForm_InAnyCaseAndSpacing(string text, string display)
        {
            var hotkey = RunBoxHotkey.Parse(text);

            Assert.Equal(display, hotkey.Display);
            Assert.Equal(hotkey, RunBoxHotkey.Parse(hotkey.Stored));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Cmd+")]
        [InlineData("Cmd+Escape")]
        [InlineData("Hyper+K")]
        public void Parse_GivesNull_ForNoShortcut_OrWhatItCantRead(string text)
        {
            Assert.Null(RunBoxHotkey.Parse(text));
        }

        [Theory]
        [InlineData("Option+Space", false)]
        [InlineData("Shift+Option+K", false)]
        [InlineData("K", false)]
        [InlineData("Ctrl+K", true)]
        [InlineData("Option+Cmd+K", true)]
        public void OnlyAShortcutWithCommandOrControl_IsAllowed(string text, bool allowed)
        {
            Assert.Equal(allowed, RunBoxHotkey.Parse(text).IsAllowed);
        }
    }
}
