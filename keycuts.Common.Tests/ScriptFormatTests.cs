using System;
using System.IO;
using System.Linq;
using keycuts.Common;
using keycuts.Tests.Characterization;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The script format seam (Phase 2.5): <see cref="BatFormat"/> is the emitter and parser as they were, unchanged.</summary>
    public class ScriptFormatTests
    {
        [Fact]
        public void BatFormat_IsABatWithRemComments_ThatNeedsNoExecutableBit()
        {
            var format = new BatFormat();

            Assert.Equal(".bat", format.Extension);
            Assert.Equal("REM", format.CommentPrefix);
            Assert.False(format.NeedsExecutableBit);
        }

        [Fact]
        public void ShFormat_IsAnExtensionlessShScriptWithHashComments_ThatNeedsTheExecutableBit()
        {
            var format = new ShFormat();

            Assert.Equal("", format.Extension);
            Assert.Equal("#", format.CommentPrefix);
            Assert.True(format.NeedsExecutableBit);
        }

        [Theory]
        [InlineData("plain")]
        [InlineData("it's")]
        [InlineData("''")]
        [InlineData("a \"b\" $HOME `c` \\ d")]
        [InlineData("café ☕ \t tab")]
        [InlineData("")]
        public void ShFormat_Quote_IsOneWordThatSplitsBackToTheValue(string value)
        {
            Assert.Equal(new[] { value }, ShFormat.SplitWords(ShFormat.Quote(value)));
        }

        [Theory]
        [InlineData("$HOME", "\"$HOME\"")]
        [InlineData("$HOME/Downloads", "\"$HOME/Downloads\"")]
        [InlineData("$HOME/a \"b\" $c `d` \\e 'f'", "\"$HOME/a \\\"b\\\" \\$c \\`d\\` \\\\e 'f'\"")]
        [InlineData("$HOMEWORK", "'$HOMEWORK'")]
        [InlineData("/Users/me/$HOME/x", "'/Users/me/$HOME/x'")]
        public void ShFormat_QuoteTarget_DoubleQuotesOnlyAHomePath_AndSplitsBackToIt(string target, string quoted)
        {
            Assert.Equal(quoted, ShFormat.QuoteTarget(target));
            Assert.Equal(new[] { target }, ShFormat.SplitWords(quoted));
        }

        [Fact]
        public void ShFormat_SplitWords_ReadsDoubleQuotesWithoutExpanding_AndRefusesAnUnclosedQuote()
        {
            Assert.Equal(new[] { "open", "$HOME/a \"b\" $c" }, ShFormat.SplitWords("open \"$HOME/a \\\"b\\\" \\$c\""));
            Assert.Null(ShFormat.SplitWords("open 'x"));
            Assert.Null(ShFormat.SplitWords("open \"x"));
        }

        [MacFact]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        public void MarkExecutable_GivesAShKeycutMode755()
        {
            var path = Path.GetTempFileName();

            try
            {
                ScriptFormats.MarkExecutable(new ShFormat(), path);

                Assert.Equal("755", Convert.ToString((int)File.GetUnixFileMode(path), 8));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TheCurrentFormat_IsBat()
        {
            Assert.IsType<BatFormat>(ScriptFormats.Current);
        }

        [Theory]
        [InlineData(ShortcutType.Url, "https://mail.google.com/", "")]
        [InlineData(ShortcutType.File, @"C:\Tools\tool.exe", "Opens the tool")]
        [InlineData(ShortcutType.Folder, @"C:\Users\me\Documents", "100% done")]
        [InlineData(ShortcutType.CLSIDKey, "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "")]
        [InlineData(ShortcutType.Command, "ipconfig /all", "")]
        public void BatFormat_EmitsWhatTheEmitterDoes(ShortcutType type, string target, string description)
        {
            var shortcut = Shortcut.FromParts(type, target, @"C:\Shortcuts\x.bat");

            Assert.Equal(Runner.BuildShortcutLines(shortcut, description), new BatFormat().EmitLines(shortcut, description));
        }

        [Fact]
        public void BatFormat_ReadsEveryCorpusFileAsTheParserDoes()
        {
            var repoRoot = Path.GetDirectoryName(RepoFiles.Find("keycuts.sln"));
            var corpus = CharacterizationRenderer.ReadCorpus(repoRoot).ToList();

            Assert.NotEmpty(corpus);

            foreach (var (path, lines) in corpus)
            {
                var expected = new ShortcutFile(path, lines);
                var actual = new BatFormat().Parse(path, lines);

                Assert.Equal(
                    (expected.Type, expected.Target, expected.OpenWithApp, expected.Command, expected.Description, expected.Program, expected.StartInFolder, expected.ProgramName, expected.Arguments),
                    (actual.Type, actual.Target, actual.OpenWithApp, actual.Command, actual.Description, actual.Program, actual.StartInFolder, actual.ProgramName, actual.Arguments));
            }
        }
    }
}
