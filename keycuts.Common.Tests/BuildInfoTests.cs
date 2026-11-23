using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class BuildInfoTests
    {
        [Theory]
        [InlineData("2.1.0+76cc9b5cbef11660fd72c188c0b7268a0551e389", "76cc9b5")]
        [InlineData("2.1.0+76CC9B5", "76cc9b5")]
        [InlineData("2.1.0-beta.1+76cc9b5cbef1.dirty", "76cc9b5")]
        [InlineData("2.1.0", null)]
        [InlineData("2.1.0+", null)]
        [InlineData("2.1.0+abc12", null)]
        [InlineData("2.1.0+not-a-commit", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseCommit_IsTheFirstSevenHexCharactersAfterThePlus(string informationalVersion, string expected)
        {
            Assert.Equal(expected, BuildInfo.ParseCommit(informationalVersion));
        }

        [Fact]
        public void Commit_OfThisBuild_IsNullOrSevenHexCharacters()
        {
            Assert.True(BuildInfo.Commit == null || (BuildInfo.Commit.Length == BuildInfo.CommitLength && BuildInfo.Commit.All(System.Uri.IsHexDigit)));
        }
    }
}
