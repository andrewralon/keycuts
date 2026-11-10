using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#97: <c>keycuts paste</c>, formerly keypaste. Every test passes its own folders and files.</summary>
    public class KeycutPasteTests
    {
        static readonly string[] Folders = { @"C:\work", @"C:\Shortcuts", @"C:\Program Files\keycuts" };

        class FakeFiles
        {
            public Dictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public List<string> Copied { get; } = new List<string>();

            public Exception ClipboardError { get; set; }

            public StringWriter Output { get; } = new StringWriter();

            public StringWriter Error { get; } = new StringWriter();

            public ExitCode Run(string name)
            {
                return KeycutPaste.Run(
                    name,
                    Folders,
                    path => Files.ContainsKey(path),
                    path => Files[path].Replace("\r\n", "\n").Split('\n'),
                    path => Files[path],
                    text =>
                    {
                        if (ClipboardError != null)
                        {
                            throw ClipboardError;
                        }

                        Copied.Add(text);
                    },
                    Output,
                    Error);
            }
        }

        static string Keycut(string destination)
        {
            var lines = Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Folder, destination, @"C:\Shortcuts\db.bat"));
            return string.Join("\r\n", lines);
        }

        [Fact]
        public void GetDefaultFolders_TheDebugOverrideReplacesTheConfiguredFolder()
        {
            var asked = 0;
            string Configured() { asked++; return @"C:\Users\me\Shortcuts"; }

            Assert.Equal(@"C:\Fixtures", KeycutPaste.GetDefaultFolders(@"C:\Fixtures", Configured)[1]);
            Assert.Equal(0, asked);

            Assert.Equal(@"C:\Users\me\Shortcuts", KeycutPaste.GetDefaultFolders(null, Configured)[1]);
            Assert.Equal(1, asked);
        }

        [Fact]
        public void Candidates_TryEachFolderInOrder_TheNameThenTxtThenBat()
        {
            Assert.Equal(new[]
            {
                @"C:\work\db", @"C:\work\db.txt", @"C:\work\db.bat",
                @"C:\Shortcuts\db", @"C:\Shortcuts\db.txt", @"C:\Shortcuts\db.bat",
                @"C:\Program Files\keycuts\db", @"C:\Program Files\keycuts\db.txt", @"C:\Program Files\keycuts\db.bat",
            }, KeycutPaste.Candidates("db", Folders).ToArray());
        }

        [Theory]
        [InlineData("notes.txt")]
        [InlineData("db.BAT")]
        public void Candidates_ANameWithTxtOrBat_IsntGivenAnotherExtension(string name)
        {
            Assert.Equal(new[] { @"C:\work\" + name, @"C:\Shortcuts\" + name }, KeycutPaste.Candidates(name, Folders.Take(2)).ToArray());
        }

        [Fact]
        public void Candidates_ADottedKeycutName_StillTriesBat()
        {
            // keypaste.exe skipped .txt and .bat for any name with a dot, so a keycut named "my.site" was never found
            Assert.Contains(@"C:\Shortcuts\my.site.bat", KeycutPaste.Candidates("my.site", Folders));
        }

        [Fact]
        public void Candidates_AFullPath_IsTriedOnce()
        {
            Assert.Equal(new[] { @"D:\notes\todo.txt" }, KeycutPaste.Candidates(@"D:\notes\todo.txt", Folders).ToArray());
        }

        [Fact]
        public void Run_AKeycut_CopiesItsDestination_AndPrintsIt()
        {
            var fake = new FakeFiles();
            fake.Files[@"C:\Shortcuts\db.bat"] = Keycut(@"C:\Users\me\Dropbox");

            Assert.Equal(ExitCode.Success, fake.Run("db"));
            Assert.Equal(new[] { @"C:\Users\me\Dropbox" }, fake.Copied);
            Assert.Equal(@"C:\Users\me\Dropbox" + Environment.NewLine, fake.Output.ToString());
            Assert.Empty(fake.Error.ToString());
        }

        [Fact]
        public void Run_ATextFile_CopiesItsContents_AndTheWorkingFolderWins()
        {
            var fake = new FakeFiles();
            fake.Files[@"C:\work\sig.txt"] = "Best,\r\nMe";
            fake.Files[@"C:\Shortcuts\sig.bat"] = Keycut(@"C:\elsewhere");

            Assert.Equal(ExitCode.Success, fake.Run("sig"));
            Assert.Equal(new[] { "Best,\r\nMe" }, fake.Copied);
        }

        [Fact]
        public void Run_NotFound_SaysWhereItLooked_AndCopiesNothing()
        {
            var fake = new FakeFiles();

            Assert.Equal(ExitCode.CannotFindFile, fake.Run("nope"));
            Assert.Empty(fake.Copied);
            Assert.Contains("Can't find \"nope\"", fake.Error.ToString());
            Assert.Contains(@"C:\Shortcuts", fake.Error.ToString());
        }

        [Fact]
        public void Run_ABatThatIsntAKeycut_CopiesNothing()
        {
            var fake = new FakeFiles();
            fake.Files[@"C:\Shortcuts\build.bat"] = "@echo off\r\nmsbuild";

            Assert.Equal(ExitCode.CannotOpenFile, fake.Run("build"));
            Assert.Empty(fake.Copied);
            Assert.Contains("isn't a keycut", fake.Error.ToString());
        }

        [Fact]
        public void Run_AClipboardFailure_IsReported()
        {
            var fake = new FakeFiles { ClipboardError = new System.ComponentModel.Win32Exception(5, "Access is denied.") };
            fake.Files[@"C:\Shortcuts\db.bat"] = Keycut(@"C:\Users\me\Dropbox");

            Assert.Equal(ExitCode.CannotSetClipboard, fake.Run("db"));
            Assert.Contains("Access is denied.", fake.Error.ToString());
            Assert.Empty(fake.Output.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Run_NoName_IsBadArguments(string name)
        {
            Assert.Equal(ExitCode.BadArguments, new FakeFiles().Run(name));
        }
    }
}
