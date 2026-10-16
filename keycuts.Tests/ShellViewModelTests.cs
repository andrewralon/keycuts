#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;
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
            Assert.Equal("keycuts \u2014 Your keycuts", shell.Title);
            Assert.Contains(nameof(ShellViewModel.CurrentView), changed);
            Assert.Contains(nameof(ShellViewModel.Mode), changed);
            Assert.Contains(nameof(ShellViewModel.Title), changed);
        }

        [Fact]
        public void ShowCreator_ReturnsToCreator_AndKeepsCreatorInput()
        {
            var shell = CreateShell();
            shell.Creator.Destination = @"C:\temp";
            shell.Creator.ShortcutName = "tmp";

            shell.ShowManager();
            shell.ShowCreatorCommand.Execute(null);

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.Same(shell.Creator, shell.CurrentView);
            Assert.Equal(@"C:\temp", shell.Creator.Destination);
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

        [Fact]
        public void SettingsCommand_RaisesSettingsRequested_FromBothViews()
        {
            var shell = CreateShell();
            var requests = 0;
            shell.SettingsRequested += (sender, e) => requests++;

            shell.Creator.OpenSettingsCommand.Execute(null);
            shell.Manager.OpenSettingsCommand.Execute(null);

            Assert.Equal(2, requests);
        }

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
        public void Creator_FocusesNameFieldOnlyWhenDestinationIsSet(string destination, bool expected)
        {
            var shell = CreateShell();
            shell.Creator.Destination = destination;

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
            Assert.Equal(440, WindowLayout.CreatorWidth);
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
