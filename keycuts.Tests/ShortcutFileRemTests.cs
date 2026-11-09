using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class ShortcutFileRemTests
    {
        private static bool Parse(string[] lines, out ShortcutType type, out string destination)
        {
            return ShortcutFile.TryParseRemTypeAndDestination(lines, out type, out destination);
        }

        [Fact]
        public void BothTags_AreRead()
        {
            var ok = Parse(new[] { "REM <type>file</type>", @"REM <destination>C:\a b\c.txt</destination>" }, out var type, out var destination);

            Assert.True(ok);
            Assert.Equal(ShortcutType.File, type);
            Assert.Equal(@"C:\a b\c.txt", destination);
        }

        [Fact]
        public void Destination_KeepsQuotesAndColonsInside()
        {
            var ok = Parse(new[] { "REM <type>clsidkey</type>", "REM <destination>::{645FF040-5081-101B-9F08-00AA002F954E}</destination>" }, out var type, out var destination);

            Assert.True(ok);
            Assert.Equal(ShortcutType.CLSIDKey, type);
            Assert.Equal("::{645FF040-5081-101B-9F08-00AA002F954E}", destination);

            ok = Parse(new[] { "REM <type>file</type>", "REM <destination>C:\\Say \"Hi\".txt</destination>" }, out _, out destination);

            Assert.True(ok);
            Assert.Equal("C:\\Say \"Hi\".txt", destination);
        }

        [Fact]
        public void SurroundingWhitespace_IsTrimmed()
        {
            var ok = Parse(new[] { "  REM   <type> Folder </type>  ", @"REM <destination>  C:\Shortcuts   </destination>" }, out var type, out var destination);

            Assert.True(ok);
            Assert.Equal(ShortcutType.Folder, type);
            Assert.Equal(@"C:\Shortcuts", destination);
        }

        [Theory]
        [InlineData("file")]
        [InlineData("FILE")]
        [InlineData("CLSIDKey")]
        [InlineData("hostsfile")]
        [InlineData("url")]
        [InlineData("command")]
        public void TypeName_IsCaseInsensitive(string name)
        {
            var ok = Parse(new[] { $"REM <type>{name}</type>", "REM <destination>x</destination>" }, out var type, out _);

            Assert.True(ok);
            Assert.Equal(name.ToLowerInvariant(), type.ToString().ToLowerInvariant());
        }

        [Fact]
        public void MissingType_IsRejected()
        {
            Assert.False(Parse(new[] { "REM <destination>C:\\a</destination>" }, out var type, out var destination));
            Assert.Equal(ShortcutType.Unknown, type);
            Assert.Null(destination);
        }

        [Fact]
        public void MissingDestination_IsRejected()
        {
            Assert.False(Parse(new[] { "REM <type>file</type>" }, out var type, out var destination));
            Assert.Equal(ShortcutType.Unknown, type);
            Assert.Null(destination);
        }

        [Fact]
        public void EmptyDestination_IsRejected()
        {
            Assert.False(Parse(new[] { "REM <type>file</type>", "REM <destination></destination>" }, out _, out _));
            Assert.False(Parse(new[] { "REM <type>file</type>", "REM <destination>   </destination>" }, out _, out _));
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("")]
        [InlineData("nonsense")]
        [InlineData("3")]
        [InlineData("-1")]
        [InlineData("99")]
        [InlineData("file,folder")]
        public void UnknownOrInvalidType_IsRejected(string name)
        {
            Assert.False(Parse(new[] { $"REM <type>{name}</type>", "REM <destination>x</destination>" }, out var type, out var destination));
            Assert.Equal(ShortcutType.Unknown, type);
            Assert.Null(destination);
        }

        [Fact]
        public void LinesThatAreNotRemComments_AreIgnored()
        {
            Assert.False(Parse(new[] { "<type>file</type>", "<destination>x</destination>" }, out _, out _));
            Assert.False(Parse(new[] { "ECHO <type>file</type>", "ECHO <destination>x</destination>" }, out _, out _));
        }

        [Fact]
        public void FirstWellFormedLine_Wins()
        {
            var ok = Parse(new[]
            {
                "REM <type>file</type>", "REM <type>folder</type>",
                "REM <destination>first</destination>", "REM <destination>second</destination>",
            }, out var type, out var destination);

            Assert.True(ok);
            Assert.Equal(ShortcutType.File, type);
            Assert.Equal("first", destination);
        }

        [Fact]
        public void Constructor_PrefersRemOverTheLaunchCommand_AndKeepsTheCommand()
        {
            var lines = new[]
            {
                "@ECHO OFF",
                "REM <type>file</type>",
                @"REM <destination>C:\tools\app.exe</destination>",
                "START \"\" /B \"C:\\tools\\app.exe\" %*",
                "EXIT",
            };

            var file = new ShortcutFile(@"C:\Shortcuts\app.bat", lines);

            Assert.Equal(ShortcutType.File, file.Type);
            Assert.Equal(@"C:\tools\app.exe", file.Destination);
            Assert.Equal("START \"\" /B \"C:\\tools\\app.exe\" %*", file.Command);
        }

        [Fact]
        public void Constructor_FallsBackToTheCommand_WhenThereAreNoRemLines()
        {
            var lines = new[]
            {
                "@ECHO OFF",
                "START https://example.com",
                "EXIT",
            };

            var file = new ShortcutFile(@"C:\Shortcuts\site.bat", lines);

            Assert.Equal(ShortcutType.Url, file.Type);
            Assert.Equal("https://example.com", file.Destination);
        }

        [Fact]
        public void Constructor_FallsBackToTheCommand_WhenTheRemLinesAreInvalid()
        {
            var lines = new[]
            {
                "@ECHO OFF",
                "REM <type>3</type>",
                "REM <destination>C:\\ignored</destination>",
                "START https://example.com",
                "EXIT",
            };

            var file = new ShortcutFile(@"C:\Shortcuts\site.bat", lines);

            Assert.Equal(ShortcutType.Url, file.Type);
            Assert.Equal("https://example.com", file.Destination);
        }
    }
}
