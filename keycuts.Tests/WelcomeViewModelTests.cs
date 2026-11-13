#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The first-run panel and its bubbles. Settings and creation are fakes: nothing touches HKCU or a real folder.</summary>
    public class WelcomeViewModelTests
    {
        const string DownloadsPath = @"C:\Users\me\Downloads";

        const string AppPath = @"C:\keycuts\keycuts.GUI.exe";

        // The same three as the app's, with fixed targets
        static IReadOnlyList<WelcomeExample> Examples()
        {
            return new[]
            {
                new WelcomeExample("g", KeycutKind.Url, "google.com", "https://www.google.com"),
                new WelcomeExample("dl", KeycutKind.Folder, "Downloads", DownloadsPath),
                new WelcomeExample("kc", KeycutKind.File, "keycuts", AppPath)
            };
        }

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
            public readonly List<string?> CheckedFolders = new List<string?>();
            public readonly List<string?> AddedFolders = new List<string?>();

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
                    },
                    Examples());
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

        [Fact]
        public void DefaultExamples_AreAWebsite_AFolder_AndKeycutsItself_InThatOrder()
        {
            var examples = WelcomeExample.CreateDefaults();

            Assert.Equal(new[] { "g", "dl", "kc" }, examples.Select(e => e.Name));
            Assert.Equal(new[] { KeycutKind.Url, KeycutKind.Folder, KeycutKind.File }, examples.Select(e => e.Kind));
            Assert.Equal(new[] { "google.com", "Downloads", "keycuts" }, examples.Select(e => e.Label));
            Assert.All(examples, e => Assert.False(string.IsNullOrWhiteSpace(e.Target)));
            Assert.True(Path.IsPathRooted(examples[1].Target));
            Assert.True(Path.IsPathRooted(examples[2].Target));
        }

        [Fact]
        public void DefaultExamples_AreNamesAKeycutMayHave_AndTheWebsiteIsATarget()
        {
            var examples = WelcomeExample.CreateDefaults();

            // kc, not keycuts: that would clash with the CLI's command on PATH
            Assert.All(examples, e => Assert.True(KeycutCreator.IsValidName(e.Name), e.Name));
            Assert.True(TargetResolver.Resolve(examples[0].Target).IsValid);
        }

        [Fact]
        public void Example_NamesItselfForAScreenReader()
        {
            Assert.Equal("URL google.com, creates the keycut g", Examples()[0].AutomationName);
            Assert.Equal("Folder Downloads, creates the keycut dl", Examples()[1].AutomationName);
            Assert.Equal("File keycuts, creates the keycut kc", Examples()[2].AutomationName);
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

            // After a create the panel talks about it; showing it again starts over
            welcome.Select(welcome.Examples[0]);
            welcome.OnKeycutCreated();
            Assert.True(welcome.NeedsPath);

            welcome.Show();

            Assert.False(welcome.NeedsPath);
            Assert.False(welcome.IsSuccess);
            Assert.False(welcome.HelperIsError);
            Assert.Equal(string.Empty, welcome.HelperText);
            Assert.All(welcome.Examples, e => Assert.False(e.IsSelected));
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
        public void HelperText_IsEmptyUntilABubbleIsPicked()
        {
            var welcome = new Fakes().Create();
            welcome.OnKeycutCountLoaded(0);
            // No creator is listening here, so nothing picks the first bubble
            Assert.Equal(string.Empty, welcome.HelperText);
            Assert.False(welcome.HasHelperText);
            Assert.False(welcome.ShowRocket);

            welcome.Select(welcome.Examples[1]);

            Assert.Equal("Click the rocket button to create it", welcome.HelperText);
            Assert.True(welcome.HasHelperText);
            Assert.True(welcome.ShowRocket);
            Assert.False(welcome.IsSuccess);
        }

        [Fact]
        public void Select_HighlightsOneBubble_AndTellsTheCreator()
        {
            var welcome = new Fakes().Create();
            welcome.OnKeycutCountLoaded(0);
            var picked = new List<string>();
            welcome.ExampleSelected += (sender, example) => picked.Add(example.Name);

            welcome.SelectExampleCommand.Execute(welcome.Examples[2]);
            Assert.Equal(new[] { false, false, true }, welcome.Examples.Select(e => e.IsSelected));

            welcome.SelectExampleCommand.Execute(welcome.Examples[0]);
            Assert.Equal(new[] { true, false, false }, welcome.Examples.Select(e => e.IsSelected));

            // Picking the one that is already picked fills the fields again
            welcome.SelectExampleCommand.Execute(welcome.Examples[0]);

            Assert.Equal(new[] { "kc", "g", "g" }, picked);
        }

        [Fact]
        public void OnKeycutCreated_SaysHowToOpenIt_WithoutNamingIt_AndRemembersTheWelcomeWasUsed()
        {
            var fakes = new Fakes();
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);
            welcome.Select(welcome.Examples[0]);

            welcome.OnKeycutCreated();

            Assert.True(welcome.IsSuccess);
            Assert.False(welcome.HelperIsError);
            Assert.False(welcome.ShowRocket);
            Assert.Equal("Created. Now try it out! Press Win+R, type its name, and press Enter to open it.", welcome.HelperText);
            Assert.Equal(1, fakes.MarkCount);
            Assert.True(welcome.IsVisible);

            // Picking another goes back to the rocket hint, so it can be created too
            welcome.Select(welcome.Examples[1]);

            Assert.False(welcome.IsSuccess);
            Assert.Equal("Click the rocket button to create it", welcome.HelperText);
        }

        [Fact]
        public void OnKeycutCreated_WhileTheWelcomeIsHidden_DoesNothing()
        {
            var fakes = new Fakes { Dismissed = true };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(3);

            welcome.OnKeycutCreated();

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

            welcome.OnKeycutCreated();

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
            Assert.StartsWith("Created.", welcome.HelperText);
            Assert.False(welcome.AddToPathCommand.CanExecute(null));
        }

        [Fact]
        public void PathCheckAndAdd_UseTheFolderTheKeycutWasCreatedIn()
        {
            // Settings can change the output folder after the create; the .bat stays where it was written
            var fakes = new Fakes { OnPath = false };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);

            welcome.OnKeycutCreated(@"D:\Portable\Shortcuts");
            welcome.AddToPathCommand.Execute(null);

            Assert.All(fakes.CheckedFolders, folder => Assert.Equal(@"D:\Portable\Shortcuts", folder));
            Assert.Equal(new string?[] { @"D:\Portable\Shortcuts" }, fakes.AddedFolders);
        }

        [Fact]
        public void PathCheck_WithoutAFolderInTheResult_PassesNone_AndAnotherCreateUsesItsOwnFolder()
        {
            var fakes = new Fakes { OnPath = false };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);

            welcome.OnKeycutCreated();
            welcome.OnKeycutCreated(@"E:\Other");

            Assert.Equal(new string?[] { null, @"E:\Other" }, fakes.CheckedFolders);
        }

        [Fact]
        public async Task Creator_TellsTheWelcome_WhichFolderTheKeycutWentTo()
        {
            var fakes = new Fakes { OnPath = false };
            var welcome = fakes.Create();
            var creator = new CreatorViewModel(
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                (target, name, description, useEnvironmentVariables, replace, ct) =>
                    Task.FromResult(CreateResult.Success(new Keycut { Name = name, FilePath = @"D:\Portable\Shortcuts\" + name + ".bat" })),
                TestNameChecks.NoWarnings)
            {
                Welcome = welcome
            };
            welcome.OnKeycutCountLoaded(0);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new string?[] { @"D:\Portable\Shortcuts" }, fakes.CheckedFolders);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        public void AddToPath_ThatFailsOrDoesntStick_KeepsTheButton(bool addSucceeds, bool addPutsItOnPath)
        {
            var fakes = new Fakes { OnPath = false, AddSucceeds = addSucceeds, AddPutsItOnPath = addPutsItOnPath };
            var welcome = fakes.Create();
            welcome.OnKeycutCountLoaded(0);
            welcome.OnKeycutCreated();

            welcome.AddToPathCommand.Execute(null);

            Assert.True(welcome.NeedsPath);
            Assert.Equal(CreatorViewModel.PathUpdateErrorMessage, welcome.HelperText);
            Assert.True(welcome.AddToPathCommand.CanExecute(null));
        }

        [Fact]
        public void WithoutPathFunctions_TheFolderCountsAsOnPath()
        {
            // The Debug folder override passes none, so it never offers to write PATH
            var welcome = new WelcomeViewModel(() => false, () => { }, examples: Examples());
            welcome.OnKeycutCountLoaded(0);

            welcome.OnKeycutCreated();

            Assert.False(welcome.NeedsPath);
            Assert.True(welcome.IsSuccess);
        }

        [Fact]
        public void Creator_PicksTheFirstBubble_WhenTheWelcomeShowsOverAnEmptyForm()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, target) => focus.Add(target);

            welcome.OnKeycutCountLoaded(0);

            Assert.Equal("https://www.google.com", creator.Target);
            Assert.Equal("g", creator.ShortcutName);
            Assert.True(creator.CanCreate);
            Assert.True(welcome.Examples[0].IsSelected);
            Assert.Equal(new[] { CreatorFocusTarget.Name }, focus);
            Assert.Equal("Click the rocket button to create it", welcome.HelperText);
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
            Assert.All(welcome.Examples, e => Assert.False(e.IsSelected));
            Assert.Equal(string.Empty, welcome.HelperText);

            // And Got it leaves it alone too, since the welcome didn't put it there
            welcome.Dismiss();

            Assert.Equal(@"C:\Tools\tool.exe", creator.Target);
        }

        [Fact]
        public void Creator_ReplacesTheFields_WhenABubbleIsPicked_EvenOverATypedForm()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);
            creator.ShortcutName = "mine";
            creator.Target = @"C:\Tools\tool.exe";

            welcome.SelectExampleCommand.Execute(welcome.Examples[1]);

            Assert.Equal(DownloadsPath, creator.Target);
            Assert.Equal("dl", creator.ShortcutName);

            welcome.SelectExampleCommand.Execute(welcome.Examples[2]);

            Assert.Equal(AppPath, creator.Target);
            Assert.Equal("kc", creator.ShortcutName);
        }

        [Fact]
        public void GotIt_ClearsTheFieldsABubbleFilled()
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
            Assert.All(welcome.Examples, e => Assert.False(e.IsSelected));
        }

        [Fact]
        public async Task GotIt_AfterCreatingOne_ClearsTheFieldsThatWereKept()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);

            await creator.CreateCommand.ExecuteAsync();

            Assert.True(creator.IsSuccess);
            Assert.Equal("https://www.google.com", creator.Target);
            Assert.Equal("Created. Now try it out! Press Win+R, type its name, and press Enter to open it.", welcome.HelperText);

            welcome.Dismiss();

            Assert.Equal(string.Empty, creator.Target);
            Assert.Equal(string.Empty, creator.ShortcutName);
            Assert.False(creator.IsSuccess);
        }

        [Fact]
        public async Task EachBubbleCanBeCreatedInTurn()
        {
            var created = new List<(string Target, string Name)>();
            var welcome = new Fakes().Create();
            var creator = new CreatorViewModel(
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                (target, name, description, useEnvironmentVariables, replace, ct) =>
                {
                    created.Add((target, name));
                    return Task.FromResult(CreateResult.Success(null!));
                },
                TestNameChecks.NoWarnings)
            {
                Welcome = welcome
            };
            welcome.OnKeycutCountLoaded(0);

            await creator.CreateCommand.ExecuteAsync();
            Assert.True(welcome.IsSuccess);

            welcome.SelectExampleCommand.Execute(welcome.Examples[1]);
            Assert.Equal("Click the rocket button to create it", welcome.HelperText);
            Assert.True(creator.CanCreate);
            await creator.CreateCommand.ExecuteAsync();

            welcome.SelectExampleCommand.Execute(welcome.Examples[2]);
            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(
                new[] { ("https://www.google.com", "g"), (DownloadsPath, "dl"), (AppPath, "kc") },
                created);
            Assert.True(welcome.IsSuccess);
        }

        [Fact]
        public void ShowingTheWelcomeAgain_PicksTheFirstBubbleAgain_AfterGotItCleared()
        {
            var welcome = new Fakes().Create();
            var creator = NewCreator(welcome);
            welcome.OnKeycutCountLoaded(0);
            welcome.Dismiss();
            Assert.Equal(string.Empty, creator.ShortcutName);

            welcome.Show();

            Assert.Equal("g", creator.ShortcutName);
            Assert.True(welcome.Examples[0].IsSelected);

            // What is in the form isn't replaced by showing the panel again
            creator.ShortcutName = "mine";
            welcome.Show();
            Assert.Equal("mine", creator.ShortcutName);
            Assert.All(welcome.Examples, e => Assert.False(e.IsSelected));
        }

        [Fact]
        public async Task Shell_ShowsTheWelcomeFromTheFirstCount_AndFillsTheFirstBubble()
        {
            var welcome = new Fakes().Create();
            var shell = new ShellViewModel(() => Task.FromResult(0), welcome: welcome);

            Assert.Same(welcome, shell.Creator.Welcome);

            await shell.ReloadAsync();

            Assert.True(welcome.IsVisible);
            Assert.Equal("g", shell.Creator.ShortcutName);
            Assert.Equal("https://www.google.com", shell.Creator.Target);
        }
    }
}
