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

            public Func<Task<CreateResult>> Result = () => Task.FromResult(CreateResult.Success(null!));

            public Task<CreateResult> Create(string destination, string name, CancellationToken ct)
            {
                Calls.Add((destination, name));
                return Result();
            }
        }

        static CreatorViewModel Create(FakeCreator fake, Func<string, bool>? nameExists = null)
        {
            var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), fake.Create, nameExists ?? (_ => false));
            creator.SuccessDuration = TimeSpan.FromMilliseconds(20);
            return creator;
        }

        static CreatorViewModel Ready(FakeCreator fake, Func<string, bool>? nameExists = null)
        {
            var creator = Create(fake, nameExists);
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
        public async Task DuplicateName_ShowsInlineErrorKeepsTextAndSelectsTheName()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.DuplicateName)) };
            var creator = Ready(fake);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorState.DuplicateName, creator.State);
            Assert.True(creator.IsDuplicateName);
            Assert.True(creator.HelperIsError);
            Assert.Contains("already exists", creator.HelperText);
            Assert.Equal("ex", creator.ShortcutName);
            Assert.Equal("https://example.com", creator.Destination);
            Assert.Equal(new[] { CreatorFocusTarget.NameSelectAll }, focus);

            creator.ShortcutName = "ex2";

            Assert.Equal(CreatorState.DestinationSelected, creator.State);
            Assert.False(creator.IsDuplicateName);
        }

        [Fact]
        public async Task InvalidName_UsesTheDuplicateVisualsWithItsOwnMessage()
        {
            var fake = new FakeCreator { Result = () => Task.FromResult(Failure(CreateOutcome.InvalidName)) };
            var creator = Ready(fake);
            var focus = new List<CreatorFocusTarget>();
            creator.FocusRequested += (sender, e) => focus.Add(e);

            await creator.CreateCommand.ExecuteAsync();

            Assert.Equal(CreatorState.DuplicateName, creator.State);
            Assert.True(creator.IsDuplicateName);
            Assert.Equal(CreatorViewModel.InvalidNameMessage, creator.HelperText);
            Assert.Equal("Use letters, numbers, dashes, or dots \u2014 no \\ / : * ? \" < > |", creator.HelperText);
            Assert.Equal(new[] { CreatorFocusTarget.NameSelectAll }, focus);

            creator.ShortcutName = "ok";

            Assert.False(creator.IsDuplicateName);
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
        public async Task CheckDuplicateName_ShowsTheErrorWhenTheNameExists()
        {
            var creator = Ready(new FakeCreator(), name => name == "ex");

            await creator.CheckDuplicateNameAsync();

            Assert.Equal(CreatorState.DuplicateName, creator.State);
        }

        [Fact]
        public async Task CheckDuplicateName_DoesNothingWhenTheNameIsFree()
        {
            var creator = Ready(new FakeCreator(), _ => false);

            await creator.CheckDuplicateNameAsync();

            Assert.Equal(CreatorState.DestinationSelected, creator.State);
        }

        [Fact]
        public async Task CheckDuplicateName_IgnoresAnEmptyName()
        {
            var calls = 0;
            var creator = Ready(new FakeCreator(), _ => { calls++; return true; });
            creator.ShortcutName = " ";

            await creator.CheckDuplicateNameAsync();

            Assert.Equal(0, calls);
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
