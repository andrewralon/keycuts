#nullable enable
using System.Collections.Generic;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Settings' Open at login (Phase 6.5): offered only with a login-item service, toggling asks it, a refusal shows, and a switched-off item says where to turn it on.</summary>
    public class OpenAtLoginSettingsTests
    {
        private sealed class FakeLoginItem : ILoginItem
        {
            public bool IsOn { get; set; }

            public bool NeedsApproval { get; set; }

            /// <summary>What the next <see cref="Set"/> returns; null when macOS accepted.</summary>
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

        private static (SettingsViewModel Settings, FakeLoginItem Item) New()
        {
            var item = new FakeLoginItem();
            return (new SettingsViewModel(new Store()) { LoginItem = item }, item);
        }

        [Fact]
        public void TheRow_IsOnlyOffered_WithALoginItemService()
        {
            var plain = new SettingsViewModel(new Store());
            Assert.False(plain.IsOpenAtLoginOffered);
            Assert.False(plain.OpenAtLogin);

            var (settings, _) = New();
            Assert.True(settings.IsOpenAtLoginOffered);
            Assert.False(settings.OpenAtLogin);
            Assert.Equal(SettingsViewModel.OpenAtLoginNote, settings.OpenAtLoginText);
        }

        [Fact]
        public void Toggling_CallsSet_AndFollowsTheService()
        {
            var (settings, item) = New();

            settings.OpenAtLogin = true;
            Assert.Equal(new[] { true }, item.Calls);
            Assert.True(settings.OpenAtLogin);
            Assert.Null(settings.SaveError);

            settings.OpenAtLogin = false;
            Assert.Equal(new[] { true, false }, item.Calls);
            Assert.False(settings.OpenAtLogin);
        }

        [Fact]
        public void SettingTheCurrentValue_DoesNotCallSet()
        {
            var (settings, item) = New();

            settings.OpenAtLogin = false;

            Assert.Empty(item.Calls);
        }

        [Fact]
        public void ARefusal_ShowsAsTheSaveError_AndTheToggleStaysOff()
        {
            var (settings, item) = New();
            item.Refusal = "macOS refused.";

            settings.OpenAtLogin = true;

            Assert.Equal("macOS refused.", settings.SaveError);
            Assert.False(settings.OpenAtLogin);
        }

        [Fact]
        public void WhenTheItemNeedsApproval_TheNoteSaysWhereToTurnItOn()
        {
            var (settings, item) = New();
            item.NeedsApproval = true;

            Assert.Equal(SettingsViewModel.OpenAtLoginApprovalNote, settings.OpenAtLoginText);
        }
    }
}
