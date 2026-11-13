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
            public readonly List<(string Target, string Name)> Calls = new List<(string, string)>();

            public readonly List<bool> UsedEnvironmentVariables = new List<bool>();

            public readonly List<bool> Replaced = new List<bool>();

            public readonly List<string> Descriptions = new List<string>();

            public Func<Task<CreateResult>> Result = () => Task.FromResult(CreateResult.Success(null!));

            public Task<CreateResult> Create(string target, string name, string description, bool useEnvironmentVariables, bool replace, CancellationToken ct)
            {
                Calls.Add((target, name));
                Descriptions.Add(description);
                UsedEnvironmentVariables.Add(useEnvironmentVariables);
                Replaced.Add(replace);
                return Result();
            }
        }

        static CreatorViewModel Create(FakeCreator fake, Func<string, NameCheck>? checkName = null, IDescriptionVisibilityStore? descriptionStore = null)
        {
            var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), fake.Create, checkName ?? TestNameChecks.NoWarnings, descriptionStore);
            // Longer than any test runs, so a test that asserts Success never races the reset to Empty (a slow CI runner
            // can pause a test past a short duration). The tests about the reset set their own.
            creator.SuccessDuration = TimeSpan.FromMinutes(1);
            return creator;
        }

        static readonly PathVariable[] TestVariables = { new PathVariable("USERPROFILE", @"C:\Users\me") };

        static CreatorViewModel FileCreator(FakeCreator fake, string target = @"C:\Users\me\Desktop\notes.txt")
        {
            var creator = Create(fake);
            creator.PathVariables = TestVariables;
            creator.Target = target;
            creator.TargetKind = KeycutKind.File;
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
        public void TargetText_ShowsThePathWithVariables_WhileItIsNotBeingEdited_AndFollowsShift()
        {
            var creator = FileCreator(new FakeCreator());
            var changed = new List<string?>();
            creator.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.TargetText);

            creator.IsShiftHeld = true;
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.TargetText);
            Assert.Contains(nameof(CreatorViewModel.TargetText), changed);

            creator.UseEnvironmentVariablesSetting = true;   // the setting on, flipped by Shift: back to the plain path
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.TargetText);

            creator.IsShiftHeld = false;
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.TargetText);

            // It is only displayed: what is created from is still the real path
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.Target);
        }

        [Fact]
        public void TargetText_IsTheRealPath_WhileTheTargetBoxIsBeingEdited()
        {
            var creator = FileCreator(new FakeCreator());
            creator.UseEnvironmentVariablesSetting = true;
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.TargetText);

            creator.IsTargetEditing = true;
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.TargetText);

            creator.IsShiftHeld = true;   // Shift while typing a capital must not rewrite the text
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.TargetText);

            creator.IsTargetEditing = false;
            Assert.Equal(@"C:\Users\me\Desktop\notes.txt", creator.TargetText);   // setting on, Shift flips it off
        }

        [Fact]
        public void TargetText_SetByTyping_SetsTheTarget()
        {
            var creator = FileCreator(new FakeCreator());
            creator.UseEnvironmentVariablesSetting = true;
            creator.IsTargetEditing = true;

            creator.TargetText = @"C:\Users\me\Desktop\other.txt";

            Assert.Equal(@"C:\Users\me\Desktop\other.txt", creator.Target);
            Assert.Null(creator.TargetKind);   // a new text forgets what the old one resolved to
        }

        [Fact]
        public void TargetText_PreviewsTheResolvedTarget_NotTheTypedText()
        {
            var creator = Create(new FakeCreator());
            creator.PathVariables = TestVariables;
            creator.UseEnvironmentVariablesSetting = true;
            creator.Target = @"C:\Users\me\Desktop\app.lnk";   // typed; it resolves to the shortcut's target
            creator.TargetKind = KeycutKind.File;

            creator.ResolvedTarget = @"C:\Users\me\Tools\app.exe";
            Assert.Equal(@"%USERPROFILE%\Tools\app.exe", creator.TargetText);

            creator.ResolvedTarget = @"D:\Tools\app.exe";      // no variable fits the target: the typed text stays
            Assert.Equal(@"C:\Users\me\Desktop\app.lnk", creator.TargetText);

            creator.Target = @"C:\Users\me\Desktop\notes.txt";  // new text forgets the old resolution
            creator.TargetKind = KeycutKind.File;
            Assert.Null(creator.ResolvedTarget);
            Assert.Equal(@"%USERPROFILE%\Desktop\notes.txt", creator.TargetText);
        }

        [Theory]
        [InlineData(@"D:\Data\notes.txt", KeycutKind.File)]           // no variable fits
        [InlineData("https://example.com", KeycutKind.Url)]           // not a path
        public void TargetText_IsTheTarget_WhenThereIsNoVariableForIt(string target, KeycutKind kind)
        {
            var creator = Create(new FakeCreator());
            creator.PathVariables = TestVariables;
            creator.UseEnvironmentVariablesSetting = true;
            creator.Target = target;
            creator.TargetKind = kind;

            Assert.Equal(string.Empty, creator.TargetWithVariables);
            Assert.Equal(target, creator.TargetText);
        }

        static CreatorViewModel Ready(FakeCreator fake, Func<string, NameCheck>? checkName = null)
        {
            var creator = Create(fake, checkName);
            creator.Target = "https://example.com";
            creator.TargetKind = KeycutKind.Url;
            creator.ShortcutName = "ex";
            return creator;
        }

        static CreateResult Failure(CreateOutcome outcome, TargetError error = TargetError.None)
        {
            return CreateResult.Failure(outcome, targetError: error);
        }

        [Fact]
        public void CanCreate_NeedsTargetAndName()
        {
            var creator = Create(new FakeCreator());
            Assert.False(creator.CreateCommand.CanExecute(null));

            creator.Target = "https://example.com";
            Assert.False(creator.CreateCommand.CanExecute(null));

            creator.ShortcutName = "   ";
            Assert.False(creator.CreateCommand.CanExecute(null));

            creator.ShortcutName = "ex";
            Assert.True(creator.CreateCommand.CanExecute(null));
        }

        [Fact]
        public void CanCreate_IsFalseForATargetError()
        {
            var creator = Ready(new FakeCreator());

            creator.TargetError = TargetError.NotFound;

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
        public async Task Execute_PassesTheTrimmedNameAndTarget()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            creator.ShortcutName = "  ex  ";

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(("https://example.com", "ex"), fake.Calls[0]);
        }

        #region Description (#162)

        [Fact]
        public void Description_IsCollapsedByDefault()
        {
            var creator = Ready(new FakeCreator());

            Assert.False(creator.IsDescriptionVisible);
            Assert.True(creator.ShowAddDescription);
            Assert.Equal(string.Empty, creator.Description);
        }

        [Fact]
        public void AddADescription_ShowsTheFieldAndFocusesIt()
        {
            var creator = Ready(new FakeCreator());
            var focus = new List<CreatorFocusTarget>();
            var changed = new List<string?>();
            creator.FocusRequested += (sender, e) => focus.Add(e);
            creator.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            creator.ToggleDescriptionCommand.Execute(null);

            Assert.True(creator.IsDescriptionVisible);
            Assert.False(creator.ShowAddDescription);
            Assert.Equal(new[] { CreatorFocusTarget.Description }, focus);
            Assert.Contains(nameof(CreatorViewModel.IsDescriptionVisible), changed);
            Assert.Contains(nameof(CreatorViewModel.ShowAddDescription), changed);
        }

        [Fact]
        public void ADescriptionWithText_IsNeverHidden()
        {
            var creator = Ready(new FakeCreator());

            creator.Description = "Team wiki";

            Assert.True(creator.IsDescriptionVisible);
            Assert.False(creator.ShowAddDescription);
        }

        [Fact]
        public async Task Description_IsPassedToCreate_Trimmed()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            creator.Description = "  Team wiki  ";

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { "Team wiki" }, fake.Descriptions);
        }

        [Fact]
        public async Task WithoutADescription_CreateGetsAnEmptyOne()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { string.Empty }, fake.Descriptions);
        }

        [Fact]
        public async Task Description_IsKeptWhenTheCreateFails()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.Unexpected)) };
            var creator = Ready(fake);
            creator.Description = "Team wiki";

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal("Team wiki", creator.Description);
            Assert.True(creator.IsDescriptionVisible);
        }

        [Fact]
        public async Task Description_StaysFilledInAfterACreate_AndSoDoesTheBox()
        {
            var creator = Ready(new FakeCreator());
            creator.SuccessDuration = TimeSpan.FromMilliseconds(500);
            creator.Description = "Team wiki";

            await creator.CreateCommand.ExecuteAsync();
            await WaitFor(() => creator.State == CreatorState.TargetSelected);

            Assert.Equal("Team wiki", creator.Description);
            Assert.True(creator.IsDescriptionVisible);   // shown or hidden is the user's choice, not something a create undoes
            Assert.False(creator.ShowAddDescription);
        }

        [Fact]
        public void TheDescriptionLabel_IsAButtonThatHidesTheBoxAgain()
        {
            var creator = Ready(new FakeCreator());
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            Assert.Equal("Add a description (optional)", creator.DescriptionToggleText);
            Assert.Equal("Add a description (optional)", creator.DescriptionToggleName);

            creator.ToggleDescriptionCommand.Execute(null);
            Assert.Equal("Description (optional)", creator.DescriptionToggleText);
            Assert.Equal("Hide description", creator.DescriptionToggleName);

            creator.ToggleDescriptionCommand.Execute(null);
            Assert.False(creator.IsDescriptionVisible);
            Assert.True(creator.ShowAddDescription);
            Assert.Equal("Add a description (optional)", creator.DescriptionToggleText);
            Assert.Equal(new[] { CreatorFocusTarget.Description }, focus);   // only showing moves the focus into the box
        }

        [Fact]
        public async Task AHiddenDescription_IsNotSaved_ButComesBackWhenShownAgain()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            creator.Description = "Team wiki";
            creator.ToggleDescriptionCommand.Execute(null);   // hides it, text and all

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { string.Empty }, fake.Descriptions);
            Assert.Equal("Team wiki", creator.Description);
        }

        [Fact]
        public void TheDescriptionBox_StartsWhereItWasLeft()
        {
            Assert.True(Create(new FakeCreator(), null, new FakeDescriptionStore(true)).IsDescriptionVisible);
            Assert.False(Create(new FakeCreator(), null, new FakeDescriptionStore(false)).IsDescriptionVisible);
        }

        [Fact]
        public void ShowingAndHidingTheDescription_IsSavedEachTime_AndOpeningItWritesNothing()
        {
            var store = new FakeDescriptionStore(false);
            var creator = Create(new FakeCreator(), null, store);
            Assert.Empty(store.Saved);   // reading the state on open doesn't write it

            creator.ToggleDescriptionCommand.Execute(null);
            creator.ToggleDescriptionCommand.Execute(null);
            creator.ToggleDescriptionCommand.Execute(null);

            Assert.Equal(new[] { true, false, true }, store.Saved);
        }

        [Fact]
        public async Task TextShowingTheBox_IsNotTheUsersChoice_SoNothingIsSaved_EvenAfterACreate()
        {
            var store = new FakeDescriptionStore(false);
            var creator = Create(new FakeCreator(), null, store);
            creator.Target = "https://example.com";
            creator.TargetKind = KeycutKind.Url;
            creator.ShortcutName = "ex";

            creator.Description = "Team wiki";   // shows the box, because text is never hidden
            await creator.CreateCommand.ExecuteAsync();

            Assert.True(creator.IsDescriptionVisible);
            Assert.Empty(store.Saved);
        }

        sealed class FakeDescriptionStore : IDescriptionVisibilityStore
        {
            private readonly bool initial;

            public readonly List<bool> Saved = new List<bool>();

            public FakeDescriptionStore(bool initial)
            {
                this.initial = initial;
            }

            public bool Load()
            {
                return initial;
            }

            public void Save(bool shown)
            {
                Saved.Add(shown);
            }
        }

        [Fact]
        public async Task TypingADescription_DuringTheSuccessMessage_EndsItAtOnce_AndKeepsTheOtherFields()
        {
            var creator = Ready(new FakeCreator());
            creator.SuccessDuration = TimeSpan.FromMilliseconds(50);

            await creator.CreateCommand.ExecuteAsync();
            Assert.True(creator.IsSuccess);
            creator.Description = "next";
            await Task.Delay(150);

            Assert.False(creator.IsSuccess);
            Assert.Equal("next", creator.Description);
            Assert.Equal("https://example.com", creator.Target);
            Assert.Equal("ex", creator.ShortcutName);
        }

        #endregion Description (#162)

        [Fact]
        public async Task Success_ShowsCreatedRaisesCreated_ThenLeavesTheFormFilledInAndReady()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake);
            // Short enough to wait for the reset, and long enough that the state is still Success when the test resumes:
            // the name check runs on the thread pool, so under a parallel test run the continuation can arrive late
            creator.SuccessDuration = TimeSpan.FromMilliseconds(500);
            var created = 0;
            var focus = new List<CreatorFocusTarget>();
            creator.KeycutCreated += (sender, e) => created++;
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(1, created);
            Assert.Equal(CreatorState.Success, creator.State);
            Assert.Equal("Created ex", creator.HelperText);
            // "Created" sits by the Name label, not under the field, so the line under it stays out of the way
            Assert.Equal("Created ex", creator.NameStatusText);
            Assert.True(creator.HasNameStatus);
            Assert.False(creator.ShowHelperLine);
            Assert.False(creator.CanCreate);

            await WaitFor(() => creator.State == CreatorState.TargetSelected);

            // Nothing is cleared (#205): the keycut is a small edit away, and the focus isn't moved
            Assert.Equal("https://example.com", creator.Target);
            Assert.Equal(KeycutKind.Url, creator.TargetKind);
            Assert.Equal("ex", creator.ShortcutName);
            Assert.Equal(string.Empty, creator.NameStatusText);
            Assert.True(creator.CanCreate);
            Assert.Empty(focus);
        }

        [Fact]
        public async Task Success_EndsWhenTheUserStartsTyping_AndTheTextIsTheirs()
        {
            var creator = Ready(new FakeCreator());
            creator.SuccessDuration = TimeSpan.FromMilliseconds(50);

            await creator.CreateCommand.ExecuteAsync();
            creator.ShortcutName = "next";
            await Task.Delay(150);

            Assert.False(creator.IsSuccess);
            Assert.Equal("next", creator.ShortcutName);
            Assert.Equal("https://example.com", creator.Target);
        }

        [Fact]
        public async Task SavingAndCreated_ShowByTheNameLabel_AndWarningsAndErrorsUnderTheField()
        {
            var release = new TaskCompletionSource<CreateResult>();
            var fake = new FakeCreator { Result = () => release.Task };
            var creator = Ready(fake);

            var create = creator.CreateCommand.ExecuteAsync();
            await WaitFor(() => creator.IsSaving);
            Assert.Equal(CreatorViewModel.SavingHelperText, creator.NameStatusText);
            Assert.False(creator.ShowHelperLine);

            release.SetResult(CreateResult.Success(null!));
            await create;
            Assert.Equal("Created ex", creator.NameStatusText);
            Assert.False(creator.ShowHelperLine);

            // A warning is a sentence about a path: it keeps the line under the field
            creator.ShortcutName = "ex2";
            creator.SetNameWarning(TestNameChecks.Existing("ex2"));
            Assert.Equal(string.Empty, creator.NameStatusText);
            Assert.True(creator.ShowHelperLine);
        }

        [Fact]
        public async Task AnErrorAfterACreate_OutranksTheCreatedMessage_SoNothingSaysCreated()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.Unexpected)) };
            var creator = Ready(fake);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(string.Empty, creator.NameStatusText);
            Assert.True(creator.ShowHelperLine);
            Assert.Equal(CreatorViewModel.UnexpectedErrorMessage, creator.HelperText);
        }

        [Fact]
        public async Task ANameWithAWarning_AsksFirst_AndDecliningCreatesNothing()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Existing(name));
            var asked = new List<(NameCheck Check, string Target)>();
            creator.ConfirmName = (check, target) => { asked.Add((check, target)); return false; };
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Empty(fake.Calls);
            var (askedCheck, askedTarget) = Assert.Single(asked);
            Assert.Equal(TestNameChecks.OldTarget, askedCheck.Existing!.Target);
            Assert.Equal("https://example.com", askedTarget);
            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.True(creator.HelperIsWarning);
            Assert.Contains("already exists", creator.HelperText);
            Assert.False(creator.IsSaving);
            Assert.Equal("ex", creator.ShortcutName);
            Assert.Equal(new[] { CreatorFocusTarget.Name }, focus);

            creator.ShortcutName = "ex2";

            Assert.Equal(CreatorState.TargetSelected, creator.State);
        }

        [Fact]
        public async Task ConfirmingAReplace_CreatesWithReplaceOn()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Existing(name));
            creator.ConfirmName = (check, target) => true;

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(new[] { true }, fake.Replaced);
            Assert.Equal(CreatorState.Success, creator.State);
        }

        [Fact]
        public async Task ConfirmingANameConflict_CreatesWithoutReplacing()
        {
            var fake = new FakeCreator();
            var creator = Ready(fake, name => TestNameChecks.Conflict(name));
            creator.ConfirmName = (check, target) => true;

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
            creator.ConfirmName = (check, target) => throw new InvalidOperationException("should not ask");

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
            creator.ConfirmName = (check, target) => { asked++; return check.Existing != null; };

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
        [InlineData(TargetError.NotFound)]
        [InlineData(TargetError.AccessDenied)]
        public async Task TargetInvalid_ShowsTheInvalidTargetState(TargetError error)
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.TargetInvalid, error)) };
            var creator = Ready(fake);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorState.InvalidTarget, creator.State);
            Assert.Equal(error, creator.TargetError);
            Assert.Equal(CreatorViewModel.GetTargetErrorMessage(error), creator.HelperText);
            Assert.False(creator.CanCreate);
            Assert.Equal(new[] { CreatorFocusTarget.Target }, focus);
        }

        [Fact]
        public async Task TargetInvalid_WithoutAReason_FallsBackToNotFound()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.TargetInvalid)) };
            var creator = Ready(fake);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(TargetError.NotFound, creator.TargetError);
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
            Assert.Contains(TestNameChecks.OldTarget, creator.HelperText);
        }

        [Fact]
        public async Task CheckName_DoesNothingWhenTheNameIsFree()
        {
            var creator = Ready(new FakeCreator());

            await creator.CheckNameAsync();

            Assert.Equal(CreatorState.TargetSelected, creator.State);
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

            Assert.Equal(CreatorState.TargetSelected, creator.State);
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
