using keycuts.Common;
using System.IO;
using Xunit;

namespace keycuts.Tests
{
    public class GuiLauncherTests
    {
        private const string Folder = @"C:\fake\keycuts";

        [Fact]
        public void NoArguments_StartsTheGuiBesideTheCli()
        {
            string started = null;

            var result = GuiLauncher.TryLaunch(new string[0], Folder, _ => true, path => started = path);

            Assert.Equal(ExitCode.Success, result);
            Assert.Equal(Path.Combine(Folder, "keycuts.GUI.exe"), started);
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("paste")]
        [InlineData("-d")]
        public void AnyArgument_StaysTheCommandLine(string arg)
        {
            var started = false;

            var result = GuiLauncher.TryLaunch(new[] { arg }, Folder, _ => true, _ => started = true);

            Assert.Null(result);
            Assert.False(started);
        }

        [Fact]
        public void NoGuiBesideTheCli_StartsNothingAndSaysSo()
        {
            var started = false;

            var result = GuiLauncher.TryLaunch(new string[0], Folder, _ => false, _ => started = true);

            Assert.Equal(ExitCode.CannotFindFile, result);
            Assert.False(started);
        }
    }
}
