using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class ShortcutTests
    {
        [Theory]
        [InlineData("a%b", "a%%b")]
        [InlineData("a%%b", "a%%b")]
        [InlineData("a&b", "a^&b")]
        [InlineData("plain", "plain")]
        public void SanitizeBatEscapeCharacters_EscapesSpecialCharacters(string input, string expected)
        {
            Assert.Equal(expected, Shortcut.SanitizeBatEscapeCharacters(input));
        }
    }
}
