#nullable enable
using System.Collections.Generic;
using Avalonia.Input;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Settings' Run box shortcut (Phase 6.2): recorded by pressing it, refused without ⌘ or ⌃, and why it couldn't be used.</summary>
    public class RunBoxShortcutSettingsTests
    {
        private sealed class FakeShortcut : IRunBoxShortcut
        {
            public RunBoxHotkey? Current { get; set; } = RunBoxHotkey.Default;

            public int Status { get; set; }

            /// <summary>What the next <see cref="Apply"/> reports.</summary>
            public int NextStatus { get; set; }

            public List<bool> Pauses { get; } = new();

            public int Apply(RunBoxHotkey? hotkey)
            {
                Current = hotkey;
                Status = NextStatus;
                return Status;
            }

            public void Pause(bool paused)
            {
                Pauses.Add(paused);
            }
        }

        private sealed class Store : ISettingsStore
        {
            public Settings Load() => new Settings { OutputFolder = "/Users/me/Shortcuts" };

            public string? SaveOptions(Settings settings) => null;

            public FolderSaveResult SaveOutputFolder(string folder) => new(true, null);
        }

        private static (SettingsViewModel Settings, FakeShortcut Shortcut) New()
        {
            var shortcut = new FakeShortcut();
            return (new SettingsViewModel(new Store()) { RunBoxShortcut = shortcut }, shortcut);
        }

        [Fact]
        public void TheSection_IsOnlyOffered_WithAShortcutService()
        {
            Assert.False(new SettingsViewModel(new Store()).IsRunBoxOffered);
            var (settings, _) = New();
            Assert.True(settings.IsRunBoxOffered);
            Assert.Equal("⇧⌘Space", settings.ShortcutText);
            Assert.Equal("Opens the Run box from any app.", settings.ShortcutNote);
            Assert.False(settings.ShortcutIsWarning);
        }

        [Fact]
        public void Recording_PausesTheShortcut_AndTheKeysPressedBecomeIt()
        {
            var (settings, shortcut) = New();

            settings.RecordShortcutCommand!.Execute(null);
            Assert.True(settings.IsRecordingShortcut);
            Assert.Equal(new[] { true }, shortcut.Pauses);
            Assert.Equal("Press the keys. Esc cancels.", settings.ShortcutNote);

            settings.RecordShortcut(new RunBoxHotkey("K", false, true, false, true));

            Assert.False(settings.IsRecordingShortcut);
            Assert.Equal("⌥⌘K", shortcut.Current!.Display);
            Assert.Equal("⌥⌘K", settings.ShortcutText);
        }

        [Fact]
        public void KeysWithoutCommandOrControl_AreRefused_AndTheOldShortcutComesBack()
        {
            var (settings, shortcut) = New();
            settings.IsRecordingShortcut = true;

            settings.RecordShortcut(new RunBoxHotkey("Space", false, true, false, false));

            Assert.Equal("⇧⌘Space", shortcut.Current!.Display);
            Assert.Equal(new[] { true, false }, shortcut.Pauses);
            Assert.Equal("⌥Space", settings.ShortcutText);
            Assert.True(settings.ShortcutIsWarning);
            Assert.Equal("Add ⌘ or ⌃: macOS doesn’t allow ⌥ or ⇧ alone.", settings.ShortcutNote);
        }

        [Fact]
        public void AShortcutAnotherAppHas_SaysSo()
        {
            var (settings, shortcut) = New();
            shortcut.NextStatus = -9878;
            settings.IsRecordingShortcut = true;

            settings.RecordShortcut(new RunBoxHotkey("K", true, false, false, false));

            Assert.True(settings.ShortcutIsWarning);
            Assert.Equal("Couldn’t use ⌃K: another app has it. Choose another.", settings.ShortcutNote);
        }

        [Fact]
        public void EscCancels_AndDeleteLeavesNoShortcut()
        {
            var (settings, shortcut) = New();
            settings.IsRecordingShortcut = true;
            settings.IsRecordingShortcut = false;
            Assert.Equal(new[] { true, false }, shortcut.Pauses);
            Assert.Equal("⇧⌘Space", settings.ShortcutText);

            settings.IsRecordingShortcut = true;
            settings.ClearShortcut();

            Assert.Null(shortcut.Current);
            Assert.Equal("none", settings.ShortcutText);
            Assert.Equal("None. Open the Run box from the menu bar.", settings.ShortcutNote);
        }

        [Theory]
        [InlineData(Key.K, PhysicalKey.K, "K")]
        [InlineData(Key.D5, PhysicalKey.Digit5, "5")]
        [InlineData(Key.Space, PhysicalKey.Space, "Space")]
        [InlineData(Key.OemComma, PhysicalKey.Comma, ",")]
        [InlineData(Key.F5, PhysicalKey.F5, "F5")]
        [InlineData(Key.LeftShift, PhysicalKey.ShiftLeft, null)]
        [InlineData(Key.Escape, PhysicalKey.Escape, null)]
        [InlineData(Key.K, PhysicalKey.None, "K")]
        public void TheKeyPressed_IsNamedByItsPlaceOnTheKeyboard(Key key, PhysicalKey physical, string? expected)
        {
            Assert.Equal(expected, SettingsView.ShortcutKeyName(new KeyEventArgs { Key = key, PhysicalKey = physical }));
        }
    }
}
