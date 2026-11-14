#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The creator's update line and the Settings Updates section (#171), over fakes: no network, no HKCU, no browser.</summary>
    public class UpdateNoticeViewModelTests
    {
        const string ReleaseUrl = "https://github.com/andrewralon/keycuts/releases/tag/v1.2.0";

        #region Creator notice

        [Fact]
        public async Task Startup_ANewerRelease_ShowsTheLine()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, new FakeUpdateState()), _ => { });

            await notice.CheckOnStartupAsync();

            Assert.True(notice.IsVisible);
            Assert.Equal("keycuts 1.2.0 is available", notice.Message);
            Assert.Equal("Download keycuts 1.2.0", notice.DownloadName);
            Assert.True(notice.DownloadCommand.CanExecute(null));
        }

        [Fact]
        public async Task Startup_TheSameVersion_ShowsNothing()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.1.1") };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, new FakeUpdateState(), "1.1.1.0"), _ => { });

            await notice.CheckOnStartupAsync();

            Assert.False(notice.IsVisible);
            Assert.Equal(string.Empty, notice.Message);
            Assert.False(notice.DownloadCommand.CanExecute(null));
        }

        [Fact]
        public async Task Startup_AFailure_ShowsNothing_AndDoesNotThrow()
        {
            var source = new FakeReleaseSource { Next = () => throw new HttpRequestException("offline") };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, new FakeUpdateState()), _ => { });

            await notice.CheckOnStartupAsync();

            Assert.False(notice.IsVisible);
        }

        [Fact]
        public async Task Startup_WithinADay_ShowsWhatTheLastCheckFound_WithoutARequest()
        {
            var source = new FakeReleaseSource();
            var state = new FakeUpdateState { LastCheck = UpdateFakes.Now.AddHours(-1), LatestReleaseSeen = ("1.2.0", ReleaseUrl) };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, state), _ => { });

            await notice.CheckOnStartupAsync();

            Assert.Equal(0, source.Calls);
            Assert.True(notice.IsVisible);
        }

        [Fact]
        public async Task Startup_WhenTurnedOff_ShowsNothing_AndMakesNoRequest()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var state = new FakeUpdateState { Enabled = false, LatestReleaseSeen = ("1.2.0", ReleaseUrl) };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, state), _ => { });

            await notice.CheckOnStartupAsync();

            Assert.Equal(0, source.Calls);
            Assert.False(notice.IsVisible);
        }

        [Fact]
        public async Task Download_OpensTheReleasePage()
        {
            var opened = new List<string>();
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, new FakeUpdateState()), opened.Add);
            await notice.CheckOnStartupAsync();

            notice.DownloadCommand.Execute(null);

            Assert.Equal(new[] { ReleaseUrl }, opened);
            Assert.True(notice.IsVisible);
        }

        [Fact]
        public async Task Dismiss_HidesTheLine_UntilANewerRelease()
        {
            var tag = "v1.2.0";
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release(tag) };
            var state = new FakeUpdateState();
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, state), _ => { });
            await notice.CheckOnStartupAsync();

            var changed = new List<string?>();
            notice.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);
            notice.DismissCommand.Execute(null);

            Assert.False(notice.IsVisible);
            Assert.Contains(nameof(UpdateNoticeViewModel.IsVisible), changed);
            Assert.Equal("1.2.0", state.DismissedRelease);

            // The next start: the same release stays hidden
            notice.Refresh();
            Assert.False(notice.IsVisible);

            // A newer one shows again
            tag = "v1.3.0";
            await notice.Checker.CheckNowAsync();
            notice.Refresh();
            Assert.True(notice.IsVisible);
            Assert.Equal("keycuts 1.3.0 is available", notice.Message);
        }

        [Fact]
        public async Task Refresh_AfterTheToggleIsTurnedOff_HidesTheLine()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var state = new FakeUpdateState();
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(source, state), _ => { });
            await notice.CheckOnStartupAsync();

            state.Enabled = false;
            notice.Refresh();

            Assert.False(notice.IsVisible);
        }

        #endregion

        #region Settings

        sealed class Store : ISettingsStore
        {
            public Settings Stored { get; set; } = new Settings { OutputFolder = @"C:\Shortcuts" };

            public List<Settings> Saves { get; } = new List<Settings>();

            public Settings Load()
            {
                return Stored;
            }

            public string? Save(Settings settings)
            {
                Saves.Add(settings);
                return null;
            }
        }

        static SettingsViewModel NewSettings(UpdateChecker? updates, Store? store = null, Action<string>? openUrl = null)
        {
            return new SettingsViewModel(store ?? new Store(), checkFolder: _ => null, directoryExists: _ => true,
                updates: updates, openUrl: openUrl ?? (_ => { }));
        }

        [Fact]
        public void Settings_LoadsTheToggle_AndStoresItAtOnce()
        {
            var store = new Store { Stored = new Settings { OutputFolder = @"C:\Shortcuts", CheckForUpdates = false } };
            var vm = NewSettings(null, store);
            Assert.False(vm.CheckForUpdates);

            // Stored the moment it changes (#218)
            vm.CheckForUpdates = true;

            Assert.True(Assert.Single(store.Saves).CheckForUpdates);
        }

        [Fact]
        public void Settings_TheToggleIsOnByDefault()
        {
            Assert.True(new Settings().CheckForUpdates);
        }

        [Fact]
        public void Settings_ShowsTheRunningVersionInThreeParts()
        {
            var vm = NewSettings(UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState(), "1.1.1.0"));

            Assert.Equal("Version 1.1.1", vm.CurrentVersionText);
        }

        [Fact]
        public void Settings_WithoutAChecker_HidesCheckNow()
        {
            var vm = NewSettings(null);

            Assert.False(vm.CanCheckForUpdates);
            Assert.False(vm.CheckNowCommand.CanExecute(null));
            Assert.False(vm.HasUpdateStatus);
        }

        [Fact]
        public void Settings_BeforeAnyCheck_ShowsNoStatus()
        {
            var vm = NewSettings(UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState()));

            Assert.True(vm.CanCheckForUpdates);
            Assert.False(vm.HasUpdateStatus);
            Assert.False(vm.IsUpdateAvailable);
        }

        [Fact]
        public void Settings_ShowsWhatTheLastCheckFound()
        {
            var state = new FakeUpdateState { LatestReleaseSeen = ("1.2.0", ReleaseUrl) };
            var vm = NewSettings(UpdateFakes.Checker(new FakeReleaseSource(), state));

            Assert.Equal("keycuts 1.2.0 is available", vm.UpdateStatusText);
            Assert.True(vm.IsUpdateAvailable);
        }

        // Settings always says what's out, even a release whose footer notice was dismissed
        [Fact]
        public void Settings_ShowsADismissedRelease_TooBecauseItIsStillNewer()
        {
            var state = new FakeUpdateState { LatestReleaseSeen = ("1.2.0", ReleaseUrl), DismissedRelease = "1.2.0" };
            var vm = NewSettings(UpdateFakes.Checker(new FakeReleaseSource(), state));

            Assert.True(vm.IsUpdateAvailable);
        }

        [Fact]
        public async Task CheckNow_UpToDate()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.1.1") };
            var vm = NewSettings(UpdateFakes.Checker(source, new FakeUpdateState(), "1.1.1.0"));

            await vm.CheckNowCommand.ExecuteAsync();

            Assert.Equal("You\u2019re up to date (1.1.1).", vm.UpdateStatusText);
            Assert.False(vm.IsUpdateAvailable);
            Assert.False(vm.DownloadUpdateCommand.CanExecute(null));
        }

        [Fact]
        public async Task CheckNow_Available_AndDownloadOpensTheReleasePage()
        {
            var opened = new List<string>();
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var vm = NewSettings(UpdateFakes.Checker(source, new FakeUpdateState()), openUrl: opened.Add);

            await vm.CheckNowCommand.ExecuteAsync();
            vm.DownloadUpdateCommand.Execute(null);

            Assert.Equal("keycuts 1.2.0 is available", vm.UpdateStatusText);
            Assert.True(vm.IsUpdateAvailable);
            Assert.Equal(new[] { ReleaseUrl }, opened);
        }

        [Fact]
        public async Task CheckNow_AFailure_SaysSo_InsteadOfStayingSilent()
        {
            var state = new FakeUpdateState { LatestReleaseSeen = ("1.2.0", ReleaseUrl) };
            var source = new FakeReleaseSource { Next = () => throw new HttpRequestException("offline") };
            var vm = NewSettings(UpdateFakes.Checker(source, state));
            Assert.True(vm.IsUpdateAvailable);

            await vm.CheckNowCommand.ExecuteAsync();

            Assert.Equal("Couldn\u2019t check for updates.", vm.UpdateStatusText);
            Assert.False(vm.IsUpdateAvailable);
        }

        [Fact]
        public async Task CheckNow_AsksEvenWhenTurnedOff_AndWithinADay()
        {
            var source = new FakeReleaseSource();
            var state = new FakeUpdateState { Enabled = false, LastCheck = UpdateFakes.Now.AddMinutes(-1) };
            var vm = NewSettings(UpdateFakes.Checker(source, state));

            await vm.CheckNowCommand.ExecuteAsync();

            Assert.Equal(1, source.Calls);
        }

        [Fact]
        public async Task CheckNow_RaisesUpdateChecked_AfterTheResultIsStored()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var state = new FakeUpdateState();
            var vm = NewSettings(UpdateFakes.Checker(source, state));
            string? storedWhenRaised = null;
            vm.UpdateChecked += (sender, e) => storedWhenRaised = state.LatestReleaseSeen?.Version;

            await vm.CheckNowCommand.ExecuteAsync();

            Assert.Equal("1.2.0", storedWhenRaised);
        }

        [Fact]
        public async Task CheckNow_StoresWhatItFound_SoTheCreatorLineFollows()
        {
            var source = new FakeReleaseSource { Next = () => FakeReleaseSource.Release("v1.2.0") };
            var checker = UpdateFakes.Checker(source, new FakeUpdateState());
            var notice = new UpdateNoticeViewModel(checker, _ => { });
            var vm = NewSettings(checker);

            await vm.CheckNowCommand.ExecuteAsync();
            notice.Refresh();

            Assert.True(notice.IsVisible);
        }

        #endregion
    }
}
