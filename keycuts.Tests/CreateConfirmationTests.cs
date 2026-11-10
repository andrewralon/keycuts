using System;
using System.IO;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#153: the CLI's y / n before a replace or a name conflict, the setting it follows, and the name check.</summary>
    public class CreateConfirmationTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-confirm-" + Guid.NewGuid().ToString("N"));

        public CreateConfirmationTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        static NameCheck Replace()
        {
            return TestNameChecks.Existing("gmail", "https://old.example.com");
        }

        static CreateDecision Decide(NameCheck check, bool force, bool redirected, char key, out bool overwrite, out string output, out int reads,
            bool askBeforeReplacing = true)
        {
            var writer = new StringWriter();
            var count = 0;
            var decision = CreateConfirmation.Decide(check, "https://new.example.com", force, askBeforeReplacing, redirected,
                () => { count++; return key; }, writer, out overwrite);
            output = writer.ToString();
            reads = count;
            return decision;
        }

        #region CLI confirmation

        [Fact]
        public void NothingToWarnAbout_CreatesWithoutAsking_ButNeverReplacesUnasked()
        {
            // A keycut that appears after the check (or a lookup that failed) must not be replaced silently
            var decision = Decide(NameCheck.None("gmail"), false, false, 'n', out var overwrite, out var output, out var reads);

            Assert.Equal(CreateDecision.Create, decision);
            Assert.False(overwrite);
            Assert.Equal(0, reads);
            Assert.Equal(string.Empty, output);
        }

        [Fact]
        public void WithAskBeforeReplacingOff_ReplacingNeedsNoQuestion()
        {
            var decision = Decide(NameCheck.None("gmail"), false, false, 'n', out var overwrite, out _, out var reads, askBeforeReplacing: false);

            Assert.Equal(CreateDecision.Create, decision);
            Assert.True(overwrite);
            Assert.Equal(0, reads);
        }

        [Fact]
        public void AYesToANameConflict_DoesNotAllowReplacing()
        {
            var decision = Decide(TestNameChecks.Conflict("ex"), false, false, 'y', out var overwrite, out _, out _);

            Assert.Equal(CreateDecision.Create, decision);
            Assert.False(overwrite);
        }

        [Theory]
        [InlineData(@"C:\Shortcuts\tool.exe", "tool.exe")]
        [InlineData(@"C:\Shortcuts\tool", "tool")]
        [InlineData("tool.exe", "tool.exe")]
        [InlineData("gmail", "gmail")]
        [InlineData("my.site", "my")]
        public void ShortcutGetName_IsTheBareNameTheKeycutGets(string shortcut, string name)
        {
            Assert.Equal(name, Shortcut.GetName(shortcut));
        }

        [Fact]
        public void AFullPathEndingInExe_FindsTheExistingKeycut()
        {
            var shortcut = Path.Combine(root, "tool.exe");
            File.WriteAllLines(shortcut + ".bat", Runner.BuildShortcutLines(
                Shortcut.FromParts(ShortcutType.Url, "https://old.example.com", shortcut + ".bat")));

            var check = NameCheck.Run(Shortcut.GetName(shortcut), Shortcut.GetFolder(shortcut, @"C:\elsewhere"), true, NoCommands);

            Assert.Equal("https://old.example.com", check.Existing!.Destination);
        }

        [Fact]
        public void Force_SkipsBothPrompts()
        {
            var both = new NameCheck("gmail", Replace().Existing, TestNameChecks.Conflict("gmail").Conflicts);

            var decision = Decide(both, true, false, 'n', out var overwrite, out var output, out var reads);

            Assert.Equal(CreateDecision.Create, decision);
            Assert.True(overwrite);
            Assert.Equal(0, reads);
            Assert.Equal(string.Empty, output);
        }

        [Fact]
        public void AReplace_ShowsTheOldAndNewDestinations_AndYesOverwrites()
        {
            var decision = Decide(Replace(), false, false, 'Y', out var overwrite, out var output, out var reads);

            Assert.Equal(CreateDecision.Create, decision);
            Assert.True(overwrite);
            Assert.Equal(1, reads);
            Assert.Contains("Shortcut \"gmail\" already exists.", output);
            Assert.Contains("It opens:      https://old.example.com", output);
            Assert.Contains("It would open: https://new.example.com", output);
            Assert.Contains("Overwrite? (y / n)", output);
        }

        [Fact]
        public void AReplace_AnsweredNo_KeepsTheExistingShortcut()
        {
            var decision = Decide(Replace(), false, false, 'n', out var overwrite, out var output, out _);

            Assert.Equal(CreateDecision.Declined, decision);
            Assert.False(overwrite);
            Assert.Contains("Kept the existing shortcut.", output);
        }

        [Fact]
        public void ANameConflict_AsksToCreateAnyway_AndNamesTheOtherProgram()
        {
            var check = TestNameChecks.Conflict("ex", NameConflictEffect.Hidden);

            var decision = Decide(check, false, false, 'x', out _, out var output, out _);

            Assert.Equal(CreateDecision.Declined, decision);
            Assert.Contains("\"ex\" already opens " + TestNameChecks.ConflictPath + ", so Win+R would never run this shortcut.", output);
            Assert.Contains("Create it anyway? (y / n)", output);
            Assert.Contains("Didn't create the shortcut.", output);
        }

        [Fact]
        public void RedirectedInput_NeverWaits_AndNeedsConfirmation()
        {
            var decision = Decide(Replace(), false, true, 'y', out var overwrite, out var output, out var reads);

            Assert.Equal(CreateDecision.ConfirmationNeeded, decision);
            Assert.False(overwrite);
            Assert.Equal(0, reads);
            Assert.Contains(CreateConfirmation.RetryWithForceMessage, output);
        }

        [Fact]
        public void ConfirmationNeeded_HasItsOwnExitCode()
        {
            Assert.Equal(14, (int)ExitCode.ConfirmationNeeded);
        }

        #endregion CLI confirmation

        #region The setting

        [Theory]
        [InlineData(null, null, true)]   // never set: ask
        [InlineData(null, 1, false)]     // Force overwrite was on: don't ask
        [InlineData(null, 0, true)]      // Force overwrite was off: ask
        [InlineData(1, 1, true)]         // the new value wins once it's saved
        [InlineData(0, 0, false)]
        public void AskBeforeReplacing_CarriesTheOldForceOverwriteValueOver(int? ask, int? force, bool expected)
        {
            Assert.Equal(expected, RegistryStuff.ResolveAskBeforeReplacing(ask, force, Runner.DefaultAskBeforeReplacing));
        }

        [Fact]
        public void AskBeforeReplacing_IgnoresAValueOfTheWrongType()
        {
            Assert.True(RegistryStuff.ResolveAskBeforeReplacing("0", "1", true));
        }

        #endregion The setting

        #region Name check

        static readonly NameConflictEnvironment NoCommands = new NameConflictEnvironment();

        [Fact]
        public void NameCheck_ReportsAnExistingKeycut_OnlyWhenReplacingAsks()
        {
            File.WriteAllLines(Path.Combine(root, "gmail.bat"), Runner.BuildShortcutLines(
                Shortcut.FromParts(ShortcutType.Url, "https://mail.example.com", Path.Combine(root, "gmail.bat"))));

            var asks = NameCheck.Run(" GMail ", root, true, NoCommands);
            var doesNotAsk = NameCheck.Run("gmail", root, false, NoCommands);

            Assert.Equal("GMail", asks.Name);
            Assert.Equal("https://mail.example.com", asks.Existing!.Destination);
            Assert.True(asks.HasWarnings);
            Assert.Null(doesNotAsk.Existing);
            Assert.False(doesNotAsk.HasWarnings);
        }

        [Fact]
        public void NameCheck_AlwaysReportsConflicts()
        {
            var env = new NameConflictEnvironment { SystemFolder = @"C:\Windows\System32", FileExists = path => path == @"C:\Windows\System32\notepad.exe" };

            var check = NameCheck.Run("notepad", root, false, env);

            Assert.Equal(@"C:\Windows\System32\notepad.exe", check.MainConflict!.Path);
        }

        [Fact]
        public void RepositoryFind_ReturnsAnUnparseableFile_WithAnEmptyDestination()
        {
            File.WriteAllText(Path.Combine(root, "junk.bat"), "not a keycut");

            var found = new KeycutRepository(root).Find("JUNK");

            Assert.Equal(Path.Combine(root, "junk.bat"), found!.FilePath);
            Assert.Equal(string.Empty, found.Destination);
            Assert.Null(new KeycutRepository(root).Find("missing"));
            Assert.Null(new KeycutRepository(Path.Combine(root, "nope")).Find("junk"));
        }

        #endregion Name check
    }
}
