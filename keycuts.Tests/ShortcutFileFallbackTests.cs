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
