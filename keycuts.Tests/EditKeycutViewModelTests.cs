#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class EditKeycutViewModelTests
    {
        static Keycut NewKeycut()
        {
            return new Keycut
            {
                Name = "gmail",
                Kind = KeycutKind.Url,
                RawType = ShortcutType.Url,
                Description = "Work email",
                Target = "https://mail.google.com",
                FilePath = @"C:\fixture\gmail.bat"
            };
        }

        sealed class Harness
        {
            public List<KeycutEdit> Saves { get; } = new List<KeycutEdit>();

            public Func<KeycutEdit, Task<Keycut>>? Behavior { get; set; }

            public List<bool> Closed { get; } = new List<bool>();

            public List<EditFocusTarget> Focus { get; } = new List<EditFocusTarget>();

            public EditKeycutViewModel Form { get; }

            public Harness(Func<string, NameCheck>? checkName = null, Func<NameCheck, bool>? confirm = null)
            {
                Form = new EditKeycutViewModel(NewKeycut(), SaveAsync, checkName, confirm);
                Form.CloseRequested += (s, saved) => Closed.Add(saved);
                Form.FocusRequested += (s, target) => Focus.Add(target);
            }

            private Task<Keycut> SaveAsync(KeycutEdit edit)
            {
                Saves.Add(edit);
                return Behavior != null
                    ? Behavior(edit)
                    : Task.FromResult(NewKeycut() with { Name = edit.Name, Target = edit.Target, Description = edit.Description });
            }
        }

        static NameCheck Conflicts(string name)
        {
            return TestNameChecks.Conflict(name, NameConflictEffect.Hidden, @"C:\Windows\" + name + ".exe");
        }

        [Fact]
        public void TheFormStartsWithTheKeycutsValues_AndNothingToSave()
        {
            var form = new Harness().Form;

            Assert.Equal("https://mail.google.com", form.Target);
            Assert.Equal("gmail", form.Name);
            Assert.Equal("Work email", form.Description);
            Assert.False(form.HasChanges);
            Assert.True(form.CanSave);
            Assert.True(form.IsInputEnabled);
            Assert.Equal("Edit keycut", form.Title);
            Assert.Null(form.Saved);
        }

        [Fact]
        public async Task SavingWithNothingChanged_JustCloses_AndWritesNothing()
        {
            var harness = new Harness();

            await harness.Form.SaveCommand.ExecuteAsync();

            Assert.Empty(harness.Saves);
            Assert.Equal(new[] { true }, harness.Closed);
            Assert.Null(harness.Form.Saved);
        }

        [Fact]
        public async Task WhitespaceAroundTheValues_IsNotAChange()
        {
            var harness = new Harness();
            harness.Form.Target = "  https://mail.google.com ";
            harness.Form.Name = " gmail  ";
            harness.Form.Description = "Work email   ";

            Assert.False(harness.Form.HasChanges);

            await harness.Form.SaveAsync();

            Assert.Empty(harness.Saves);
        }

        [Fact]
        public async Task Save_PassesTheTrimmedValues_SetsSaved_AndCloses()
        {
            var harness = new Harness();
            harness.Form.Target = "  https://example.com/new  ";
            harness.Form.Name = " mail ";
            harness.Form.Description = " Personal ";

            Assert.True(harness.Form.HasChanges);
            await harness.Form.SaveCommand.ExecuteAsync();

            var edit = Assert.Single(harness.Saves);
            Assert.Equal(new KeycutEdit("https://example.com/new", "mail", "Personal"), edit);
            Assert.Equal("mail", harness.Form.Saved!.Name);
            Assert.Equal(new[] { true }, harness.Closed);
            Assert.False(harness.Form.IsSaving);
            Assert.False(harness.Form.HasSaveError);
        }

        [Fact]
        public async Task AnEmptiedDescription_IsAChange()
        {
            var harness = new Harness();
            harness.Form.Description = string.Empty;

            await harness.Form.SaveAsync();

            Assert.Equal(string.Empty, harness.Saves.Single().Description);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void SaveNeedsATargetAndAName(string blank)
        {
            var form = new Harness().Form;

            form.Target = blank;
            Assert.False(form.CanSave);

            form.Target = "https://example.com";
            Assert.True(form.CanSave);

            form.Name = blank;
            Assert.False(form.CanSave);
        }

        [Fact]
        public async Task AnInvalidName_StaysInTheForm_BesideTheNameField_WithoutSaving()
        {
            var harness = new Harness();
            harness.Form.Name = "what?";

            await harness.Form.SaveAsync();

            Assert.Empty(harness.Saves);
            Assert.Empty(harness.Closed);
            Assert.Equal(CreatorViewModel.InvalidNameMessage, harness.Form.NameError);
            Assert.True(harness.Form.HasNameError);
            Assert.Equal(new[] { EditFocusTarget.Name }, harness.Focus);
        }

        [Fact]
        public async Task ANameThatIsAKeycutsCommand_SaysSo()
        {
            var harness = new Harness();
            harness.Form.Name = "keycuts";

            await harness.Form.SaveAsync();

            Assert.Empty(harness.Saves);
            Assert.Equal(CreatorViewModel.GetInvalidNameMessage("keycuts"), harness.Form.NameError);
            Assert.Contains("keycuts command", harness.Form.NameError);
        }

        [Fact]
        public async Task ATakenName_IsExplainedBesideTheName()
        {
            var harness = new Harness { Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, "taken")) };
            harness.Form.Name = "other";

            await harness.Form.SaveAsync();

            Assert.Equal("\u201Cother\u201D already exists. Choose another name.", harness.Form.NameError);
            Assert.Empty(harness.Closed);
            Assert.Equal(new[] { EditFocusTarget.Name }, harness.Focus);
            Assert.False(harness.Form.IsSaving);
            Assert.Null(harness.Form.Saved);
        }

        [Theory]
        [InlineData(TargetError.NotFound)]
        [InlineData(TargetError.AccessDenied)]
        [InlineData(TargetError.Unsupported)]
        public async Task ATargetThatIsRefused_IsExplainedBesideTheTarget_AsTheCreatorDoes(TargetError error)
        {
            var harness = new Harness
            {
                Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.InvalidTarget, "no", null, error))
            };
            harness.Form.Target = @"C:\nowhere";

            await harness.Form.SaveAsync();

            Assert.Equal(CreatorViewModel.GetTargetErrorMessage(error), harness.Form.TargetError);
            Assert.True(harness.Form.HasTargetError);
            Assert.Empty(harness.Closed);
            Assert.Equal(new[] { EditFocusTarget.Target }, harness.Focus);
        }

        [Fact]
        public async Task AFileThatCantBeEditedHere_SaysToUseNotepad()
        {
            var harness = new Harness { Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.NotEditable, "no")) };
            harness.Form.Description = "x";

            await harness.Form.SaveAsync();

            Assert.Equal("This keycut can\u2019t be edited here. Edit it in Notepad.", harness.Form.SaveError);
            Assert.True(harness.Form.HasSaveError);
            Assert.Empty(harness.Closed);
        }

        [Fact]
        public async Task AKeycutThatIsGone_IsSaidToBeGone()
        {
            var harness = new Harness { Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.NotFound, "gone")) };
            harness.Form.Description = "x";

            await harness.Form.SaveAsync();

            Assert.Equal(EditKeycutViewModel.MissingMessage, harness.Form.SaveError);
        }

        [Fact]
        public async Task AnotherFailure_SaysWhy_UsingTheFileSystemsReason()
        {
            var inner = new System.IO.IOException("The disk is full.");
            var harness = new Harness { Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.InUse, "Can't save.", inner)) };
            harness.Form.Description = "x";

            await harness.Form.SaveAsync();

            Assert.Equal("Couldn\u2019t save \u201Cgmail\u201D: The disk is full.", harness.Form.SaveError);
        }

        [Fact]
        public async Task AnUnexpectedException_IsShownToo_AndTheFormStaysUsable()
        {
            var harness = new Harness { Behavior = edit => Task.FromException<Keycut>(new InvalidOperationException("boom")) };
            harness.Form.Description = "x";

            await harness.Form.SaveAsync();

            Assert.Equal("Couldn\u2019t save \u201Cgmail\u201D: boom", harness.Form.SaveError);
            Assert.False(harness.Form.IsSaving);
            Assert.True(harness.Form.CanSave);
        }

        [Fact]
        public async Task TypingInAField_ClearsWhatWasSaidAboutIt_AndTheGeneralError()
        {
            var harness = new Harness { Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.InvalidTarget, "no", null, TargetError.NotFound)) };
            harness.Form.Target = @"C:\nowhere";
            await harness.Form.SaveAsync();
            Assert.True(harness.Form.HasTargetError);

            harness.Form.Name = "mail";
            Assert.True(harness.Form.HasTargetError);

            harness.Form.Target = @"C:\somewhere";
            Assert.False(harness.Form.HasTargetError);

            harness.Behavior = edit => Task.FromException<Keycut>(new KeycutRepositoryException(KeycutRepositoryError.NotEditable, "no"));
            await harness.Form.SaveAsync();
            Assert.True(harness.Form.HasSaveError);

            harness.Form.Description = "typing";
            Assert.False(harness.Form.HasSaveError);
        }

        [Fact]
        public async Task TheFieldsAreOffWhileSaving_AndASecondSaveWaits()
        {
            var gate = new TaskCompletionSource<Keycut>();
            var harness = new Harness { Behavior = edit => gate.Task };
            harness.Form.Description = "x";
            var states = new List<bool>();
            harness.Form.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EditKeycutViewModel.IsInputEnabled))
                {
                    states.Add(harness.Form.IsInputEnabled);
                }
            };

            var first = harness.Form.SaveCommand.ExecuteAsync();

            Assert.True(harness.Form.IsSaving);
            Assert.False(harness.Form.IsInputEnabled);
            Assert.False(harness.Form.CanSave);
            Assert.False(harness.Form.SaveCommand.CanExecute(null));

            await harness.Form.SaveCommand.ExecuteAsync();
            Assert.Single(harness.Saves);

            gate.SetResult(NewKeycut());
            await first;

            Assert.Equal(new[] { false, true }, states);
            Assert.Single(harness.Saves);
            Assert.Equal(new[] { true }, harness.Closed);
        }

        [Fact]
        public void Cancel_ClosesWithoutSaving()
        {
            var harness = new Harness();
            harness.Form.Description = "changed";

            harness.Form.CancelCommand.Execute(null);

            Assert.Equal(new[] { false }, harness.Closed);
            Assert.Empty(harness.Saves);
            Assert.Null(harness.Form.Saved);
        }

        #region Names other commands use (#153)

        [Fact]
        public async Task ANewNameAnotherCommandUses_AsksFirst_AndAYesSaves()
        {
            var asked = new List<NameCheck>();
            var harness = new Harness(Conflicts, check => { asked.Add(check); return true; });
            harness.Form.Name = "notepad";

            await harness.Form.SaveAsync();

            Assert.Equal("notepad", asked.Single().Name);
            Assert.Equal("notepad", harness.Saves.Single().Name);
            Assert.Equal(new[] { true }, harness.Closed);
        }

        [Fact]
        public async Task ANoToAConflict_StaysInTheForm_AtTheName_WithNothingSaved()
        {
            var harness = new Harness(Conflicts, check => false);
            harness.Form.Name = "notepad";

            await harness.Form.SaveAsync();

            Assert.Empty(harness.Saves);
            Assert.Empty(harness.Closed);
            Assert.Equal(new[] { EditFocusTarget.Name }, harness.Focus);
            Assert.False(harness.Form.IsSaving);
            Assert.Equal("notepad", harness.Form.Name);
        }

        [Fact]
        public async Task WithNobodyToAsk_AConflictIsNotSaved()
        {
            var harness = new Harness(Conflicts);
            harness.Form.Name = "notepad";

            await harness.Form.SaveAsync();

            Assert.Empty(harness.Saves);
            Assert.Empty(harness.Closed);
        }

        [Theory]
        [InlineData("gmail")]
        [InlineData("GMAIL")]
        public async Task TheSameName_OrADifferentCase_ChecksNothing(string name)
        {
            var checks = new List<string>();
            var harness = new Harness(n => { checks.Add(n); return Conflicts(n); }, check => false);
            harness.Form.Name = name;
            harness.Form.Description = "changed";

            await harness.Form.SaveAsync();

            Assert.Empty(checks);
            Assert.Single(harness.Saves);
        }

        [Fact]
        public async Task ANameWithoutConflicts_SavesWithoutAsking()
        {
            var asked = 0;
            var harness = new Harness(n => NameCheck.None(n), check => { asked++; return false; });
            harness.Form.Name = "mail";

            await harness.Form.SaveAsync();

            Assert.Equal(0, asked);
            Assert.Single(harness.Saves);
        }

        [Fact]
        public async Task ACheckThatFails_WarnsAboutNothing()
        {
            var harness = new Harness(n => throw new InvalidOperationException("boom"), check => false);
            harness.Form.Name = "mail";

            await harness.Form.SaveAsync();

            Assert.Equal("mail", harness.Saves.Single().Name);
        }

        [Fact]
        public async Task ChangingOnlyTheDescription_ChecksNoName()
        {
            var checks = 0;
            var harness = new Harness(n => { checks++; return NameCheck.None(n); });
            harness.Form.Description = "changed";

            await harness.Form.SaveAsync();

            Assert.Equal(0, checks);
        }

        #endregion Names other commands use (#153)
    }
}
