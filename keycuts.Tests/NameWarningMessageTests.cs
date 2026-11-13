#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#213: a name that already exists "one to one" says so, and a conflict that is a keycut is described as one.</summary>
    public class NameWarningMessageTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-namewarn-" + Guid.NewGuid().ToString("N"));

        public NameWarningMessageTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        const string Downloads = @"C:\Users\me\Downloads";

        static CreatorViewModel NewCreator()
        {
            return new CreatorViewModel(
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                (target, name, description, useEnvironmentVariables, replace, ct) => Task.FromResult(CreateResult.Success(null!)),
                TestNameChecks.NoWarnings);
        }

        // A keycut file in a folder, and a name check run against a user PATH that holds that folder but isn't the output folder
        (string Folder, NameCheck Check) CheckAgainstAKeycutOnPath(string name, string target)
        {
            var other = Path.Combine(root, "other");
            Directory.CreateDirectory(other);
            var file = Path.Combine(other, name + ".bat");
            File.WriteAllLines(file, Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Folder, target, file)));

            var output = Path.Combine(root, "output");
            Directory.CreateDirectory(output);
            var env = new NameConflictEnvironment { UserPath = other, FileExists = File.Exists };

            return (other, NameCheck.Run(name, output, true, env));
        }

        [Theory]
        [InlineData(@"C:\Users\me\Downloads", @"C:\Users\me\Downloads", true)]
        [InlineData(@"C:\Users\me\Downloads", @"c:\users\ME\downloads\", true)]
        [InlineData(@"C:\Users\me\Downloads\", @"C:\Users\me\Downloads", true)]
        [InlineData(@"""C:\Users\me\Downloads""", @"C:\Users\me\Downloads", true)]
        [InlineData("https://www.google.com", "https://www.google.com/", true)]
        [InlineData("HTTPS://WWW.GOOGLE.COM", "https://www.google.com", true)]
        [InlineData(@"C:\", @"C:\", true)]
        [InlineData(@"C:\Users\me\Downloads", @"C:\Users\me\Documents", false)]
        [InlineData("https://www.google.com", "https://www.google.com/search", false)]
        [InlineData("https://www.google.com", @"C:\Users\me\Downloads", false)]
        [InlineData("", "", false)]
        [InlineData(@"C:\Users\me\Downloads", "", false)]
        public void IsSame_IgnoresCase_QuotesAndATrailingSlash(string first, string second, bool same)
        {
            Assert.Equal(same, TargetComparison.IsSame(first, second));
            Assert.Equal(same, TargetComparison.IsSame(second, first));
        }

        [Fact]
        public void IsSame_ExpandsEnvironmentVariables()
        {
            var profile = Environment.GetEnvironmentVariable("USERPROFILE")!;

            Assert.True(TargetComparison.IsSame(@"%USERPROFILE%\Downloads", Path.Combine(profile, "Downloads")));
            Assert.False(TargetComparison.IsSame(@"%USERPROFILE%\Downloads", Path.Combine(profile, "Documents")));
        }

        [Fact]
        public void AnExistingKeycut_WithTheSameTarget_SaysItAlreadyExistsOneToOne()
        {
            var check = TestNameChecks.Existing("dl", Downloads);

            Assert.Equal("\u201cdl\u201d already exists and opens this target.", CreatorViewModel.GetNameWarningMessage(check, Downloads + @"\"));
        }

        [Fact]
        public void AnExistingKeycut_WithAnotherTarget_StillSaysWhatItOpens()
        {
            var check = TestNameChecks.Existing("dl", Downloads);

            Assert.Equal("\u201cdl\u201d already exists and opens C:\\Users\\me\\Downloads.", CreatorViewModel.GetNameWarningMessage(check, @"C:\Users\me\Documents"));
            // And with no target to compare (a rename, or none typed yet) it is the same
            Assert.Equal("\u201cdl\u201d already exists and opens C:\\Users\\me\\Downloads.", CreatorViewModel.GetNameWarningMessage(check));
        }

        [Fact]
        public void TheCreatorsWarning_ComparesWithItsOwnTarget_AsItChanges()
        {
            var creator = NewCreator();
            creator.ShortcutName = "dl";
            creator.Target = Downloads;
            creator.SetNameWarning(TestNameChecks.Existing("dl", Downloads));

            Assert.Equal("\u201cdl\u201d already exists and opens this target.", creator.HelperText);

            creator.Target = @"C:\Users\me\Documents";
            creator.SetNameWarning(TestNameChecks.Existing("dl", Downloads));

            Assert.Contains("already exists and opens C:\\Users\\me\\Downloads", creator.HelperText);
        }

        [Fact]
        public void AConflictThatIsAKeycut_IsDescribedAsOne_WithWhatItOpens()
        {
            var (folder, check) = CheckAgainstAKeycutOnPath("dl", Downloads);
            var conflict = check.MainConflict!;

            Assert.Equal(Path.Combine(folder, "dl.bat"), conflict.Path);
            Assert.Equal(Downloads, conflict.Keycut!.Target);
            Assert.Equal(
                $"\u201cdl\u201d is already a keycut in {folder} that opens {Downloads}, so this keycut would be ignored.",
                CreatorViewModel.GetConflictMessage("dl", conflict, @"C:\Users\me\Documents"));
            Assert.Equal(
                $"\u201cdl\u201d is already a keycut in {folder} that opens {Downloads}, so this keycut would be ignored.",
                CreatorViewModel.GetConflictMessage("dl", conflict));
        }

        [Fact]
        public void AConflictThatIsAKeycut_WithTheSameTarget_SaysItAlreadyExistsOneToOne()
        {
            var (folder, check) = CheckAgainstAKeycutOnPath("dl", Downloads);

            Assert.Equal(
                $"\u201cdl\u201d already exists in {folder} and opens this target.",
                CreatorViewModel.GetConflictMessage("dl", check.MainConflict!, Downloads));
            Assert.Equal(
                $"\u201cdl\u201d already exists in {folder} and opens this target.",
                CreatorViewModel.GetNameWarningMessage(check, Downloads));
        }

        [Fact]
        public void AConflictThatIsABatFileButNotAKeycut_IsStillAProgram()
        {
            var other = Path.Combine(root, "other");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "tool.bat"), "@echo off\r\necho hi\r\n");
            var env = new NameConflictEnvironment { UserPath = other, FileExists = File.Exists };

            var check = NameCheck.Run("tool", Path.Combine(root, "output"), true, env);

            Assert.Null(check.MainConflict!.Keycut);
            Assert.Equal(
                $"\u201ctool\u201d already opens {Path.Combine(other, "tool.bat")}, so Win+R would never run this keycut.",
                CreatorViewModel.GetConflictMessage("tool", check.MainConflict));
        }

        [Fact]
        public void AHeaderlessLauncher_IsAProgram_EvenThoughTheRepositoryParserAcceptsIt()
        {
            var other = Path.Combine(root, "other");
            Directory.CreateDirectory(other);
            var file = Path.Combine(other, "app.bat");
            File.WriteAllText(file, "@ECHO OFF\r\nSTART \"\" /B \"C:\\Tools\\app.exe\"\r\nEXIT\r\n");
            var env = new NameConflictEnvironment { UserPath = other, FileExists = File.Exists };

            var check = NameCheck.Run("app", Path.Combine(root, "output"), true, env);

            Assert.Equal(file, check.MainConflict!.Path);
            Assert.Null(check.MainConflict.Keycut);
            Assert.StartsWith("\u201capp\u201d already opens ", CreatorViewModel.GetConflictMessage("app", check.MainConflict, @"C:\Tools\app.exe"));
        }

        [Fact]
        public void ARenameCheck_WithoutAskingAboutReplacing_StillDescribesAKeycutConflict()
        {
            var other = Path.Combine(root, "other");
            Directory.CreateDirectory(other);
            var file = Path.Combine(other, "dl.bat");
            File.WriteAllLines(file, Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Folder, Downloads, file)));
            var env = new NameConflictEnvironment { UserPath = other, FileExists = File.Exists };

            var check = NameCheck.Run("dl", Path.Combine(root, "output"), false, env);

            Assert.Null(check.Existing);
            Assert.Equal(Downloads, check.MainConflict!.Keycut!.Target);
        }

        [Fact]
        public void TheConfirmation_UsesTheSameWords()
        {
            var (folder, check) = CheckAgainstAKeycutOnPath("dl", Downloads);

            var create = ConfirmViewModel.ForCreate(check, Downloads);
            var rename = ConfirmViewModel.ForRename(check);

            Assert.Equal(new[] { $"\u201cdl\u201d already exists in {folder} and opens this target." }, create.Warnings);
            Assert.Equal(new[] { $"\u201cdl\u201d is already a keycut in {folder} that opens {Downloads}, so this keycut would be ignored." }, rename.Warnings);
        }
    }
}
