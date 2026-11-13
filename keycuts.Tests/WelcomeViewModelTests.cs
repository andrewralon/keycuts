#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The first-run panel. Settings and creation are fakes: nothing touches HKCU or a real folder.</summary>
    public class WelcomeViewModelTests
    {
        sealed class Fakes
        {
            public bool Dismissed;
            public int MarkCount;
            public Exception? ReadFailure;
            public Exception? MarkFailure;
            public bool OnPath = true;
            public bool AddSucceeds = true;
            public bool AddPutsItOnPath = true;
            public int AddCount;

            public WelcomeViewModel Create()
            {
                return new WelcomeViewModel(
                    () => ReadFailure != null ? throw ReadFailure : Dismissed,
                    () =>
                    {
                        if (MarkFailure != null)
                        {
                            throw MarkFailure;
                        }

                        MarkCount++;
                    },
                    () => OnPath,
                    () =>
                    {
                        AddCount++;
                        if (AddSucceeds && AddPutsItOnPath)
                        {
                            OnPath = true;
                        }

                        return AddSucceeds;
                    });
            }
        }

        // A creator with the welcome panel, whose creates succeed without touching the disk
        static CreatorViewModel NewCreator(WelcomeViewModel welcome)
        {
            return new CreatorViewModel(
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                (target, name, description, useEnvironmentVariables, replace, ct) => Task.FromResult(CreateResult.Success(null!)),
                TestNameChecks.NoWarnings)
            {
                Welcome = welcome
            };
        }

        [Theory]
        [InlineData(false, 0, true)]  // first run, empty folder
        [InlineData(false, 3, true)]  // first run after an upgrade, keycuts already there
        [InlineData(true, 0, true)]   // dismissed, but there are no keycuts
        [InlineData(true, 3, false)]  // dismissed, and keycuts exist
        public void Shows_OnTheFirstRun_OrForAnEmptyFolder(bool dismissed, int count, bool visible)
        {
            var welcome = new Fakes { Dismissed = dismissed }.Create();
            var shown = 0;
            welcome.Shown += (sender, e) => shown++;

            welcome.OnKeycutCountLoaded(count);

            Assert.Equal(visible, welcome.IsVisible);
            Assert.Equal(visible ? 1 : 0, shown);
        }

        [Fact]
        public void Decides_OnlyFromTheFirstCount()
        {
            var fakes = new Fakes { Dismissed = true };

            var shown = fakes.Create();
            shown.OnKeycutCountLoaded(0);
            shown.OnKeycutCountLoaded(1);
            Assert.True(shown.IsVisible);

            var hidden = fakes.Create();
            hidden.OnKeycutCountLoaded(2);
            hidden.OnKeycutCountLoaded(0);
            Assert.False(hidden.IsVisible);
        }

        [Fact]
        public void Show_BringsThePanelBackFromTheStart()
        {
            var fakes = new Fakes { Dismissed = true, OnPath = false };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(5);
            Assert.False(welcome.IsVisible);
            var shown = 0;
            welcome.Shown += (sender, e) => shown++;

            welcome.Show();

            Assert.True(welcome.IsVisible);
            Assert.Equal(1, shown);

            // After a create the panel talks about that keycut; showing it again starts over
            welcome.OnKeycutCreated("np");
            Assert.True(welcome.NeedsPath);

            welcome.Show();

            Assert.False(welcome.NeedsPath);
            Assert.False(welcome.IsSuccess);
            Assert.False(welcome.HelperIsError);
            Assert.Equal(string.Empty, welcome.HelperText);
            Assert.Equal(2, shown);

            // A later count doesn't hide it again
            welcome.OnKeycutCountLoaded(6);
            Assert.True(welcome.IsVisible);
        }

        [Fact]
        public void Shell_ShowWelcome_ReturnsToTheCreatorWithThePanel()
        {
            var welcome = new Fakes { Dismissed = true }.Create();
            var shell = new ShellViewModel(() => Task.FromResult(4), welcome: welcome);
            welcome.OnKeycutCountLoaded(4);
            shell.ShowManager();

            shell.ShowWelcome();

            Assert.Equal(ShellMode.Creator, shell.Mode);
            Assert.True(welcome.IsVisible);
        }

        [Fact]
        public void UnreadableSetting_CountsAsNotDismissed()
        {
            var fakes = new Fakes { ReadFailure = new UnauthorizedAccessException() };
            var welcome = fakes.Create();

            welcome.OnKeycutCountLoaded(0);

            Assert.True(welcome.IsVisible);
        }

        [Fact]
        public void Dismiss_HidesThePanelAndRemembersIt()
        {
            var fakes = new Fakes();
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);
            var dismissed = 0;
            welcome.Dismissed += (sender, e) => dismissed++;

            welcome.DismissCommand.Execute(null);

            Assert.False(welcome.IsVisible);
            Assert.Equal(1, fakes.MarkCount);
            Assert.Equal(1, dismissed);
        }

        [Fact]
        public void Dismiss_StillHides_WhenTheSettingCantBeSaved()
        {
            var fakes = new Fakes { MarkFailure = new IOException() };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);

            welcome.Dismiss();

            Assert.False(welcome.IsVisible);
        }

        [Fact]
        public void HelperText_IsEmptyUntilAKeycutIsCreated()
        {
            var welcome = new Fakes().Create();
            welcome.OnKeycutCountLoaded(0);

            Assert.Equal(string.Empty, welcome.HelperText);
            Assert.False(welcome.HasHelperText);
            Assert.False(welcome.IsSuccess);
        }

        [Fact]
        public void OnKeycutCreated_SaysHowToOpenIt_AndRemembersTheWelcomeWasUsed()
        {
            var fakes = new Fakes();
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);

            welcome.OnKeycutCreated("np");

            Assert.True(welcome.IsSuccess);
            Assert.False(welcome.HelperIsError);
            Assert.True(welcome.HasHelperText);
            Assert.Equal("Created np. Press Win+R, type np, and press Enter to open it.", welcome.HelperText);
            Assert.Equal(1, fakes.MarkCount);
            Assert.True(welcome.IsVisible);
        }

        [Fact]
        public void OnKeycutCreated_WhileTheWelcomeIsHidden_DoesNothing()
        {
            var fakes = new Fakes { Dismissed = true };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(3);

            welcome.OnKeycutCreated("np");

            Assert.Equal(string.Empty, welcome.HelperText);
            Assert.Equal(0, fakes.MarkCount);
        }

        [Fact]
        public void OnKeycutCreated_WhenTheFolderIsntOnPath_SaysSo_AndOffersToAddIt()
        {
            var fakes = new Fakes { OnPath = false };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);
            Assert.False(welcome.AddToPathCommand.CanExecute(null));

            welcome.OnKeycutCreated("np");

            Assert.True(welcome.NeedsPath);
            Assert.False(welcome.IsSuccess);
            Assert.True(welcome.HelperIsError);
            Assert.Contains("isn’t on your PATH", welcome.HelperText);
            Assert.True(welcome.AddToPathCommand.CanExecute(null));
            Assert.Equal(1, fakes.MarkCount);

            welcome.AddToPathCommand.Execute(null);

            Assert.Equal(1, fakes.AddCount);
            Assert.False(welcome.NeedsPath);
            Assert.True(welcome.IsSuccess);
            Assert.False(welcome.HelperIsError);
            Assert.Contains("Win+R, type np", welcome.HelperText);
            Assert.False(welcome.AddToPathCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        public void AddToPath_ThatFailsOrDoesntStick_KeepsTheButton(bool addSucceeds, bool addPutsItOnPath)
        {
            var fakes = new Fakes { OnPath = false, AddSucceeds = addSucceeds, AddPutsItOnPath = addPutsItOnPath };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);
            welcome.OnKeycutCreated("np");

            welcome.AddToPathCommand.Execute(null);

            Assert.True(welcome.NeedsPath);
            Assert.Equal(CreatorViewModel.PathUpdateErrorMessage, welcome.HelperText);
            Assert.True(welcome.AddToPathCommand.CanExecute(null));
        }

        [Fact]
        public void WithoutPathFunctions_TheFolderCountsAsOnPath()
        {
            // The Debug folder override passes none, so it never offers to write PATH
            var welcome = new WelcomeViewModel(() => false, () => { });
            welcome.OnKeycutCountLoaded(0);

            welcome.OnKeycutCreated("np");

            Assert.False(welcome.NeedsPath);
            Assert.True(welcome.IsSuccess);
        }

        [Fact]
        public void ExampleTarget_IsATargetTheCreatorAccepts()
        {
            Assert.EndsWith(@"\notepad.exe", WelcomeViewModel.ExampleTarget);
            Assert.True(File.Exists(WelcomeViewModel.ExampleTarget));
            Assert.True(TargetResolver.Resolve(WelcomeViewModel.ExampleTarget).IsValid);
        }

        [Fact]
        public void Creator_GetsTheExample_WhenTheWelcomeShowsOverAnEmptyForm()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, target) => focus.Add(target);

            welcome.OnKeycutCountLoaded(0);

            Assert.Equal(WelcomeViewModel.ExampleTarget, creator.Target);
            Assert.Equal("np", creator.ShortcutName);
            Assert.True(creator.CanCreate);
            Assert.Equal(new[] { CreatorFocusTarget.Name }, focus);
        }

        [Fact]
        public void Creator_KeepsWhatWasThere_WhenTheWelcomeShows()
        {
            // The Explorer verb passes a target in before the first count arrives
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            creator.Target = @"C:\Tools\tool.exe";

            welcome.OnKeycutCountLoaded(0);

            Assert.Equal(@"C:\Tools\tool.exe", creator.Target);
            Assert.Equal(string.Empty, creator.ShortcutName);

            // And Got it leaves it alone too, since the welcome didn't put it there
            welcome.Dismiss();

            Assert.Equal(@"C:\Tools\tool.exe", creator.Target);
        }

        [Fact]
        public void GotIt_ClearsTheFieldsTheExampleFilled()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);
            creator.Description = "Notes";

            welcome.DismissCommand.Execute(null);

            Assert.False(welcome.IsVisible);
            Assert.Equal(string.Empty, creator.Target);
            Assert.Equal(string.Empty, creator.ShortcutName);
            Assert.Equal(string.Empty, creator.Description);
            Assert.False(creator.CanCreate);
        }

        [Fact]
        public async Task GotIt_AfterCreatingTheExample_ClearsTheFieldsThatWereKept()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);

            await creator.CreateCommand.ExecuteAsync();

            Assert.True(creator.IsSuccess);
            Assert.Equal(WelcomeViewModel.ExampleTarget, creator.Target);
            Assert.Equal("Created np. Press Win+R, type np, and press Enter to open it.", welcome.HelperText);

            welcome.Dismiss();

            Assert.Equal(string.Empty, creator.Target);
            Assert.Equal(string.Empty, creator.ShortcutName);
            Assert.False(creator.IsSuccess);
        }

        [Fact]
        public void ShowingTheWelcomeAgain_FillsTheExampleAgain_AndKeepsTypedWork()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);
            welcome.Dismiss();
            Assert.Equal(string.Empty, creator.ShortcutName);

            welcome.Show();
            Assert.Equal("np", creator.ShortcutName);

            // What is in the form isn't replaced
            creator.ShortcutName = "mine";
            welcome.Show();
            Assert.Equal("mine", creator.ShortcutName);
            Assert.Equal(WelcomeViewModel.ExampleTarget, creator.Target);
        }

        [Fact]
        public async Task Creator_TellsTheWelcome_WhatWasCreated()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);
            creator.ShortcutName = "mine";

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal("Created mine. Press Win+R, type mine, and press Enter to open it.", welcome.HelperText);
        }

        [Fact]
        public async Task Shell_ShowsTheWelcomeFromTheFirstCount_AndFillsTheExample()
        {
            var welcome = new Fakes().Create();
            var shell = new ShellViewModel(() => Task.FromResult(0), welcome: welcome);

            Assert.Same(welcome, shell.Creator.Welcome);

            await shell.ReloadAsync();

            Assert.True(welcome.IsVisible);
            Assert.Equal("np", shell.Creator.ShortcutName);
            Assert.Equal(WelcomeViewModel.ExampleTarget, shell.Creator.Target);
        }
    }
}
