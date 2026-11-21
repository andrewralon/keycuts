using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#212: a hand-written keycut's target with its SET variables filled in, for display only. Nothing is run.</summary>
    public class ScriptVariablesTests
    {
        private static readonly Dictionary<string, string> Env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USERPROFILE"] = @"C:\Users\me"
        };

        private static string Lookup(string name) => Env.TryGetValue(name, out var value) ? value : null;

        private static ResolvedTarget Resolve(string target, params string[] lines)
        {
            return ScriptVariables.Resolve(lines, target, Lookup);
        }

        private static string Corpus(string name)
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(folder.FullName, "keycuts.sln")))
            {
                folder = folder.Parent;
            }

            return Path.Combine(folder.FullName, "tests", "corpus", "variables", name);
        }

        // Read through the repository, as the Manager reads it: the corpus folder only, never the real keycuts folder
        private static Keycut Load(string name)
        {
            return new KeycutRepository(Path.GetDirectoryName(Corpus(name + ".bat"))).Find(name);
        }

        #region The corpus shapes

        [WindowsFact]
        public void G_IsTheNoArgumentsUrl_WithTheSearchBranchSkipped()
        {
            var keycut = Load("g");

            Assert.Equal("https://github.com/GolfNowEng", keycut.ResolvedTarget.Text);
            Assert.True(keycut.ResolvedTarget.AssumesNoArguments);
            Assert.Equal(new[] { new ScriptVariable("BASE", "https://github.com/GolfNowEng", true), new ScriptVariable("QUERY", "", true) },
                keycut.ResolvedTarget.Variables);
            Assert.Equal("%BASE%%QUERY%", keycut.Target.Trim());   // the target is still what the file says
        }

        [WindowsFact]
        public void Ghpr_UndoublesItsPercents()
        {
            var keycut = Load("ghpr");

            Assert.Equal("https://github.com/pulls?q=is%3Aopen+org%3AGolfNowEng", keycut.ResolvedTarget.Text);
            Assert.False(keycut.ResolvedTarget.AssumesNoArguments);
        }

        [WindowsFact]
        public void Gg_TakesTheDefault_WhenItsOneLineIfWouldNeedAnArgument()
        {
            Assert.Equal("https://www.google.com/", Load("gg").ResolvedTarget.Text);
        }

        [WindowsFact]
        public void AVariableSetInAnotherIf_StaysUnknown()
        {
            var keycut = Load("unknown-if");

            // Unknown, so the text doesn't change, so there is nothing to show
            Assert.Null(keycut.ResolvedTarget);
        }

        [WindowsFact]
        public void SetP_StaysUnknown_AndOnlyTheKnownPartIsFilledIn()
        {
            var resolved = Load("set-p").ResolvedTarget;

            Assert.Equal("https://example.com/?q=%TERM%", resolved.Text);
            Assert.Contains(new ScriptVariable("TERM", null, false), resolved.Variables);
        }

        #endregion

        #region SET

        [Theory]
        [InlineData("SET BASE=https://a.com")]
        [InlineData("set base=https://a.com")]
        [InlineData("SET \"BASE=https://a.com\"")]
        [InlineData("@SET \"BASE=https://a.com\" ")]
        public void TheSetForms_AllAssign(string set)
        {
            Assert.Equal("https://a.com", Resolve("%BASE%", set, "START \"\" %BASE%").Text);
        }

        [Fact]
        public void AnEmptySet_IsEmpty()
        {
            Assert.Equal("https://a.com", Resolve("https://a.com%Q%", "SET Q=", "START \"\" https://a.com%Q%").Text);
        }

        [Fact]
        public void AVariableSetTwice_IsTheLastValue()
        {
            Assert.Equal("https://b.com", Resolve("%U%", "SET U=https://a.com", "SET U=https://b.com", "START \"\" %U%").Text);
        }

        [Fact]
        public void AVariableCanReferToAnother_AndToTheEnvironment()
        {
            var resolved = Resolve("%DOCS%", "SET HOME=%USERPROFILE%", "SET DOCS=%HOME%\\Documents", "START \"\" \"%DOCS%\"");

            Assert.Equal(@"C:\Users\me\Documents", resolved.Text);
        }

        [Fact]
        public void CaretsAreUnescaped_OutsideQuotes_ButNotInside()
        {
            Assert.Equal("https://a.com/?x=1&y=2", Resolve("%U%", "SET U=https://a.com/?x=1^&y=2", "START \"\" %U%").Text);

            // In quotes the caret stays in the variable; cmd expands %U% before it parses the launch line, so START gets the & there
            var quoted = Resolve("%U%", "SET \"U=https://a.com/?x=1^&y=2\"", "START \"\" %U%");
            Assert.Equal("https://a.com/?x=1^&y=2", Assert.Single(quoted.Variables).Value);
            Assert.Equal("https://a.com/?x=1&y=2", quoted.Text);
        }

        [Fact]
        public void SetA_IsUnknown()
        {
            Assert.Null(Resolve("%N%", "SET /A N=1+1", "START \"\" %N%"));
        }

        [Fact]
        public void DelayedExpansion_MakesAValueWithBangsUnknown()
        {
            Assert.Null(Resolve("%U%", "SETLOCAL ENABLEDELAYEDEXPANSION", "SET B=https://a.com", "SET U=!B!", "START \"\" %U%"));
        }

        [Theory]
        [InlineData(":top")]
        [InlineData("GOTO top")]
        [InlineData("CALL :sub")]
        [InlineData("FOR %%f IN (a) DO SET U=%%f")]
        public void AfterALabelGotoCallOrFor_EveryScriptVariableIsUnknown(string line)
        {
            Assert.Null(Resolve("%U%", "SET U=https://a.com", line, "START \"\" %U%"));
        }

        [Fact]
        public void ATargetThatNamesOnlyEnvironmentVariables_IsShownAsWritten()
        {
            Assert.Null(Resolve(@"%USERPROFILE%\Desktop", "SET X=1", "START \"\" /B \"%SystemRoot%\\explorer.exe\" \"%USERPROFILE%\\Desktop\""));
        }

        [Fact]
        public void AnUnsetEnvironmentVariable_StaysAsWritten()
        {
            Assert.Equal("https://a.com/%NOPE%", Resolve("%U%", "SET U=https://a.com/%NOPE%", "START \"\" %U%").Text);
        }

        [Fact]
        public void OnlyLinesBeforeTheLaunchLine_Count()
        {
            Assert.Equal("https://a.com", Resolve("%U%", "SET U=https://a.com", "START \"\" %U%", "SET U=https://b.com").Text);
        }

        #endregion

        #region IF and arguments

        [Fact]
        public void IfNotEmptyArgument_IsSkipped_AndItsElseRuns()
        {
            var resolved = Resolve("%U%",
                "IF NOT \"%~1\"==\"\" (",
                "  SET U=https://search.com/?q=%*",
                ") ELSE (",
                "  SET U=https://home.com",
                ")",
                "START \"\" %U%");

            Assert.Equal("https://home.com", resolved.Text);
            Assert.True(resolved.AssumesNoArguments);
        }

        [Fact]
        public void IfEmptyArgument_IsTaken()
        {
            Assert.Equal("https://home.com", Resolve("%U%", "SET U=https://x.com", "IF \"%1\"==\"\" SET U=https://home.com", "START \"\" %U%").Text);
        }

        [Fact]
        public void AnArgument_IsEmpty()
        {
            var resolved = Resolve("%U%", "SET U=https://a.com/%~1", "START \"\" %U%");

            Assert.Equal("https://a.com/", resolved.Text);
            Assert.True(resolved.AssumesNoArguments);
        }

        [Fact]
        public void ABlockSeesTheValuesFromBeforeIt_AsCmdExpandsItWhole()
        {
            var resolved = Resolve("%B%",
                "SET A=old",
                "IF \"%~1\"==\"\" (",
                "  SET A=new",
                "  SET B=https://a.com/%A%",
                ")",
                "START \"\" %B%");

            Assert.Equal("https://a.com/old", resolved.Text);
        }

        // Review of #253: an IF's block on its own line isn't evaluated, so what it sets is unknown, not the value from before
        [Fact]
        public void AOneLineBlock_MakesWhatItSetsUnknown()
        {
            Assert.Null(Resolve("%A%", "SET A=old", "IF \"%~1\"==\"\" (SET A=https://x.com) ELSE (SET A=%~1)", "START \"\" %A%"));
        }

        [Theory]
        [InlineData("IF \"%~1\"==\"\" GOTO :EOF")]
        [InlineData("IF \"%~1\"==\"\" EXIT /B")]
        [InlineData("IF EXIST C:\\flag GOTO done")]
        public void AnIfThatMayJumpAway_BeforeTheLaunchLine_StopsEvaluation(string line)
        {
            Assert.Null(Resolve("%U%", "SET U=https://a.com", line, "START \"\" %U%"));
        }

        [Fact]
        public void AnIfThatJumpsOnlyWithAnArgument_DoesntStop()
        {
            Assert.Equal("https://a.com", Resolve("%U%", "SET U=https://a.com", "IF NOT \"%~1\"==\"\" GOTO search", "START \"\" %U%").Text);
        }

        [Fact]
        public void ANestedBlock_SeesTheValuesFromBeforeTheOutermostBlock()
        {
            var resolved = Resolve("%B%",
                "SET A=old",
                "IF \"%~1\"==\"\" (",
                "  SET A=new",
                "  IF \"%~2\"==\"\" (",
                "    SET B=https://a.com/%A%",
                "  )",
                ")",
                "START \"\" %B%");

            Assert.Equal("https://a.com/old", resolved.Text);
        }

        [Fact]
        public void AQuotedTargetOnTheLaunchLine_KeepsItsCarets()
        {
            Assert.Equal("https://h/?q=a^b", Resolve("%U%", "SET \"U=https://h/?q=a^b\"", "START \"\" \"%U%\"").Text);
            Assert.Equal("https://h/?q=ab", Resolve("%U%", "SET \"U=https://h/?q=a^b\"", "START \"\" %U%").Text);
        }

        [Fact]
        public void ASetInAnUndecidedBlock_IsUnknown()
        {
            var resolved = Resolve("%U%%Q%",
                "SET U=https://a.com",
                "SET Q=",
                "IF EXIST C:\\flag (",
                "  SET Q=?flag",
                ")",
                "START \"\" %U%%Q%");

            Assert.Equal("https://a.com%Q%", resolved.Text);
            Assert.Contains(new ScriptVariable("Q", null, false), resolved.Variables);
        }

        #endregion

        [WindowsFact]
        public void TheResolvedTarget_NeverReachesTheTarget_OrTheEditCheck()
        {
            var keycut = Load("g");
            var parsed = new ShortcutFile(Corpus("g.bat"));

            Assert.Equal(parsed.Target, keycut.Target);
            Assert.Equal(KeycutRepository.CanEditInForm(Corpus("g.bat")), keycut.CanEditInForm);
        }
    }
}
