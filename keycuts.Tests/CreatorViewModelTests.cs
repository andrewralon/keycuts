#nullable enable
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class CreatorViewModelTests
    {
        static CreatorViewModel Create()
        {
            return new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }));
        }

        static CreatorViewModel WithDestinationAndName()
        {
            var creator = Create();
            creator.Destination = "https://example.com";
            creator.DestinationKind = KeycutKind.Url;
            creator.ShortcutName = "ex";
            return creator;
        }

        [Fact]
        public void Empty_ShowsArrow_DefaultHelper_AndCreateDisabled()
        {
            var creator = Create();

            Assert.Equal(CreatorState.Empty, creator.State);
            Assert.Equal(IconKind.Arrow, creator.DestinationIcon);
            Assert.Equal("Press Enter to create it", creator.HelperText);
            Assert.False(creator.CanCreate);
            Assert.True(creator.IsInputEnabled);
        }

        [Theory]
        [InlineData(KeycutKind.Url, IconKind.Link)]
        [InlineData(KeycutKind.Folder, IconKind.Folder)]
        [InlineData(KeycutKind.File, IconKind.File)]
        [InlineData(KeycutKind.Command, IconKind.Play)]
        public void ResolvedKind_ChoosesTheTypeIcon(KeycutKind kind, IconKind icon)
        {
            var creator = Create();
            creator.Destination = "x";
            creator.DestinationKind = kind;

            Assert.Equal(icon, creator.DestinationIcon);
        }

        [Fact]
        public void EditingTheDestination_ResetsTheIconToArrow()
        {
            var creator = WithDestinationAndName();

            creator.Destination = "https://example.org";

            Assert.Equal(IconKind.Arrow, creator.DestinationIcon);
        }

        [Fact]
        public void CreateNeedsDestinationAndName()
        {
            var creator = Create();
            creator.Destination = "C:\\temp";
            Assert.Equal(CreatorState.DestinationSelected, creator.State);
            Assert.False(creator.CanCreate);

            creator.ShortcutName = "   ";
            Assert.False(creator.CanCreate);

            creator.ShortcutName = "tmp";
            Assert.True(creator.CanCreate);

            creator.Destination = string.Empty;
            Assert.False(creator.CanCreate);
        }

        [Fact]
        public void Saving_DisablesInputAndCreate_AndHidesDragHover()
        {
            var creator = WithDestinationAndName();
            creator.IsDragHover = true;
            Assert.True(creator.ShowDragHover);

            creator.IsSaving = true;

            Assert.Equal(CreatorState.Saving, creator.State);
            Assert.False(creator.IsInputEnabled);
            Assert.False(creator.CanCreate);
            Assert.False(creator.ShowDragHover);
        }

        [Fact]
        public void DragHover_DoesNotChangeTheState()
        {
            var creator = Create();

            creator.IsDragHover = true;

            Assert.Equal(CreatorState.Empty, creator.State);
            Assert.True(creator.ShowDragHover);
        }

        [Fact]
        public void ExistingName_IsAWarningOnTheNameField_WithTheCurrentDestination()
        {
            var creator = WithDestinationAndName();

            creator.SetNameWarning(TestNameChecks.Existing("l-vicki", "https://old.example.com"));

            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.True(creator.IsNameWarning);
            Assert.False(creator.IsNameError);
            Assert.False(creator.ShowDestinationError);
            Assert.False(creator.HelperIsError);
            Assert.True(creator.HelperIsWarning);
            Assert.True(creator.CanCreate);
            Assert.Equal("\u201Cl-vicki\u201D already exists and opens https://old.example.com.", creator.HelperText);
        }

        [Theory]
        [InlineData(NameConflictEffect.Shadows, "\u201cex\u201d already opens C:\\Program Files\\Tool\\ex.exe. This keycut would replace it in Win+R.")]
        [InlineData(NameConflictEffect.Hidden, "\u201cex\u201d already opens C:\\Program Files\\Tool\\ex.exe, so Win+R would never run this keycut.")]
        public void NameConflict_IsAWarning_ThatNamesTheOtherProgram(NameConflictEffect effect, string message)
        {
            var creator = WithDestinationAndName();

            creator.SetNameWarning(TestNameChecks.Conflict("ex", effect));

            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.True(creator.HelperIsWarning);
            Assert.Equal(message, creator.HelperText);
        }

        [Fact]
        public void NameWarning_ListsTheReplaceFirst_ThenTheConflictThatMattersMost()
        {
            var check = new NameCheck("ex", new Keycut { Name = "ex", Destination = @"D:\old" },
                new[] { new NameConflict(@"C:\a\ex.exe", NameConflictEffect.Shadows, NameConflictSource.PathAfter),
                        new NameConflict(@"C:\Windows\ex.exe", NameConflictEffect.Hidden, NameConflictSource.WindowsFolder) });

            var lines = CreatorViewModel.GetNameWarningMessage(check).Split(System.Environment.NewLine);

            Assert.Equal(2, lines.Length);
            Assert.StartsWith("\u201cex\u201d already exists", lines[0]);
            Assert.Contains(@"C:\Windows\ex.exe", lines[1]);
        }

        [Fact]
        public void AnInvalidName_WinsOverTheWarning()
        {
            var creator = WithDestinationAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));
            creator.ShortcutName = "a/b";

            Assert.False(creator.IsNameWarning);
            Assert.False(creator.HelperIsWarning);
        }

        [Fact]
        public void EditingTheName_ClearsTheNameWarning()
        {
            var creator = WithDestinationAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));

            creator.ShortcutName = "ex2";

            Assert.False(creator.IsNameWarning);
            Assert.Equal(CreatorState.DestinationSelected, creator.State);
            Assert.Equal(CreatorViewModel.DefaultHelperText, creator.HelperText);
        }

        [Theory]
        [InlineData(DestinationError.NotFound, "That destination wasn\u2019t found.")]
        [InlineData(DestinationError.AccessDenied, "Access to that destination was denied.")]
        [InlineData(DestinationError.Unsupported, "That destination type isn\u2019t supported.")]
        public void InvalidDestination_ShowsSpecificMessage_AndDisablesCreate(DestinationError error, string message)
        {
            var creator = WithDestinationAndName();
            creator.Destination = "C:\\missing";
            creator.ShortcutName = "m";

            creator.DestinationError = error;

            Assert.Equal(CreatorState.InvalidDestination, creator.State);
            Assert.True(creator.ShowDestinationError);
            Assert.False(creator.CanCreate);
            Assert.Equal("C:\\missing", creator.Destination);
            Assert.Equal(message, creator.HelperText);
            Assert.True(creator.HelperIsError);
        }

        [Fact]
        public void DestinationError_WinsOverANameWarning_ForTheHelperLine()
        {
            var creator = WithDestinationAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));

            creator.DestinationError = DestinationError.NotFound;

            Assert.Equal(CreatorState.InvalidDestination, creator.State);
            Assert.Equal("That destination wasn\u2019t found.", creator.HelperText);
        }

        [Fact]
        public void EditingTheDestination_ClearsTheDestinationError()
        {
            var creator = WithDestinationAndName();
            creator.DestinationError = DestinationError.NotFound;

            creator.Destination = "C:\\fixed";

            Assert.Equal(DestinationError.None, creator.DestinationError);
            Assert.False(creator.ShowDestinationError);
        }

        [Fact]
        public void EmptyError_IsNotAVisibleDestinationError()
        {
            var creator = Create();

            creator.DestinationError = DestinationError.Empty;

            Assert.False(creator.ShowDestinationError);
            Assert.Equal(CreatorState.Empty, creator.State);
        }

        [Fact]
        public void Success_ShowsCreatedMessage_AndClearOnNextEdit()
        {
            var creator = Create();

            creator.SetSuccess("l-vicki");

            Assert.Equal(CreatorState.Success, creator.State);
            Assert.True(creator.IsSuccess);
            Assert.False(creator.HelperIsError);
            Assert.Equal("Created l-vicki", creator.HelperText);

            creator.ShortcutName = "next";
            Assert.False(creator.IsSuccess);
        }

        [Fact]
        public void Clear_ReturnsToEmpty()
        {
            var creator = WithDestinationAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));

            creator.Clear();

            Assert.Equal(CreatorState.Empty, creator.State);
            Assert.Equal(string.Empty, creator.Destination);
            Assert.Equal(string.Empty, creator.ShortcutName);
            Assert.Null(creator.DestinationKind);
        }

        [Fact]
        public void RaisesPropertyChanged_ForDerivedState()
        {
            var creator = Create();
            var changed = new System.Collections.Generic.List<string?>();
            creator.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            creator.IsSaving = true;

            Assert.Contains(nameof(CreatorViewModel.State), changed);
            Assert.Contains(nameof(CreatorViewModel.CanCreate), changed);
            Assert.Contains(nameof(CreatorViewModel.IsInputEnabled), changed);
        }
    }
}
