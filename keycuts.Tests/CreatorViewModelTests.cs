#nullable enable
using System;
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

        static CreatorViewModel WithTargetAndName()
        {
            var creator = Create();
            creator.Target = "https://example.com";
            creator.TargetKind = KeycutKind.Url;
            creator.ShortcutName = "ex";
            return creator;
        }

        [Fact]
        public void Created_ShowsForThreeSeconds_ByDefault()
        {
            Assert.Equal(TimeSpan.FromSeconds(3), Create().SuccessDuration);
        }

        [Fact]
        public void Empty_ShowsArrow_DefaultHelper_AndCreateDisabled()
        {
            var creator = Create();

            Assert.Equal(CreatorState.Empty, creator.State);
            Assert.Equal(IconKind.Arrow, creator.TargetIcon);
            Assert.Equal(string.Empty, creator.HelperText);
            Assert.False(creator.ShowHelperLine);
            Assert.False(creator.HasNameStatus);
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
            creator.Target = "x";
            creator.TargetKind = kind;

            Assert.Equal(icon, creator.TargetIcon);
        }

        [Fact]
        public void EditingTheTarget_ResetsTheIconToArrow()
        {
            var creator = WithTargetAndName();

            creator.Target = "https://example.org";

            Assert.Equal(IconKind.Arrow, creator.TargetIcon);
        }

        [Fact]
        public void CreateNeedsTargetAndName()
        {
            var creator = Create();
            creator.Target = "C:\\temp";
            Assert.Equal(CreatorState.TargetSelected, creator.State);
            Assert.False(creator.CanCreate);

            creator.ShortcutName = "   ";
            Assert.False(creator.CanCreate);

            creator.ShortcutName = "tmp";
            Assert.True(creator.CanCreate);

            creator.Target = string.Empty;
            Assert.False(creator.CanCreate);
        }

        [Fact]
        public void Saving_DisablesInputAndCreate_AndHidesDragHover()
        {
            var creator = WithTargetAndName();
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
        public void ExistingName_IsAWarningOnTheNameField_WithTheCurrentTarget()
        {
            var creator = WithTargetAndName();

            creator.SetNameWarning(TestNameChecks.Existing("l-vicki", "https://old.example.com"));

            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.True(creator.IsNameWarning);
            Assert.False(creator.IsNameError);
            Assert.False(creator.ShowTargetError);
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
            var creator = WithTargetAndName();

            creator.SetNameWarning(TestNameChecks.Conflict("ex", effect));

            Assert.Equal(CreatorState.NameWarning, creator.State);
            Assert.True(creator.HelperIsWarning);
            Assert.Equal(message, creator.HelperText);
        }

        [Fact]
        public void NameWarning_ListsTheReplaceFirst_ThenTheConflictThatMattersMost()
        {
            var check = new NameCheck("ex", new Keycut { Name = "ex", Target = @"D:\old" },
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
            var creator = WithTargetAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));
            creator.ShortcutName = "a/b";

            Assert.False(creator.IsNameWarning);
            Assert.False(creator.HelperIsWarning);
        }

        [Fact]
        public void EditingTheName_ClearsTheNameWarning()
        {
            var creator = WithTargetAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));

            creator.ShortcutName = "ex2";

            Assert.False(creator.IsNameWarning);
            Assert.Equal(CreatorState.TargetSelected, creator.State);
            Assert.Equal(string.Empty, creator.HelperText);
        }

        [Theory]
        [InlineData(TargetError.NotFound, "That target wasn\u2019t found.")]
        [InlineData(TargetError.AccessDenied, "Access to that target was denied.")]
        [InlineData(TargetError.Unsupported, "That target type isn\u2019t supported.")]
        public void InvalidTarget_ShowsSpecificMessage_AndDisablesCreate(TargetError error, string message)
        {
            var creator = WithTargetAndName();
            creator.Target = "C:\\missing";
            creator.ShortcutName = "m";

            creator.TargetError = error;

            Assert.Equal(CreatorState.InvalidTarget, creator.State);
            Assert.True(creator.ShowTargetError);
            Assert.False(creator.CanCreate);
            Assert.Equal("C:\\missing", creator.Target);
            Assert.Equal(message, creator.HelperText);
            Assert.True(creator.HelperIsError);
        }

        [Fact]
        public void TargetError_WinsOverANameWarning_ForTheHelperLine()
        {
            var creator = WithTargetAndName();
            creator.SetNameWarning(TestNameChecks.Existing("ex"));

            creator.TargetError = TargetError.NotFound;

            Assert.Equal(CreatorState.InvalidTarget, creator.State);
            Assert.Equal("That target wasn\u2019t found.", creator.HelperText);
        }

        [Fact]
        public void EditingTheTarget_ClearsTheTargetError()
        {
            var creator = WithTargetAndName();
            creator.TargetError = TargetError.NotFound;

            creator.Target = "C:\\fixed";

            Assert.Equal(TargetError.None, creator.TargetError);
            Assert.False(creator.ShowTargetError);
        }

        [Fact]
        public void EmptyError_IsNotAVisibleTargetError()
        {
            var creator = Create();

            creator.TargetError = TargetError.Empty;

            Assert.False(creator.ShowTargetError);
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
            Assert.Equal(CreatorViewModel.CreatedText, creator.HelperText);

            creator.ShortcutName = "next";
            Assert.False(creator.IsSuccess);
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
