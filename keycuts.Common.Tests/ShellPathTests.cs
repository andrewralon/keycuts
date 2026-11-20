using System;
using System.IO;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The keycuts folder on a Mac's PATH (Phase 4.9, #101), in a temp home: nothing reads or writes the real profile.</summary>
    public class ShellPathTests : IDisposable
    {
        private readonly string _home = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));

        private const string Folder = "/Volumes/Data/Shortcuts";

        public ShellPathTests()
        {
            Directory.CreateDirectory(_home);
        }

        public void Dispose()
        {
            Directory.Delete(_home, true);
        }

        private MacShellPath Shell(string shell = "/bin/zsh", string processPath = "/usr/bin:/bin", params string[] system)
        {
            return new MacShellPath(_home, shell, processPath, system);
        }

        [Theory]
        [InlineData("/bin/zsh", ".zprofile")]
        [InlineData("", ".zprofile")]
        [InlineData("/opt/homebrew/bin/bash", ".bash_profile")]
        public void ZshAndBash_GetAProfile(string shell, string file)
        {
            Assert.Equal(Path.Combine(_home, file), Shell(shell).ProfilePath);
            Assert.Null(Shell(shell).Instructions(Folder));
        }

        [Theory]
        [InlineData("/opt/homebrew/bin/fish", "Run: fish_add_path '/Volumes/Data/Shortcuts'")]
        [InlineData("/opt/homebrew/bin/nu", "Add to your config.nu: $env.path ++= ['/Volumes/Data/Shortcuts']")]
        [InlineData("/bin/tcsh", "Add /Volumes/Data/Shortcuts to PATH in your tcsh startup file.")]
        public void OtherShells_GetInstructions_AndNoProfileIsTouched(string shell, string instructions)
        {
            var path = Shell(shell);

            Assert.Null(path.ProfilePath);
            Assert.Equal(instructions, path.Instructions(Folder));
            Assert.False(path.Add(Folder));
            Assert.True(path.Remove());
            Assert.Empty(Directory.GetFiles(_home));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("export A=1")]
        [InlineData("export A=1\n")]
        [InlineData("export A=1\n\n")]
        [InlineData("# >>> keycuts >>> in a comment\nexport A=1\r\n")]
        public void AddThenRemove_LeavesTheProfileByteIdentical(string before)
        {
            var path = Shell();

            if (before != null)
            {
                File.WriteAllText(path.ProfilePath, before);
            }

            Assert.True(path.Add(Folder));
            Assert.Contains("export PATH=\"$PATH:/Volumes/Data/Shortcuts\"", File.ReadAllText(path.ProfilePath));

            Assert.True(path.Remove());
            Assert.Equal(before ?? "", File.ReadAllText(path.ProfilePath));
        }

        [Fact]
        public void Add_Twice_KeepsOneBlock_ForTheLatestFolder()
        {
            var path = Shell();
            File.WriteAllText(path.ProfilePath, "export A=1\n");

            path.Add(Folder);
            path.Add("/Volumes/Other");

            var text = File.ReadAllText(path.ProfilePath);
            Assert.Equal(text.IndexOf(MacShellPath.BeginMarker), text.LastIndexOf(MacShellPath.BeginMarker));
            Assert.Equal("/Volumes/Other", path.ReadBlockFolder());
            Assert.Equal("export A=1\n" + path.Block("/Volumes/Other"), text);
        }

        [Theory]
        [InlineData("/a:b")]
        [InlineData("/a\nb")]
        public void Add_RefusesAFolderPathCantHold(string folder)
        {
            Assert.False(Shell().Add(folder));
            Assert.False(File.Exists(Shell().ProfilePath));
        }

        [Fact]
        public void Block_EscapesWhatDoubleQuotesWouldExpand_AndReadsBack()
        {
            var path = Shell();
            var odd = "/Volumes/a \"b\" $c `d` \\e";

            Assert.Contains("export PATH=\"$PATH:/Volumes/a \\\"b\\\" \\$c \\`d\\` \\\\e\"", path.Block(odd));

            path.Add(odd);
            Assert.Equal(odd, path.ReadBlockFolder());
        }

        [MacFact]
        public void OnMacOS_AFolderUnderHome_IsWrittenWithDollarHome()
        {
            var path = new MacShellPath("/Users/me", "/bin/zsh", "", null);

            Assert.Contains("export PATH=\"$PATH:$HOME/Shortcuts\"", path.Block("/Users/me/Shortcuts"));
        }

        [Fact]
        public void Status_IsOnPath_InProfile_OrNot()
        {
            Assert.Equal(ShellPathStatus.OnPath, Shell(processPath: "/usr/bin:" + Folder + "/").Status(Folder));
            Assert.Equal(ShellPathStatus.OnPath, Shell("/bin/zsh", "/usr/bin", Folder).Status(Folder));

            var path = Shell();
            Assert.Equal(ShellPathStatus.NotOnPath, path.Status(Folder));

            path.Add(Folder);
            Assert.Equal(ShellPathStatus.InProfile, path.Status(Folder));
            Assert.Equal(ShellPathStatus.NotOnPath, path.Status("/Volumes/Other"));
        }

        private static (ExitCode Code, string Output, string Error) RunPath(string action, MacShellPath shell)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var code = KeycutPathCommand.Run(action, Folder, shell, output, error);

            return (code, output.ToString().Trim(), error.ToString().Trim());
        }

        [Fact]
        public void ThePathCommand_SaysAddsAndRemoves_InTheProfile()
        {
            var shell = Shell();

            Assert.Equal((ExitCode.Success, Folder + " isn't on PATH. 'keycuts path add' adds it to ~/.zprofile, so Terminal runs your keycuts by name.", ""), RunPath(null, shell));
            Assert.Equal((ExitCode.Success, "Added " + Folder + " to PATH in ~/.zprofile. Open a new Terminal window to run your keycuts by name.", ""), RunPath("add", shell));
            Assert.Equal((ExitCode.Success, Folder + " is in ~/.zprofile. Open a new Terminal window to run your keycuts by name.", ""), RunPath("STATUS", shell));
            Assert.Equal((ExitCode.Success, "Took keycuts out of ~/.zprofile. New Terminal windows won't find your keycuts by name.", ""), RunPath("remove", shell));
            Assert.Equal((ExitCode.Success, "keycuts hadn't added anything to ~/.zprofile.", ""), RunPath("remove", shell));
            Assert.Equal("", File.ReadAllText(shell.ProfilePath));
        }

        [Fact]
        public void ThePathCommand_OnPathAlready_SaysSo()
        {
            Assert.Equal((ExitCode.Success, Folder + " is on PATH, so Terminal runs your keycuts by name.", ""), RunPath("status", Shell(processPath: Folder)));
        }

        [Fact]
        public void ThePathCommand_ForFish_GivesInstructions_AndAddFails()
        {
            var fish = Shell("/opt/homebrew/bin/fish");

            Assert.Equal((ExitCode.Success, Folder + " isn't on PATH. Run: fish_add_path '" + Folder + "'", ""), RunPath("status", fish));
            Assert.Equal((ExitCode.CannotUpdatePath, "", "keycuts: keycuts doesn't edit fish's startup files. Run: fish_add_path '" + Folder + "'"), RunPath("add", fish));
        }

        [Fact]
        public void ThePathCommand_RefusesAnotherAction()
        {
            Assert.Equal((ExitCode.BadArguments, "", "keycuts: path takes status, add, remove."), RunPath("on", Shell()));
        }

        [Fact]
        public void AFolderChange_MovesABlockThatsThere_AndAddsNoneWithoutConsent()
        {
            var path = Shell();

            Assert.True(path.AddToOrReplace(Folder, "/Volumes/New"));
            Assert.False(File.Exists(path.ProfilePath));

            path.Add(Folder);
            Assert.True(path.AddToOrReplace(Folder, "/Volumes/New"));
            Assert.Equal("/Volumes/New", path.ReadBlockFolder());
        }
    }
}
