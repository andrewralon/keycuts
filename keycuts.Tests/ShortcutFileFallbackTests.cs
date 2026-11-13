using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The regex fallback, for files without valid REM type and target lines (the 0.1.x format).</summary>
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
            Assert.Equal(@"C:\Program Files\npp.exe", file.Target);
        }

        [Fact]
        public void Folder_WithoutRemLines_HasNoQuotes()
        {
            var file = new ShortcutFile(@"C:\Shortcuts\docs.bat", new[] { "@ECHO OFF", "\"%SystemRoot%\\explorer.exe\" \"%USERPROFILE%\\Documents\"", "EXIT" });

            Assert.Equal(ShortcutType.Folder, file.Type);
            Assert.Equal(@"%USERPROFILE%\Documents", file.Target);
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
            Assert.Equal(expected, file.Target);
        }

        // #203: the program is kept apart from its arguments, so a check can look for the program alone
        [Theory]
        [InlineData("START \"\" /D \"C:\\Program Files (x86)\\Notepad++\" \"notepad++.exe\"", @"C:\Program Files (x86)\Notepad++\notepad++.exe", "")]
        [InlineData("START \"\" /D \"\" \"chrome\" \"? %*\"", "chrome", "\"? %*\"")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" \"arg\"", @"C:\Tools\app.exe", "\"arg\"")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" \"a b\" \"c\"", @"C:\Tools\app.exe", "\"a b\" \"c\"")]
        public void File_StartInFolder_SplitsTheProgramFromItsArguments(string line, string program, string arguments)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal(ShortcutType.File, file.Type);
            Assert.Equal(program, file.Program);
            Assert.Equal(arguments, file.Arguments);
        }

        [Theory]
        [InlineData("START \"\" /B \"C:\\Program Files\\npp.exe\"", @"C:\Program Files\npp.exe")]
        [InlineData("START \"\" /D \"\" \"notepad.exe\"", "notepad.exe")]
        public void File_WithoutArguments_HasItsTargetAsItsProgram(string line, string program)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal(program, file.Program);
            Assert.Equal(program, file.Target);
            Assert.Equal("", file.Arguments);
        }

        // A target with quotes in it that isn't a form the parser takes apart: it can't say where the program ends
        [Fact]
        public void File_WithAQuoteInItsTarget_HasNoProgram()
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { "START \"\" /B \"C:\\a b\\app.exe\" \"C:\\c d\\file.txt\"" });

            Assert.Equal(ShortcutType.File, file.Type);
            Assert.Equal("", file.Program);
            Assert.Equal("", file.Arguments);
        }

        [Fact]
        public void NotAFile_HasNoProgram()
        {
            var folder = new ShortcutFile(@"C:\Shortcuts\docs.bat", new[] { "\"%SystemRoot%\\explorer.exe\" \"%USERPROFILE%\\Documents\"" });

            Assert.Equal("", folder.Program);
            Assert.Equal("", folder.Arguments);
        }

        [Fact]
        public void StartInFolder_WithQuotedArguments_ReEmitsALineThatRunsTheProgram()
        {
            var original = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { "START \"\" /D \"C:\\Tools\" \"app.exe\" \"arg\"" });
            var emitted = Runner.BuildShortcutLines(Shortcut.FromParts(original.Type, original.Target, @"C:\Shortcuts\app.bat"));

            Assert.Contains("START \"\" /B \"C:\\Tools\\app.exe\" \"arg\"", emitted);
        }

        // #203: a program that isn't quoted, or an argument that isn't, can't be joined into the target and re-emitted
        // identically. The target is the program alone and the rest is its arguments. These lines used to go to the File
        // pattern, whose greedy quotes gave a target with the folder in it and no backslash before the program.
        [Theory]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" --flag \"arg\"", @"C:\Tools\app.exe", "--flag \"arg\"")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" \"arg\" --flag", @"C:\Tools\app.exe", "\"arg\" --flag")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" --flag", @"C:\Tools\app.exe", "--flag")]
        [InlineData("START \"\" /D \"C:\\Tools\" app.exe", @"C:\Tools\app.exe", "")]
        [InlineData("START \"\" /D \"%programfiles(x86)%\\Google\\Chrome\\Application\" chrome.exe --profile-directory=\"Profile 2\"", @"%programfiles(x86)%\Google\Chrome\Application\chrome.exe", "--profile-directory=\"Profile 2\"")]
        [InlineData("START \"\" /D \"\" notepad.exe C:\\Windows\\win.ini", "notepad.exe", @"C:\Windows\win.ini")]
        [InlineData("START \"\" /D \"C:\\Tools\"   app.exe    --flag  ", @"C:\Tools\app.exe", "--flag")]
        public void StartInFolder_WithAnUnquotedPart_IsTheProgramAloneAndItsArguments(string line, string program, string arguments)
        {
            Assert.True(ShortcutFile.IsStartInFolder(line, out var target));
            Assert.Equal(program, target);

            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal(ShortcutType.File, file.Type);
            Assert.Equal(program, file.Target);
            Assert.Equal(program, file.Program);
            Assert.Equal(arguments, file.Arguments);
        }

        // #203: the folder and the program as written are kept apart too, since Windows looks in the folder and then
        // wherever it finds a program by name, which a joined path can't say
        [Theory]
        [InlineData("START \"\" /D \"%programfiles(x86)%\\Google\\Chrome\\Application\" chrome.exe --profile-directory=\"Default\"", @"%programfiles(x86)%\Google\Chrome\Application", "chrome.exe")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\" \"arg\"", @"C:\Tools", "app.exe")]
        [InlineData("START \"\" /D \"\" \"chrome\" \"? %*\"", "", "chrome")]
        [InlineData("START \"\" /D \"C:\\Work\" D:\\Apps\\tool.exe --flag", @"C:\Work", @"D:\Apps\tool.exe")]
        public void StartInFolder_KeepsTheFolderAndTheProgramNameApart(string line, string startIn, string programName)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal(startIn, file.StartInFolder);
            Assert.Equal(programName, file.ProgramName);
        }

        [Theory]
        [InlineData("START \"\" /B \"C:\\Program Files\\npp.exe\"")]
        [InlineData("START \"\" /B \"C:\\a b\\app.exe\" \"C:\\c d\\file.txt\"")]
        public void OtherLaunchLines_HaveNoStartInFolderOrProgramName(string line)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal("", file.StartInFolder);
            Assert.Equal("", file.ProgramName);
        }

        // A program that says where it is isn't put in the start-in folder
        [Theory]
        [InlineData("START \"\" /D \"C:\\Work\" D:\\Apps\\tool.exe --flag", @"D:\Apps\tool.exe")]
        [InlineData("START \"\" /D \"C:\\Work\" \"D:\\Apps\\tool.exe\" --flag", @"D:\Apps\tool.exe")]
        [InlineData("START \"\" /D \"C:\\Work\" \"D:\\Apps\\tool.exe\"", @"D:\Apps\tool.exe")]
        [InlineData("START \"\" /D \"C:\\Work\" \\\\server\\share\\tool.exe -x", @"\\server\share\tool.exe")]
        [InlineData("START \"\" /D \"C:\\Work\" %ProgramFiles%\\Tools\\tool.exe -x", @"%ProgramFiles%\Tools\tool.exe")]
        [InlineData("START \"\" /D \"C:\\Work\" tool.exe -x", @"C:\Work\tool.exe")]
        public void StartInFolder_AProgramThatIsAlreadyAFullPath_IsNotPrefixedWithTheFolder(string line, string program)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", new[] { line });

            Assert.Equal(program, file.Target);
            Assert.Equal(program, file.Program);
        }

        // Not read as a program: the other patterns (and the Unknown mark) deal with these as before
        [Theory]
        [InlineData("START \"\" /D \"C:\\Tools\" /MIN app.exe")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe\"x --flag")]
        [InlineData("START \"\" /D \"C:\\Tools\" app\"x.exe")]
        [InlineData("START \"\" /D \"C:\\Tools\" \"app.exe --flag")]
        [InlineData("START \"\" /D \"C:\\Tools\"    ")]
        public void StartInFolder_WithNoReadableProgram_IsLeftToTheOtherPatterns(string line)
        {
            Assert.False(ShortcutFile.IsStartInFolder(line, out _));
        }

        [Fact]
        public void HostsFile_WithoutRemLines_OpenWithAppHasNoQuotes()
        {
            var file = new ShortcutFile(@"C:\Shortcuts\hosts.bat", new[] { "START \"\" /B \"%windir%\\system32\\notepad.exe\" \"C:\\Windows\\System32\\drivers\\etc\\hosts\"" });

            Assert.Equal(ShortcutType.HostsFile, file.Type);
            Assert.Equal(@"%windir%\system32\notepad.exe", file.OpenWithApp);
            Assert.Equal(@"C:\Windows\System32\drivers\etc\hosts", file.Target);
        }

        [Theory]
        [InlineData("")]
        [InlineData("@ECHO OFF|\"C:\\tools\\app.exe\" --start|EXIT")]
        public void NoLaunchLine_IsUnknown(string joinedLines)
        {
            var file = new ShortcutFile(@"C:\Shortcuts\none.bat", joinedLines.Split('|'));

            Assert.Equal(ShortcutType.Unknown, file.Type);
            Assert.Null(file.Target);
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
            Assert.Equal(command, file.Target);
        }
    }
}
