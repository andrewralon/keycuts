using Avalonia.Input;
using keycuts.Common;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Run box's shortcut shown on the menu bar's Run… and in the welcome text (Phase 6 follow-ups).</summary>
    public class RunBoxShortcutTextTests
    {
        [Fact]
        public void TheDefaultShortcut_IsShiftCommandSpace_AsAMenuKey()
        {
            var gesture = RunBoxGesture.From(RunBoxHotkey.Default);

            Assert.Equal(new KeyGesture(Key.Space, KeyModifiers.Meta | KeyModifiers.Shift), gesture);
        }

        [Theory]
        [InlineData("Space", false, false, true, true, Key.Space, KeyModifiers.Meta | KeyModifiers.Shift)]
        [InlineData("K", true, true, false, false, Key.K, KeyModifiers.Control | KeyModifiers.Alt)]
        [InlineData("1", false, false, false, true, Key.D1, KeyModifiers.Meta)]
        [InlineData("F5", false, false, false, true, Key.F5, KeyModifiers.Meta)]
        [InlineData(";", false, false, false, true, Key.OemSemicolon, KeyModifiers.Meta)]
        public void Keys_AndModifiers_AreMapped(string key, bool control, bool option, bool shift, bool command, Key expectedKey, KeyModifiers expectedModifiers)
        {
            Assert.Equal(new KeyGesture(expectedKey, expectedModifiers), RunBoxGesture.From(new RunBoxHotkey(key, control, option, shift, command)));
        }

        [Fact]
        public void EveryKeyTheShortcutCanUse_HasAMenuKey()
        {
            foreach (var key in RunBoxHotkey.KeyCodes.Keys)
            {
                Assert.NotNull(RunBoxGesture.From(new RunBoxHotkey(key, false, false, false, true)));
            }
        }

        [Fact]
        public void NoShortcut_HasNoMenuKey()
        {
            Assert.Null(RunBoxGesture.From(null));
        }

        [MacFact]
        public void TheWelcomeText_NamesTheConfiguredShortcut_OrTheMenuBar()
        {
            var read = PlatformText.RunBoxShortcut;
            try
            {
                PlatformText.RunBoxShortcut = () => "⌃⌥K";
                Assert.Contains("press ⌃⌥K and type the name", PlatformText.WelcomeIntro);
                Assert.Contains("Press ⌃⌥K, type its name", PlatformText.WelcomeCreatedHint);

                PlatformText.RunBoxShortcut = () => null;
                Assert.Contains("choose Run… in the menu bar and type the name", PlatformText.WelcomeIntro);
                Assert.Contains("Choose Run… in the menu bar, type its name", PlatformText.WelcomeCreatedHint);
            }
            finally
            {
                PlatformText.RunBoxShortcut = read;
            }
        }
    }
}
