#nullable enable
using System.Collections.Generic;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Settings' torn corner on macOS (issue #292): offered only with a service, toggling asks it, a refusal shows, and Restore defaults turns it off.</summary>
    public class TornCornerSettingsTests
    {
        private sealed class FakeTornCorner : ITornCorner
        {
            public bool IsOn { get; set; }

            public string? Refusal { get; set; }

            public List<bool> Calls { get; } = new();

            public string? Set(bool on)
            {
                Calls.Add(on);
                if (Refusal == null)
                {
                    IsOn = on;
                }

                return Refusal;
            }
        }

        private sealed class Store : ISettingsStore
        {
            public Settings Load() => new Settings { OutputFolder = "/Users/me/Shortcuts" };

            public string? SaveOptions(Settings settings) => null;

            public FolderSaveResult SaveOutputFolder(string folder) => new(true, null);
        }

        private static (SettingsViewModel Settings, FakeTornCorner Option) New(bool confirmRestore = true)
        {
            var option = new FakeTornCorner();
            return (new SettingsViewModel(new Store(), confirmRestoreDefaults: () => confirmRestore) { TornCornerOption = option }, option);
        }

        [Fact]
        public void TheRow_IsOnlyOffered_WithAService_AndStartsOff()
        {
            Assert.False(new SettingsViewModel(new Store()).IsTornCornerOffered);

            var (settings, _) = New();
            Assert.True(settings.IsTornCornerOffered);
            Assert.False(settings.TornCorner);
        }

        [Fact]
        public void Toggling_CallsSet_AndSettingTheCurrentValueDoesNot()
        {
            var (settings, option) = New();

            settings.TornCorner = false;
            Assert.Empty(option.Calls);

            settings.TornCorner = true;
            Assert.Equal(new[] { true }, option.Calls);
            Assert.True(settings.TornCorner);
            Assert.Null(settings.SaveError);
        }

        [Fact]
        public void ARefusal_ShowsAsTheSaveError_AndTheToggleStaysOff()
        {
            var (settings, option) = New();
            option.Refusal = "Couldn’t save.";

            settings.TornCorner = true;

            Assert.Equal("Couldn’t save.", settings.SaveError);
            Assert.False(settings.TornCorner);
        }

        [Fact]
        public void RestoreDefaults_TurnsItOff()
        {
            var (settings, option) = New();
            settings.TornCorner = true;

            settings.RestoreDefaultsCommand.Execute(null);

            Assert.False(option.IsOn);
            Assert.False(settings.TornCorner);
        }
    }
}
