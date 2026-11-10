#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class CreatorSubmitTests
    {
        sealed class FakeCreator
        {
            public readonly List<(string Destination, string Name)> Calls = new List<(string, string)>();

            public readonly List<bool> UsedEnvironmentVariables = new List<bool>();

            public readonly List<bool> Replaced = new List<bool>();

            public Func<Task<CreateResult>> Result = () => Task.FromResult(CreateResult.Success(null!));

            public Task<CreateResult> Create(string destination, string name, bool useEnvironmentVariables, bool replace, CancellationToken ct)
            {
                Calls.Add((destination, name));
                UsedEnvironmentVariables.Add(useEnvironmentVariables);
                Replaced.Add(replace);
                return Result();
            }
        }

        static CreatorViewModel Create(FakeCreator fake, Func<string, NameCheck>? checkName = null)
        {
            var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), fake.Create, checkName ?? TestNameChecks.NoWarnings);
            creator.SuccessDuration = TimeSpan.FromMilliseconds(20);
            return creator;
        }

        static readonly PathVariable[] TestVariables = { new PathVariable("USERPROFILE", @"C:\Users\me") };

        static CreatorViewModel FileCreator(FakeCreator fake, string destination = @"C:\Users\me\Desktop\notes.txt")
        {
            var creator = Create(fake);
            creator.PathVariables = TestVariables;
            creator.Destination = destination;
            creator.DestinationKind = KeycutKind.File;
            creator.ShortcutName = "notes";
            return creator;
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]   // Shift turns it on for this keycut
        [InlineData(true, true, false)]   // ...or off
        public async Task EnvironmentVariables_TheSettingFlippedByShift_IsWhatCreateUses(bool setting, bool shift, bool expected)
        {
            var fake = new FakeCreator();
            var creator = FileCreator(fake);
            creator.UseEnvironmentVariablesSetting = setting;
            creator.IsShiftHeld = shift;

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { expected }, fake.UsedEnvironmentVariables);
        }

        [Fact]
        public void SavedAs_ShowsThePathAsItWillBeSaved_AndFollowsShift()
        {
            var creator = FileCreator(new FakeCreator());
            var changed = new List<string?>();
            creator.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            Assert.True(creator.ShowSavedAs);
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.SavedAsPath);

            creator.IsShiftHeld = true;
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.SavedAsPath);
            Assert.Contains(nameof(CreatorViewModel.SavedAsPath), changed);

            creator.UseEnvironmentVariablesSetting = true;
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.SavedAsPath);

            creator.IsShiftHeld = false;
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.SavedAsPath);
        }

        [Fact]
        public void SavedAs_PreviewsTheResolvedDestination_NotTheTypedText()
        {
            var creator = Create(new FakeCreator());
            creator.PathVariables = TestVariables;
            creator.UseEnvironmentVariablesSetting = true;
            creator.Destination = @"C:\Users\me\Desktop\app.lnk";   // typed; it resolves to the shortcut's target
            creator.DestinationKind = KeycutKind.File;

            creator.ResolvedDestination = @"C:\Users\me\Tools\app.exe";
            Assert.Equal(@"%USERPROFILE%\Tools\app.exe", creator.SavedAsPath);

            creator.ResolvedDestination = @"D:\Tools\app.exe";      // no variable fits the target: nothing to choose
            Assert.False(creator.ShowSavedAs);

            creator.Destination = @"C:\Users\me\Desktop\notes.txt";  // new text forgets the old resolution
            creator.DestinationKind = KeycutKind.File;
            Assert.Null(creator.ResolvedDestination);
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.SavedAsPath);
        }

        [Theory]
        [InlineData(@"D:\Data\notes.txt", KeycutKind.File)]           // no variable fits: nothing to choose
        [InlineData("https://example.com", KeycutKind.Url)]           // not a path
        public void SavedAs_IsHidden_WhenThereIsNothingToChoose(string destination, KeycutKind kind)
        {
            var creator = Create(new FakeCreator());
            creator.PathVariables = TestVariables;
            creator.Destination = destination;
            creator.DestinationKind = kind;

            Assert.False(creator.ShowSavedAs);
            Assert.Equal(string.Empty, creator.SavedAsPath);
        }

        static CreatorViewModel Ready(FakeCreator fake, Func<string, NameCheck>? checkName = null)
        {
            var creator = Create(fake, checkName);
            creator.Destination = "https://example.com";
            creator.DestinationKind = KeycutKind.Url;
            creator.ShortcutName = "ex";
            return creator;
        }

        static CreateResult Failure(CreateOutcome outcome, DestinationError error = DestinationError.None)
        {
            return CreateResult.Failure(outcome, destinationError: error);
        }

        [Fact]
        public void CanCreate_NeedsDestinationAndName()
        {
            var creator = Create(new FakeCreator());
            Assert.False(creator.CreateCommand.CanExecute(null));

            creator.Destination = "https://example.com";
            Assert.False(creator.CreateCommand.CanExecute(null));

            creator.ShortcutName = "   ";
            Assert.False(creator.CreateCommand.CanExecute(null));

            creator.ShortcutName = "ex";
            Assert.True(creator.CreateCommand.CanExecute(null));
        }

        [Fact]
        public void CanCreate_IsFalseForADestinationError()
        {
            var creator = Ready(new FakeCreator());

            creator.DestinationError = DestinationError.NotFound;

            Assert.False(creator.CreateCommand.CanExecute(null));
        }

        [Fact]
        public async Task Execute_WhenNotAllowed_DoesNotCallTheCreator()
        {
            var fake = new FakeCreator();
            var creator = Create(fake);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Empty(fake.Calls);
        }

        [Fact]
        public async Task Saving_DisablesCreateAndInputWhileRunning()
        {
            var gate = new TaskCompletionSource<CreateResult>();
            var fake = new FakeCreator { Result = () => gate.Task };
            var creator = Ready(fake);

            var run = creator.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorState.Saving, creator.State);
            Assert.False(creator.CanCreate);
            Assert.False(creator.IsInputEnabled);
            Assert.False(creator.CreateCommand.CanExecute(null));

            gate.SetResult(CreateResult.Success(null!));
            await run;

            Assert.NotEqual(CreatorState.Saving, creator.State);
        }

        [Fact]
        public async Task Execute_PassesTheTrimmedNameAndDestination()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            creator.ShortcutName = "  ex  ";

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(("https://example.com", "ex"), fake.Calls[0]);
        }

        [Fact]
        public async Task Success_ShowsSuccessRaisesCreatedThenResetsToEmpty()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            var created = 0;
            var focus = new List<CreatorFocusTarget>();
            creator.KeycutCreated += (sender, e) => created++;
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(1, created);
            Assert.Equal(CreatorState.Success, creator.State);
            Assert.Equal("Created ex", creator.HelperText);
            Assert.False(creator.CanCreate);

            await WaitFor(() => creator.State == CreatorState.Empty);

            Assert.Equal(string.Empty, creator.Destination);
            Assert.Equal(string.Empty, creator.ShortcutName);
            Assert.Equal(new[] { CreatorFocusTarget.Destination }, focus);
        }

        [Fact]
        public async Task Success_ResetIsSkippedWhenTheUserStartsTyping()
        {
            var creator = Ready(new FakeCreator());
            creator.SuccessDuration = TimeSpan.FromMilliseconds(50);

            await creator.CreateCommand.ExecuteAsync();
            creator.ShortcutName = "next";
            await Task.Delay(150);

            Assert.Equal("next", creator.ShortcutName);
            Assert.Equal("https://example.com", creator.Destination);
        }

        [Fact]
        public async Task ANameWithAWarning_AsksFirst_AndDecliningCreatesNothing()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Existing(name));
            var asked = new List<(NameCheck Check, string Destination)>();
            creator.ConfirmName = (check, destination) => { asked.Add((check, destination)); return false; };
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Empty(fake.Calls);
            var (askedCheck, askedDestination) = Assert.Single(asked);
            Assert.Equal(TestNameChecks.OldDestination, askedCheck.Existing!.Destination);
            Assert.Equal("https://example.com", askedDestination);
            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.True(creator.HelperIsWarning);
            Assert.Contains("already exists", creator.HelperText);
            Assert.False(creator.IsSaving);
            Assert.Equal("ex", creator.ShortcutName);
            Assert.Equal(new[] { CreatorFocusTarget.Name }, focus);

            creator.ShortcutName = "ex2";

            Assert.Equal(CreatorState.DestinationSelected, creator.State);
        }

        [Fact]
        public async Task ConfirmingAReplace_CreatesWithReplaceOn()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Existing(name));
            creator.ConfirmName = (check, destination) => true;

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { true }, fake.Replaced);
            Assert.Equal(CreatorState.Success, creator.State);
        }

        [Fact]
        public async Task ConfirmingANameConflict_CreatesWithoutReplacing()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Conflict(name));
            creator.ConfirmName = (check, destination) => true;

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { false }, fake.Replaced);
            Assert.Equal(CreatorState.Success, creator.State);
        }

        [Fact]
        public async Task WithNoWayToAsk_ANameWithAWarningIsNotCreated()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Conflict(name));

            await creator.CreateCommand.ExecuteAsync();

            Assert.Empty(fake.Calls);
            Assert.Equal(CreatorState.NameWarning, creator.State);
        }

        [Fact]
        public async Task AFreeName_IsCreatedWithoutAsking()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            creator.ConfirmName = (check, destination) => throw new InvalidOperationException("should not ask");

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { false }, fake.Replaced);
            Assert.Equal(CreatorState.Success, creator.State);
        }

        [Fact]
        public async Task AFailingCheck_WarnsAboutNothing_AndStillCreatesWithoutReplacing()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => throw new InvalidOperationException("registry"));

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { false }, fake.Replaced);
        }

        [Fact]
        public async Task AKeycutThatAppearsAfterTheCheck_AsksBeforeReplacingIt()
        {
            var results = new Queue<CreateResult>(new[] { Failure(CreateOutcome.DuplicateName), CreateResult.Success(null!) });
            var fake = new FakeCreator { Result = () => Task.FromResult(results.Dequeue()) };
            var creator = Ready(fake);
            var asked = 0;
            creator.ConfirmName = (check, destination) => { asked++; return check.Existing != null; };

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(1, asked);
            Assert.Equal(new[] { false, true }, fake.Replaced);
            Assert.Equal(CreatorState.Success, creator.State);
        }

        [Fact]
        public async Task AnInvalidName_SkipsTheCheck_AndShowsItsError()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.InvalidName)) };
            var checks = 0;
            var creator = Ready(fake, name => { checks++; return TestNameChecks.Conflict(name); });
            creator.ShortcutName = "keycuts";

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(0, checks);
            Assert.Single(fake.Calls);
            Assert.Equal(CreatorState.NameError, creator.State);
        }

        [Fact]
        public async Task InvalidName_UsesTheErrorVisualsWithItsOwnMessage()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.InvalidName)) };
            var creator = Ready(fake);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorState.NameError, creator.State);
            Assert.True(creator.IsNameError);
            Assert.Equal(CreatorViewModel.InvalidNameMessage, creator.HelperText);
            Assert.Equal("Use letters, numbers, dashes, or dots \u2014 no \\ / : * ? \" < > |", creator.HelperText);
            Assert.Equal(new[] { CreatorFocusTarget.NameSelectAll }, focus);

            creator.ShortcutName = "ok";

            Assert.False(creator.IsNameError);
        }

        [Theory]
        [InlineData(DestinationError.NotFound)]
        [InlineData(DestinationError.AccessDenied)]
        public async Task DestinationInvalid_ShowsTheInvalidDestinationState(DestinationError error)
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.DestinationInvalid, error)) };
            var creator = Ready(fake);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorState.InvalidDestination, creator.State);
            Assert.Equal(error, creator.DestinationError);
            Assert.Equal(CreatorViewModel.GetDestinationErrorMessage(error), creator.HelperText);
            Assert.False(creator.CanCreate);
            Assert.Equal(new[] { CreatorFocusTarget.Destination }, focus);
        }

        [Fact]
        public async Task DestinationInvalid_WithoutAReason_FallsBackToNotFound()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.DestinationInvalid)) };
            var creator = Ready(fake);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(DestinationError.NotFound, creator.DestinationError);
        }

        [Theory]
        [InlineData(CreateOutcome.OutputFolderError, CreatorViewModel.OutputFolderErrorMessage)]
        [InlineData(CreateOutcome.PathUpdateError, CreatorViewModel.PathUpdateErrorMessage)]
        [InlineData(CreateOutcome.Unexpected, CreatorViewModel.UnexpectedErrorMessage)]
        public async Task OtherFailures_ShowAnInlineMessageUnderTheNameRow(CreateOutcome outcome, string message)
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(outcome)) };
            var creator = Ready(fake);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(message, creator.HelperText);
            Assert.Contains("Settings", creator.HelperText);
            Assert.True(creator.HelperIsError);
            Assert.False(creator.IsSaving);
            Assert.Equal(new[] { CreatorFocusTarget.Name }, focus);
            Assert.True(creator.CanCreate);

            creator.ShortcutName = "again";

            Assert.False(creator.HelperIsError);
        }

        [Fact]
        public async Task ACreatorException_IsShownAsAnUnexpectedError()
        {
            var fake = new FakeCreator { Result = () => throw new InvalidOperationException("boom") };
            var creator = Ready(fake);

            await creator.CreateCommand.ExecuteAsync();

            Assert.False(creator.IsSaving);
            Assert.Equal(CreatorViewModel.UnexpectedErrorMessage, creator.HelperText);
        }

        [Fact]
        public async Task CheckName_ShowsTheWarningWhileTyping()
        {
            var creator = Ready(new FakeCreator(), name => name == "ex" ? TestNameChecks.Existing(name) : NameCheck.None(name));

            await creator.CheckNameAsync();

            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.Contains(TestNameChecks.OldDestination, creator.HelperText);
        }

        [Fact]
        public async Task CheckName_DoesNothingWhenTheNameIsFree()
        {
            var creator = Ready(new FakeCreator());

            await creator.CheckNameAsync();

            Assert.Equal(CreatorState.DestinationSelected, creator.State);
        }

        [Fact]
        public async Task CheckName_IgnoresAnEmptyOrInvalidName()
        {
            var calls = 0;
            var creator = Ready(new FakeCreator(), name => { calls++; return TestNameChecks.Existing(name); });

            creator.ShortcutName = " ";
            await creator.CheckNameAsync();
            creator.ShortcutName = "a/b";
            await creator.CheckNameAsync();

            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task CheckName_DropsAResultForANameThatChanged()
        {
            var gate = new TaskCompletionSource<bool>();
            var creator = Ready(new FakeCreator(), name => { gate.Task.Wait(); return TestNameChecks.Existing(name); });

            var check = creator.CheckNameAsync();
            creator.ShortcutName = "other";
            gate.SetResult(true);
            await check;

            Assert.Equal(CreatorState.DestinationSelected, creator.State);
        }

        static async Task WaitFor(Func<bool> condition)
        {
            for (var i = 0; i < 100 && !condition(); i++)
            {
                await Task.Delay(20);
            }

            Assert.True(condition());
        }
    }
}
