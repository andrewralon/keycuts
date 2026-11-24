#nullable enable
using System;
using System.Collections.Generic;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Settings' Spotlight row (#328): offered only with the signed file, and the button opens it in Shortcuts.</summary>
    public class SpotlightSettingsTests
    {
        private sealed class Store : ISettingsStore
        {
            public Settings Load() => new Settings { OutputFolder = "/Users/me/Shortcuts" };

            public string? SaveOptions(Settings settings) => null;

            public FolderSaveResult SaveOutputFolder(string folder) => new(true, null);
        }

        [Fact]
        public void TheRow_IsOnlyOffered_WithTheFile()
        {
            Assert.False(new SettingsViewModel(new Store()).IsSpotlightOffered);
            Assert.True(new SettingsViewModel(new Store()) { SpotlightShortcutFile = "/x/Run keycut.shortcut" }.IsSpotlightOffered);
        }

        [Fact]
        public void TheButton_OpensTheFile()
        {
            var opened = new List<string>();
            var settings = new SettingsViewModel(new Store()) { SpotlightShortcutFile = "/x/Run keycut.shortcut", OpenSpotlightShortcut = opened.Add };

            settings.AddSpotlightShortcutCommand.Execute(null);

            Assert.Equal(new[] { "/x/Run keycut.shortcut" }, opened);
            Assert.Null(settings.SaveError);
        }

        [Fact]
        public void AFailedOpen_ShowsAMessage_InsteadOfThrowing()
        {
            var settings = new SettingsViewModel(new Store())
            {
                SpotlightShortcutFile = "/x/Run keycut.shortcut",
                OpenSpotlightShortcut = _ => throw new System.IO.IOException("no app")
            };

            settings.AddSpotlightShortcutCommand.Execute(null);

            Assert.Equal("Couldn’t open the shortcut in Shortcuts.", settings.SaveError);
        }

        [Fact]
        public void TheNote_NamesTheShortcutAndTheQuickKey()
        {
            Assert.Contains("Run keycut", SettingsViewModel.SpotlightNote);
            Assert.Contains("Add quick keys", SettingsViewModel.SpotlightNote);
            Assert.Contains("“k gmail”", SettingsViewModel.SpotlightNote);
        }
    }
}
