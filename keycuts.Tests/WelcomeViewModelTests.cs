#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The first-run panel. Settings and creation are fakes: nothing touches HKCU or a real folder.</summary>
    public class WelcomeViewModelTests
    {
        const string AppPath = @"C:\Program Files\keycuts\keycuts.GUI.exe";

        sealed class Fakes
        {
            public bool Dismissed;
            public int MarkCount;
            public Exception? ReadFailure;
            public Exception? MarkFailure;
            public readonly List<(string Destination, string Name)> Creates = new List<(string, string)>();
            public Func<Task<CreateResult>> Result = () => Task.FromResult(CreateResult.Success(null!));
            public bool OnPath = true;
            public bool AddSucceeds = true;
            public bool AddPutsItOnPath = true;
            public int AddCount;
            public readonly List<string?> CheckedFolders = new List<string?>();
            public readonly List<string?> AddedFolders = new List<string?>();

            public WelcomeViewModel Create()
            {
                return new WelcomeViewModel(
                    AppPath,
                    () => ReadFailure != null ? throw ReadFailure : Dismissed,
                    () =>
                    {
                        if (MarkFailure != null)
                        {
                            throw MarkFailure;
                        }

                        MarkCount++;
                    },
                    (destination, name, ct) =>
                    {
                        Creates.Add((destination, name));
                        return Result();
                    },
                    folder =>
                    {
                        CheckedFolders.Add(folder);
                        return OnPath;
                    },
                    folder =>
                    {
                        AddCount++;
                        AddedFolders.Add(folder);
                        if (AddSucceeds && AddPutsItOnPath)
                        {
                            OnPath = true;
                        }

                        return AddSucceeds;
                    });
            }
        }

        [Theory]
        [InlineData(false, 0, true)]  // first run, empty folder
        [InlineData(false, 3, true)]  // first run after an upgrade, keycuts already there
        [InlineData(true, 0, true)]   // dismissed, but there are no keycuts
        [InlineData(true, 3, false)]  // dismissed, and keycuts exist
        public void Shows_OnTheFirstRun_OrForAnEmptyFolder(bool dismissed, int count, bool visible)
        {
            var welcome = new Fakes { Dismissed = dismissed }.Create();

            welcome.OnKeycutCountLoaded(count);

            Assert.Equal(visible, welcome.IsVisible);
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
        public async Task Show_BringsThePanelBackFromTheStart()
        {
            var fakes = new Fakes { Dismissed = true };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(5);
            welcome.Name = "x";
            await welcome.CreateCommand.ExecuteAsync();
            Assert.True(welcome.IsSuccess);

            welcome.Show();

            Assert.True(welcome.IsVisible);
            Assert.False(welcome.IsSuccess);
            Assert.True(welcome.IsInputEnabled);
            Assert.Equal("kc", welcome.Name);
            Assert.False(welcome.HelperIsError);

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

            welcome.DismissCommand.Execute(null);

            Assert.False(welcome.IsVisible);
            Assert.Equal(1, fakes.MarkCount);
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
        public void Name_DefaultsToKc_AndCreateNeedsOne()
        {
            var welcome = new Fakes().Create();

            Assert.Equal("kc", welcome.Name);
            Assert.True(welcome.CreateCommand.CanExecute(null));

            welcome.Name = "   ";
            Assert.False(welcome.CreateCommand.CanExecute(null));
        }

        [Fact]
        public async Task Create_MakesAKeycutToTheApp_ThenShowsHowToUseIt()
        {
            var fakes = new Fakes();
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);
            var created = 0;
            welcome.KeycutCreated += (sender, e) => created++;
            welcome.Name = "  kc ";

            await welcome.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { (AppPath, "kc") }, fakes.Creates);
            Assert.True(welcome.IsSuccess);
            Assert.False(welcome.HelperIsError);
            Assert.Contains("Win+R, type kc", welcome.HelperText);
            Assert.False(welcome.IsInputEnabled);
            Assert.False(welcome.CreateCommand.CanExecute(null));
            Assert.Equal(1, fakes.MarkCount);
            Assert.Equal(1, created);
            Assert.True(welcome.IsVisible);
        }

        [Fact]
        public async Task Create_WhenTheFolderIsntOnPath_SaysSo_AndOffersToAddIt()
        {
            var fakes = new Fakes { OnPath = false };
            var welcome = fakes.Create();
            Assert.False(welcome.AddToPathCommand.CanExecute(null));

            await welcome.CreateCommand.ExecuteAsync();

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
            Assert.Contains("Win+R, type kc", welcome.HelperText);
            Assert.False(welcome.AddToPathCommand.CanExecute(null));
        }

        [Fact]
        public async Task PathCheckAndAdd_UseTheFolderTheKeycutWasCreatedIn()
        {
            // Settings can change the output folder after the create; the .bat stays where it was written
            var created = new Keycut { Name = "keycuts", FilePath = @"D:\Portable\Shortcuts\keycuts.bat" };
            var fakes = new Fakes { OnPath = false, Result = () => Task.FromResult(CreateResult.Success(created)) };
            var welcome = fakes.Create();

            await welcome.CreateCommand.ExecuteAsync();
            welcome.AddToPathCommand.Execute(null);

            Assert.All(fakes.CheckedFolders, folder => Assert.Equal(@"D:\Portable\Shortcuts", folder));
            Assert.Equal(new string?[] { @"D:\Portable\Shortcuts" }, fakes.AddedFolders);
        }

        [Fact]
        public async Task PathCheck_WithoutAKeycutInTheResult_PassesNoFolder()
        {
            var fakes = new Fakes { OnPath = false };
            var welcome = fakes.Create();

            await welcome.CreateCommand.ExecuteAsync();

            Assert.Equal(new string?[] { null }, fakes.CheckedFolders);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        public async Task AddToPath_ThatFailsOrDoesntStick_KeepsTheButton(bool addSucceeds, bool addPutsItOnPath)
        {
            var fakes = new Fakes { OnPath = false, AddSucceeds = addSucceeds, AddPutsItOnPath = addPutsItOnPath };
            var welcome = fakes.Create();
            await welcome.CreateCommand.ExecuteAsync();

            welcome.AddToPathCommand.Execute(null);

            Assert.True(welcome.NeedsPath);
            Assert.Equal(CreatorViewModel.PathUpdateErrorMessage, welcome.HelperText);
            Assert.True(welcome.AddToPathCommand.CanExecute(null));
        }

        [Fact]
        public async Task WithoutPathFunctions_TheFolderCountsAsOnPath()
        {
            // The Debug folder override passes none, so it never offers to write PATH
            var welcome = new WelcomeViewModel(AppPath, () => false, () => { },
                (destination, name, ct) => Task.FromResult(CreateResult.Success(null!)));

            await welcome.CreateCommand.ExecuteAsync();

            Assert.False(welcome.NeedsPath);
            Assert.True(welcome.IsSuccess);
        }

        [Fact]
        public async Task DuplicateName_ShowsAnError_UntilTheNameChanges()
        {
            var fakes = new Fakes { Result = () => Task.FromResult(CreateResult.Failure(CreateOutcome.DuplicateName)) };
            var welcome = fakes.Create();

            await welcome.CreateCommand.ExecuteAsync();

            Assert.True(welcome.HelperIsError);
            Assert.Equal("\u201Ckc\u201D already exists. Choose another name.", welcome.HelperText);
            Assert.False(welcome.IsSuccess);
            Assert.True(welcome.IsInputEnabled);
            Assert.Equal(0, fakes.MarkCount);

            welcome.Name = "kc2";
            Assert.False(welcome.HelperIsError);
        }

        [Theory]
        [InlineData(CreateOutcome.InvalidName, CreatorViewModel.InvalidNameMessage)]
        [InlineData(CreateOutcome.OutputFolderError, CreatorViewModel.OutputFolderErrorMessage)]
        [InlineData(CreateOutcome.PathUpdateError, CreatorViewModel.PathUpdateErrorMessage)]
        [InlineData(CreateOutcome.Unexpected, CreatorViewModel.UnexpectedErrorMessage)]
        public async Task Failures_ShowTheCreatorsMessages(CreateOutcome outcome, string message)
        {
            var welcome = new Fakes { Result = () => Task.FromResult(CreateResult.Failure(outcome)) }.Create();

            await welcome.CreateCommand.ExecuteAsync();

            Assert.True(welcome.HelperIsError);
            Assert.Equal(message, welcome.HelperText);
        }

        [Fact]
        public async Task ACreateThatThrows_IsAnUnexpectedError()
        {
            var welcome = new Fakes { Result = () => throw new InvalidOperationException("boom") }.Create();

            await welcome.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorViewModel.UnexpectedErrorMessage, welcome.HelperText);
            Assert.False(welcome.IsSaving);
        }

        [Fact]
        public async Task Shell_ShowsTheWelcomeFromTheFirstCount_AndReloadsAfterTheSelfKeycut()
        {
            var count = 0;
            var loads = 0;
            var welcome = new Fakes().Create();
            var shell = new ShellViewModel(() =>
            {
                loads++;
                return Task.FromResult(count);
            }, welcome: welcome);

            Assert.Same(welcome, shell.Creator.Welcome);

            await shell.ReloadAsync();
            Assert.True(welcome.IsVisible);

            count = 1;
            await welcome.CreateCommand.ExecuteAsync();
            await Task.Yield();

            Assert.Equal(2, loads);
            Assert.Equal(1, shell.Creator.KeycutCount);
            Assert.True(welcome.IsVisible);
        }
    }
}
