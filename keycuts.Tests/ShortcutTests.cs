using System;
using System.IO;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class ShortcutTests
    {
        [Theory]
        [InlineData("a%b", "a%%b")]
        [InlineData("a%%b", "a%%%%b")]
        [InlineData("a&b", "a^&b")]
        [InlineData("a&&b", "a^&^&b")]
        [InlineData("&a", "^&a")]
        [InlineData("a^&b", "a^^^&b")]
        [InlineData("a|b<c>d", "a^|b^<c^>d")]
        [InlineData("q=\"a&b\"&c", "q=\"a&b\"^&c")]
        [InlineData("q=\"50%\"", "q=\"50%%\"")]
        [InlineData("a\"b&c", "a\"b&c")]
        [InlineData("plain", "plain")]
        public void SanitizeBatEscapeCharacters_EscapesSpecialCharacters(string input, string expected)
        {
            Assert.Equal(expected, Shortcut.SanitizeBatEscapeCharacters(input));
        }

        [Fact]
        public void GetShortcutTypeFromDestination_MissingPath_IsUnknownInsteadOfThrowing()
        {
            var missing = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"), "missing.txt");

            var type = Shortcut.GetShortcutTypeFromDestination(missing, out var newDestination);

            Assert.Equal(ShortcutType.Unknown, type);
            Assert.Equal(missing, newDestination);
        }

        [Fact]
        public void GetShortcutTypeFromDestination_ExistingFileAndFolder_AreStillClassified()
        {
            var folder = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "notes.txt");
            File.WriteAllText(file, "");

            try
            {
                Assert.Equal(ShortcutType.Folder, Shortcut.GetShortcutTypeFromDestination(folder, out _));
                Assert.Equal(ShortcutType.File, Shortcut.GetShortcutTypeFromDestination(file, out _));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
