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
            Assert.Equal(Path.Combine(Folder, GuiLauncher.FileName), started);
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("paste")]
        [InlineData("-t")]
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

        [WindowsFact]
        public void OnWindows_TheGuiIsShellExecuted()
        {
            var start = GuiLauncher.StartInfo(@"C:\fake\keycuts\keycuts.GUI.exe");

            Assert.Equal((@"C:\fake\keycuts\keycuts.GUI.exe", true), (start.FileName, start.UseShellExecute));
        }

        [MacFact]
        public void OnTheMac_TheGuiBesideTheCli_IsOpenedByItsBundleId()
        {
            var start = GuiLauncher.StartInfo("/Applications/keycuts.app/Contents/MacOS/keycuts.GUI");

            Assert.Equal("keycuts.GUI", GuiLauncher.FileName);
            Assert.Equal("/usr/bin/open", start.FileName);
            Assert.Equal(new[] { "-b", "com.teamralon.keycuts" }, start.ArgumentList);
        }
    }
}
