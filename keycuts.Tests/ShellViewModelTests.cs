#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class ShellViewModelTests
    {
        static ShellViewModel CreateShell(Func<Task<int>>? countProvider = null)
        {
            return new ShellViewModel(countProvider ?? (() => Task.FromResult(3)));
        }

        [Fact]
        public void StartsInCreator_WithCreatorTitle()
        {
            var shell = CreateShell();

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.Same(shell.Creator, shell.CurrentView);
            Assert.Equal("keycuts", shell.Title);
        }

        [Fact]
        public void ShowManager_SwapsViewAndTitle_AndRaisesChanges()
        {
            var shell = CreateShell();
            var changed = new System.Collections.Generic.List<string?>();
            shell.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            shell.ShowManagerCommand.Execute(null);

            Assert.Equal(ShellMode.Manager, shell.Mode);
            Assert.Same(shell.Manager, shell.CurrentView);
            Assert.Equal("keycuts", shell.Title);
            Assert.Contains(nameof(ShellViewModel.CurrentView), changed);
            Assert.Contains(nameof(ShellViewModel.Mode), changed);
            Assert.Contains(nameof(ShellViewModel.Title), changed);
        }

        [Fact]
        public void ShowCreator_ReturnsToCreator_AndKeepsCreatorInput()
        {
            var shell = CreateShell();
            shell.Creator.Target = @"C:\temp";
            shell.Creator.ShortcutName = "tmp";

            shell.ShowManager();
            shell.ShowCreatorCommand.Execute(null);

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.Same(shell.Creator, shell.CurrentView);
            Assert.Equal(@"C:\temp", shell.Creator.Target);
            Assert.Equal("tmp", shell.Creator.ShortcutName);
        }

        [Fact]
        public void ShowingTheCurrentModeAgain_DoesNothing()
        {
            var shell = CreateShell();
            var raised = 0;
            shell.PropertyChanged += (sender, e) => raised++;

            shell.ShowCreator();
            Assert.Equal(0, raised);

            shell.ShowManager();
            raised = 0;
            shell.ShowManager();
            Assert.Equal(0, raised);
        }

        [Fact]
        public void ChildCommands_NavigateThroughTheShell()
        {
            var shell = CreateShell();

            shell.Creator.ShowManagerCommand.Execute(null);
            Assert.Equal(ShellMode.Manager, shell.Mode);

            shell.Manager.ShowCreatorCommand.Execute(null);
            Assert.Equal(ShellMode.Creator, shell.Mode);
        }

        [Fact]
        public void ShowManager_ResetsSearchAndMenu()
        {
            var shell = CreateShell();
            shell.Manager.SearchText = "old";
            shell.Manager.IsMenuOpen = true;

            shell.ShowManager();

            Assert.Equal(string.Empty, shell.Manager.SearchText);
            Assert.False(shell.Manager.IsMenuOpen);
        }

        #region Settings as a screen (#218)

        private sealed class SettingsStore : ISettingsStore
        {
            public Settings Stored { get; set; } = new Settings { OutputFolder = @"C:\Shortcuts", CheckForUpdates = true };

            public int Saves { get; private set; }

            public Settings Load()
            {
                return Stored;
            }

            public string? SaveOptions(Settings settings)
            {
                Saves++;
                Stored = settings;
                return null;
            }

            public FolderSaveResult SaveOutputFolder(string folder)
            {
                return new FolderSaveResult(true, null);
            }
        }

        static (ShellViewModel Shell, SettingsStore Store, List<SettingsViewModel> Made) CreateShellWithSettings(Func<Task<int>>? countProvider = null)
        {
            var store = new SettingsStore();
            var made = new List<SettingsViewModel>();
            var shell = new ShellViewModel(countProvider ?? (() => Task.FromResult(3)), settingsFactory: () =>
            {
                var screen = new SettingsViewModel(store, checkFolder: _ => null);
                made.Add(screen);
                return screen;
            });
            return (shell, store, made);
        }

        [Fact]
        public void SettingsCommand_ShowsTheSettingsScreen_FromBothViews()
        {
            var (shell, _, made) = CreateShellWithSettings();

            shell.Creator.OpenSettingsCommand.Execute(null);

            Assert.Equal(ShellMode.Settings, shell.Mode);
            Assert.Same(shell.Settings, shell.CurrentView);
            Assert.Same(made.Single(), shell.Settings);
            Assert.Equal("keycuts", shell.Title);

            shell.ShowManager();
            Assert.Null(shell.Settings);
            shell.Manager.OpenSettingsCommand.Execute(null);
            Assert.Equal(ShellMode.Settings, shell.Mode);
        }

        [Fact]
        public void TheSettingsScreen_IsMadeFreshEachTimeItOpens_AndShowsTheCreatorsFooter()
        {
            var (shell, store, made) = CreateShellWithSettings();

            shell.ShowSettings();
            shell.ShowSettings();
            Assert.Single(made);

            store.Stored = new Settings { OutputFolder = @"D:\Other" };
            shell.ShowCreator();
            shell.ShowSettings();

            Assert.Equal(2, made.Count);
            Assert.Equal(@"D:\Other", shell.Settings!.OutputFolder);
            Assert.Same(shell.Creator, shell.Settings.Footer);
        }

        [Fact]
        public void WithoutAWayToMakeIt_SettingsCantOpen()
        {
            var shell = CreateShell();

            Assert.False(shell.OpenSettingsCommand.CanExecute(null));
            shell.ShowSettings();

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.Null(shell.Settings);
        }

        [Fact]
        public void Escape_OnSettings_ClosesTheIconPickerFirst_ThenReturnsToTheCreator()
        {
            var (shell, _, _) = CreateShellWithSettings();
            shell.ShowSettings();
            shell.Settings!.IsIconPickerOpen = true;

            Assert.Equal(EscapeResult.ClosedIconPicker, shell.HandleEscape());
            Assert.Equal(ShellMode.Settings, shell.Mode);
            Assert.False(shell.Settings.IsIconPickerOpen);

            // Nothing on the screen is unsaved, so leaving it loses nothing
            Assert.Equal(EscapeResult.ReturnedToCreator, shell.HandleEscape());
            Assert.Equal(ShellMode.Creator, shell.Mode);
        }

        [Fact]
        public void TheTabs_AreOff_OnlyWhileTheIconPickerIsOpen()
        {
            var (shell, _, _) = CreateShellWithSettings();
            var changed = new List<string?>();
            shell.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);
            Assert.True(shell.AreTabsEnabled);

            shell.ShowSettings();
            Assert.True(shell.AreTabsEnabled);

            shell.Settings!.IsIconPickerOpen = true;
            Assert.False(shell.AreTabsEnabled);
            Assert.Contains(nameof(ShellViewModel.AreTabsEnabled), changed);

            shell.Settings.IsIconPickerOpen = false;
            Assert.True(shell.AreTabsEnabled);
        }

        [Fact]
        public void AStoredChange_ReachesTheCreatorAtOnce()
        {
            // What closing the old dialog refreshed now follows each change: the environment-variables preview
            var (shell, _, _) = CreateShellWithSettings();
            shell.Creator.UseEnvironmentVariablesSetting = false;
            shell.ShowSettings();

            shell.Settings!.UseEnvironmentVariables = true;
            Assert.True(shell.Creator.UseEnvironmentVariablesSetting);

            shell.Settings.UseEnvironmentVariables = false;
            Assert.False(shell.Creator.UseEnvironmentVariablesSetting);
        }

        // Reloads overlap (a screen switch, then a new folder); one that started earlier and finishes last must not put back
        // the old folder's count (PR #225 review)
        [Fact]
        public async Task AnOlderReloadThatFinishesLast_DoesntShowItsCount()
        {
            var pending = new List<TaskCompletionSource<int>>();
            var shell = new ShellViewModel(() =>
            {
                var answer = new TaskCompletionSource<int>();
                pending.Add(answer);
                return answer.Task;
            });

            var older = shell.ReloadAsync();
            var newer = shell.ReloadAsync();

            pending[1].SetResult(9);
            await newer;
            pending[0].SetResult(3);
            await older;

            Assert.Equal(9, shell.Creator.KeycutCount);
            Assert.Equal(9, shell.Manager.KeycutCount);
        }

        // A Manager load still running when the screen changes and a new folder's count comes in must not fill the table with the
        // old folder's rows afterwards (PR #227 review)
        [Fact]
        public async Task AManagerLoadOvertakenByACountOnlyReload_DoesntPublishItsRows()
        {
            var load = new TaskCompletionSource<IReadOnlyList<Keycut>>();
            var count = 3;
            var shell = new ShellViewModel(() => Task.FromResult(count), _ => load.Task);

            shell.ShowManager();
            Assert.True(shell.Manager.IsLoading);

            count = 9;
            shell.ShowCreator();
            await shell.ReloadAsync();

            load.SetResult(new[]
            {
                new Keycut { Name = "old", Kind = KeycutKind.Url, RawType = ShortcutType.Url, Target = "https://old.example.com", FilePath = @"C:\Old\old.bat" }
            });
            await Task.Yield();

            Assert.Empty(shell.Manager.Rows);
            Assert.Equal(9, shell.Manager.KeycutCount);
            Assert.Equal(9, shell.Creator.KeycutCount);
        }

        [Fact]
        public async Task AnOlderReloadThatFinishesLast_DoesntReachTheWelcomePanel()
        {
            var pending = new List<TaskCompletionSource<int>>();
            // A first run (never dismissed): the panel decides from the first count it is given
            var welcome = new WelcomeViewModel(() => false, () => { });
            var shell = new ShellViewModel(() =>
            {
                var answer = new TaskCompletionSource<int>();
                pending.Add(answer);
                return answer.Task;
            }, welcome: welcome);

            var older = shell.ReloadAsync();
            var newer = shell.ReloadAsync();
            pending[1].SetResult(5);
            await newer;
            var shownAfterNewer = welcome.IsVisible;
            pending[0].SetResult(0);
            await older;

            Assert.Equal(shownAfterNewer, welcome.IsVisible);
            Assert.Equal(5, shell.Creator.KeycutCount);
        }

        [Fact]
        public async Task ANewOutputFolder_ReloadsTheCount()
        {
            var count = 3;
            var (shell, _, _) = CreateShellWithSettings(() => Task.FromResult(count));
            shell.ShowSettings();
            await shell.ReloadAsync();
            Assert.Equal(3, shell.Creator.KeycutCount);

            count = 9;
            shell.Settings!.OutputFolder = @"C:\Elsewhere";
            Assert.True(shell.Settings.CommitOutputFolder());
            await Task.Yield();
            await shell.ReloadAsync();

            Assert.Equal(9, shell.Creator.KeycutCount);
        }

        // Check now can answer after Settings was left; the footer's update line still follows (PR #225 review)
        [Fact]
        public async Task ACheckThatEndsAfterLeavingSettings_StillRefreshesTheFooter()
        {
            var answer = new TaskCompletionSource<keycuts.Common.LatestRelease>();
            var source = new FakeReleaseSource { NextAsync = () => answer.Task };
            var checker = UpdateFakes.Checker(source, new FakeUpdateState(), "1.1.1.0");
            var notice = new UpdateNoticeViewModel(checker, _ => { });
            var store = new SettingsStore();
            var shell = new ShellViewModel(() => Task.FromResult(3), updateNotice: notice,
                settingsFactory: () => new SettingsViewModel(store, checkFolder: _ => null, updates: checker));
            notice.Refresh();
            Assert.False(notice.IsVisible);

            shell.ShowSettings();
            var check = shell.Settings!.CheckNowCommand.ExecuteAsync();
            shell.ShowCreator();

            answer.SetResult(FakeReleaseSource.Release("v1.2.0"));
            await check;

            Assert.True(notice.IsVisible);
            Assert.Equal("keycuts 1.2.0 is available", notice.Message);
        }

        [Fact]
        public void ShowWelcome_OnSettings_GoesBackToTheCreator()
        {
            var (shell, _, _) = CreateShellWithSettings();
            shell.ShowSettings();
            var screen = shell.Settings!;

            screen.ShowWelcomeCommand.Execute(null);

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.Null(shell.Settings);

            // The old screen is let go: its events no longer reach the shell
            screen.UseEnvironmentVariables = !screen.UseEnvironmentVariables;
            Assert.Equal(ShellMode.Creator, shell.Mode);
        }

        #endregion

        [Fact]
        public async Task ReloadAsync_UpdatesCountOnBothViews()
        {
            var count = 2;
            var shell = CreateShell(() => Task.FromResult(count));

            await shell.ReloadAsync();
            Assert.Equal(2, shell.Creator.KeycutCount);
            Assert.Equal(2, shell.Manager.KeycutCount);

            count = 5;
            shell.ShowManager();
            Assert.Equal(5, shell.Manager.KeycutCount);
        }

        [Fact]
        public async Task ReloadAsync_TreatsProviderFailureAsZero()
        {
            var shell = CreateShell(() => throw new InvalidOperationException("boom"));
            shell.Creator.KeycutCount = 4;

            await shell.ReloadAsync();

            Assert.Equal(0, shell.Creator.KeycutCount);
        }

        [Fact]
        public void KeycutCreated_ReloadsCount()
        {
            var shell = CreateShell(() => Task.FromResult(7));

            shell.Creator.NotifyKeycutCreated();

            Assert.Equal(7, shell.Creator.KeycutCount);
        }

        #region Escape precedence

        [Fact]
        public void Escape_InCreator_ClosesWindow()
        {
            var shell = CreateShell();

            Assert.Equal(EscapeResult.CloseWindow, shell.HandleEscape());
            Assert.Equal(ShellMode.Creator, shell.Mode);
        }

        [Fact]
        public void Escape_InManager_ClosesOpenMenuFirst_EvenWithSearchText()
        {
            var shell = CreateShell();
            shell.ShowManager();
            shell.Manager.SearchText = "abc";
            shell.Manager.IsMenuOpen = true;

            Assert.Equal(EscapeResult.ClosedMenu, shell.HandleEscape());

            Assert.False(shell.Manager.IsMenuOpen);
            Assert.Equal("abc", shell.Manager.SearchText);
            Assert.Equal(ShellMode.Manager, shell.Mode);
        }

        [Fact]
        public void Escape_InManager_ClearsSearchBeforeLeaving()
        {
            var shell = CreateShell();
            shell.ShowManager();
            shell.Manager.SearchText = "abc";

            Assert.Equal(EscapeResult.ClearedSearch, shell.HandleEscape());

            Assert.Equal(string.Empty, shell.Manager.SearchText);
            Assert.Equal(ShellMode.Manager, shell.Mode);
        }

        [Fact]
        public void Escape_InManager_WithNothingOpen_ReturnsToCreator()
        {
            var shell = CreateShell();
            shell.ShowManager();

            Assert.Equal(EscapeResult.ReturnedToCreator, shell.HandleEscape());

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.Same(shell.Creator, shell.CurrentView);
        }

        [Fact]
        public void Escape_FullSequence_MenuThenSearchThenCreatorThenClose()
        {
            var shell = CreateShell();
            shell.ShowManager();
            shell.Manager.SearchText = "x";
            shell.Manager.IsMenuOpen = true;

            Assert.Equal(EscapeResult.ClosedMenu, shell.HandleEscape());
            Assert.Equal(EscapeResult.ClearedSearch, shell.HandleEscape());
            Assert.Equal(EscapeResult.ReturnedToCreator, shell.HandleEscape());
            Assert.Equal(EscapeResult.CloseWindow, shell.HandleEscape());
        }

        #endregion Escape precedence

        [Theory]
        [InlineData("", false)]
        [InlineData("  ", false)]
        [InlineData(@"C:\temp", true)]
        public void Creator_FocusesNameFieldOnlyWhenTargetIsSet(string target, bool expected)
        {
            var shell = CreateShell();
            shell.Creator.Target = target;

            Assert.Equal(expected, shell.Creator.FocusNameFieldOnShow);
        }
    }

    public class WindowLayoutTests
    {
        static readonly Rect WorkArea = new Rect(0, 0, 1920, 1040);

        [Fact]
        public void Clamp_LeavesBoundsInsideWorkAreaUntouched()
        {
            var bounds = new Rect(100, 100, 960, 700);

            Assert.Equal(bounds, WindowLayout.ClampToWorkArea(bounds, WorkArea));
        }

        [Fact]
        public void Clamp_SlidesWindowBackOnScreen_WhenGrowingPastRightAndBottomEdges()
        {
            var result = WindowLayout.ClampToWorkArea(new Rect(1700, 800, 960, 700), WorkArea);

            Assert.Equal(new Rect(960, 340, 960, 700), result);
        }

        [Fact]
        public void Clamp_SlidesWindowBack_WhenStartingLeftOrAboveWorkArea()
        {
            var result = WindowLayout.ClampToWorkArea(new Rect(-50, -20, 960, 700), WorkArea);

            Assert.Equal(new Rect(0, 0, 960, 700), result);
        }

        [Fact]
        public void Clamp_ShrinksWindowLargerThanWorkArea()
        {
            var result = WindowLayout.ClampToWorkArea(new Rect(0, 0, 2500, 1500), WorkArea);

            Assert.Equal(WorkArea, result);
        }

        [Fact]
        public void Clamp_HonorsWorkAreaOffset_ForSecondaryMonitors()
        {
            var secondary = new Rect(-1280, 0, 1280, 984);

            var result = WindowLayout.ClampToWorkArea(new Rect(-300, 500, 960, 700), secondary);

            Assert.Equal(new Rect(-960, 284, 960, 700), result);
        }

        [Fact]
        public void Sizes_MatchTheSpec()
        {
            // What the tab bar needs plus about 60 of empty bar (#218); RailWindowTests checks it against MainWidth in Tokens.xaml
            Assert.Equal(600, WindowLayout.CreatorWidth);
            Assert.Equal(960, WindowLayout.ManagerDefaultWidth);
            Assert.Equal(700, WindowLayout.ManagerDefaultHeight);
            Assert.Equal(720, WindowLayout.ManagerMinWidth);
            Assert.Equal(480, WindowLayout.ManagerMinHeight);
        }
    }

    public class RelayCommandTests
    {
        [Fact]
        public void RelayCommand_RunsOnlyWhenCanExecute()
        {
            var runs = 0;
            var allowed = false;
            var command = new RelayCommand(() => runs++, () => allowed);

            command.Execute(null);
            Assert.Equal(0, runs);

            allowed = true;
            command.Execute(null);
            Assert.Equal(1, runs);
        }

        [Fact]
        public async Task AsyncRelayCommand_BlocksReentryWhileRunning_AndRaisesCanExecuteChanged()
        {
            var gate = new TaskCompletionSource();
            var runs = 0;
            var command = new AsyncRelayCommand(async () =>
            {
                runs++;
                await gate.Task;
            });
            var changes = 0;
            command.CanExecuteChanged += (sender, e) => changes++;

            var first = command.ExecuteAsync();
            Assert.False(command.CanExecute(null));
            await command.ExecuteAsync();
            Assert.Equal(1, runs);

            gate.SetResult();
            await first;

            Assert.True(command.CanExecute(null));
            Assert.Equal(2, changes);
        }
    }
}
