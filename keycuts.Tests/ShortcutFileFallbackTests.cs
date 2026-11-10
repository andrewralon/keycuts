using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The regex fallback, for files without valid REM type and destination lines (the 0.1.x format).</summary>
    public class ShortcutFileFallbackTests
    {
        [Theory]
        [InlineData("\"C:\\a b\\c.txt\"", "C:\\a b\\c.txt")]
        [InlineData("\"\"", "")]
        [InlineData("\"a\" \"b\"", "a\" \"b")]
        [InlineData("C:\\a.txt", "C:\\a.txt")]
        [InlineData("\"C:\\a.txt", "\"C:\\a.txt")]
        [InlineData("C:\\a.txt\"", "C:\\a.txt\"")]
        [InlineData("\"", "\"")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void Unquote_RemovesOnePairOfSurroundingQuotes(string value, string expected)
        {
            Assert.Equal(expected, ShortcutFile.Unquote(value));
        }

        [Fact]
        public void File_WithoutRemLines_HasNoQuotes()
        {
            var file = new ShortcutFile(@"C:\Shortcuts\npp.bat", new[] { "@ECHO OFF", "START \"\" /B \"C:\\Program Files\\npp.exe\"", "EXIT" });

            Assert.Equal(ShortcutType.File, file.Type);
            Assert.Equal(@"C:\Program Files\npp.exe", file.Destination);
        }

        [Fact]
        public void Folder_WithoutRemLines_HasNoQuotes()
        {
            var file = new ShortcutFile(@"C:\Shortcuts\docs.bat", new[] { "@ECHO OFF", "\"%SystemRoot%\\explorer.exe\" \"%USERPROFILE%\\Documents\"", "EXIT" });

            Assert.Equal(ShortcutType.Folder, file.Type);
            Assert.Equal(@"%USERPROFILE%\Documents", file.Destination);
        }

        [Theory]
        [InlineData("START \"\" /D \"C:\\Program Files (x86)\\Notepad++\" \"notepad++.exe\"", @"C:\Program Files (x86)\Notepad++\notepad++.exe")]
        [InlineData("START \"\" /D \"\" \"chrome\" \"? %*\"", "chrome\" \"? %*")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" \"arg\"", "C:\\Tools\\app.exe\" \"arg")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" \"a b\" \"c\"", "C:\\Tools\\app.exe\" \"a b\" \"c")]
        [InlineData("START \"\" /B \"C:\\a b\\app.exe\" \"C:\\c d\\file.txt\"", "C:\\a b\\app.exe\" \"C:\\c d\\file.txt")]
        public void File_QuotedParts_AreJoinedOnlyForTheStartInFolderForm(string line, string expected)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal(ShortcutType.File, file.Type);
            Assert.Equal(expected, file.Destination);
        }

        [Fact]
        public void StartInFolder_WithQuotedArguments_ReEmitsALineThatRunsTheProgram()
        {
            var original = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { "START \"\" /D \"C:\\Tools\" \"app.exe\" \"arg\"" });
            var emitted = Runner.BuildShortcutLines(Shortcut.FromParts(original.Type, original.Destination, @"C:\Shortcuts\app.bat"));

            Assert.Contains("START \"\" /B \"C:\\Tools\\app.exe\" \"arg\"", emitted);
        }

        [Fact]
        public void HostsFile_WithoutRemLines_OpenWithAppHasNoQuotes()
        {
            var file = new ShortcutFile(@"C:\Shortcuts\hosts.bat", new[] { "START \"\" /B \"%windir%\\system32\\notepad.exe\" \"C:\\Windows\\System32\\drivers\\etc\\hosts\"" });

            Assert.Equal(ShortcutType.HostsFile, file.Type);
            Assert.Equal(@"%windir%\system32\notepad.exe", file.OpenWithApp);
            Assert.Equal(@"C:\Windows\System32\drivers\etc\hosts", file.Destination);
        }

        [Theory]
        [InlineData("")]
        [InlineData("@ECHO OFF|\"C:\\tools\\app.exe\" --start|EXIT")]
        public void NoLaunchLine_IsUnknown(string joinedLines)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\none.bat", joinedLines.Split('|'));

            Assert.Equal(ShortcutType.Unknown, file.Type);
            Assert.Null(file.Destination);
            Assert.Null(file.Command);
        }

        [Theory]
        [InlineData("ipconfig /all")]
        [InlineData("\"C:\\a b\\tool.exe\" \"arg one\" /x")]
        [InlineData("\"C:\\Windows\\explorer.exe\" \"C:\\My Folder\"")]
        public void Command_EmittedWithoutRemLines_ReadsBackTheSameCommand(string command)
        {
            var emitted = Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Command, command, @"C:\Shortcuts\cmd.bat"));
            var launchOnly = emitted.FindAll(x => !ShortcutFile.IsRemLine(x)).ToArray();

            var file = new ShortcutFile(@"C:\Shortcuts\cmd.bat", launchOnly);

            Assert.Equal(ShortcutType.Command, file.Type);
            Assert.Equal(command, file.Destination);
        }
    }
}
