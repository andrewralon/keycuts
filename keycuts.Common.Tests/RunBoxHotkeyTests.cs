using keycuts.Common;
using System.Linq;
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

        // Windows (issue #306): the same record, written and registered the Windows way
        [Fact]
        public void TheWindowsDefault_IsCtrlAltSpace()
        {
            var hotkey = RunBoxHotkey.WindowsDefault;

            Assert.Equal("Ctrl+Alt+Space", hotkey.WindowsDisplay);
            Assert.Equal(0x20u, hotkey.WindowsKeyCode);
            Assert.Equal(RunBoxHotkey.WindowsControl | RunBoxHotkey.WindowsAlt | RunBoxHotkey.WindowsNoRepeat, hotkey.WindowsModifiers);
            Assert.True(hotkey.IsAllowedOnWindows);
        }

        [Theory]
        [InlineData("Ctrl+Alt+K", "Ctrl+Alt+K", 0x4Bu)]
        [InlineData("Win+Shift+5", "Shift+Win+5", 0x35u)]
        [InlineData("Alt+F9", "Alt+F9", 0x78u)]
        [InlineData("Ctrl+Option+Shift+Cmd+;", "Ctrl+Alt+Shift+Win+;", 0xBAu)]
        public void Windows_ReadsAndWritesItsOwnKeyNames(string text, string display, uint code)
        {
            var hotkey = RunBoxHotkey.Parse(text);

            Assert.Equal(display, hotkey.WindowsDisplay);
            Assert.Equal(code, hotkey.WindowsKeyCode);
            Assert.Equal(hotkey, RunBoxHotkey.Parse(hotkey.WindowsDisplay));
        }

        [Theory]
        [InlineData("Shift+K", false)]
        [InlineData("K", false)]
        [InlineData("Alt+Space", true)]
        [InlineData("Win+K", true)]
        [InlineData("Ctrl+K", true)]
        public void OnWindows_AnythingButShiftAlone_IsAllowed(string text, bool allowed)
        {
            Assert.Equal(allowed, RunBoxHotkey.Parse(text).IsAllowedOnWindows);
        }

        [Fact]
        public void EveryKeyTheRecorderTakes_HasAWindowsCode()
        {
            Assert.Equal(RunBoxHotkey.KeyCodes.Keys.OrderBy(k => k), RunBoxHotkey.WindowsKeyCodes.Keys.OrderBy(k => k));
        }
    }
}
