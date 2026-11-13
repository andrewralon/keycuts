using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The Missing mark (#203): it means "we looked for a specific path and it isn't there". These run the real load and
    /// check over throwaway .bat files in a temp folder, whose programs are real (empty) files there.
    /// </summary>
    public class TargetCheckTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;
        private readonly string _programs;

        public TargetCheckTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "out");
            _programs = Path.Combine(_root, "programs");
            Directory.CreateDirectory(_folder);
            Directory.CreateDirectory(_programs);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private string Program(string name)
        {
            var path = Path.Combine(_programs, name);
            File.WriteAllText(path, "x");
            return path;
        }

        /// <summary>A keycut with no REM lines, like the hand-written and 0.1.x ones, so only its launch line says anything.</summary>
        private void WriteLaunch(string name, string launchLine)
        {
            File.WriteAllLines(Path.Combine(_folder, name + ".bat"), new[] { "@echo OFF", launchLine, "exit" });
        }

        private async Task<Keycut> LoadAndCheck(string name)
        {
            var repository = new KeycutRepository(_folder);
            var all = await repository.LoadAllAsync();
            var keycut = all.Single(k => k.Name == name);

            await repository.CheckTargetsAsync(all);

            return keycut;
        }

        // The control row: one quoted program, a folder to start in
        [Fact]
        public async Task QuotedProgram_InAStartInFolder_IsOk()
        {
            Program("tool.exe");
            WriteLaunch("control", $"START \"\" /D \"{_programs}\" \"tool.exe\"");

            var keycut = await LoadAndCheck("control");

            Assert.Equal(Path.Combine(_programs, "tool.exe"), keycut.Target);
            Assert.Equal(Path.Combine(_programs, "tool.exe"), keycut.Program);
            Assert.Equal("", keycut.Arguments);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        // The 0.1.x form with a quoted argument: the program exists, so the keycut isn't Missing
        [Fact]
        public async Task QuotedProgramAndArgument_InAStartInFolder_ChecksTheProgram()
        {
            Program("tool.exe");
            var argument = Program("data.ini");
            WriteLaunch("args", $"START \"\" /D \"{_programs}\" \"tool.exe\" \"{argument}\"");

            var keycut = await LoadAndCheck("args");

            // The target is unchanged: program and arguments in one string, as the parser has always held it
            Assert.Equal(Path.Combine(_programs, "tool.exe") + "\" \"" + argument, keycut.Target);
            Assert.Equal(Path.Combine(_programs, "tool.exe"), keycut.Program);
            Assert.Equal($"\"{argument}\"", keycut.Arguments);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task QuotedProgramAndArgument_WithTheProgramGone_IsMissing()
        {
            var argument = Program("data.ini");
            WriteLaunch("gone", $"START \"\" /D \"{_programs}\" \"tool.exe\" \"{argument}\"");

            var keycut = await LoadAndCheck("gone");

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
        }

        // The program exists and the argument doesn't: an argument is the program's business, not a path we promise
        [Fact]
        public async Task QuotedArgument_ThatDoesNotExist_DoesNotMakeTheKeycutMissing()
        {
            Program("tool.exe");
            WriteLaunch("noarg", $"START \"\" /D \"{_programs}\" \"tool.exe\" \"? %*\"");

            var keycut = await LoadAndCheck("noarg");

            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task PlainFile_HasItsTargetAsItsProgram()
        {
            var tool = Program("plain.exe");
            WriteLaunch("plain", $"START \"\" /B \"{tool}\"");

            var keycut = await LoadAndCheck("plain");

            Assert.Equal(tool, keycut.Program);
            Assert.Equal("", keycut.Arguments);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        // Written by the emitter: its REM target is the program alone
        [Fact]
        public async Task EmittedFile_HasItsTargetAsItsProgram()
        {
            var tool = Program("emitted.exe");
            var shortcut = new Shortcut(tool, "emitted", _folder);
            new Runner().CreateShortcutFile(shortcut, true, "");

            var keycut = await LoadAndCheck("emitted");

            Assert.Equal(tool, keycut.Program);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task Folder_AndUrl_HaveNoProgram()
        {
            File.WriteAllLines(Path.Combine(_folder, "site.bat"), new[] { "@ECHO OFF", "REM <type>url</type>", "REM <target>https://example.com</target>", "START https://example.com", "EXIT" });
            File.WriteAllLines(Path.Combine(_folder, "dir.bat"), new[] { "@ECHO OFF", $"\"%SystemRoot%\\explorer.exe\" \"{_programs}\"", "EXIT" });
            var all = await new KeycutRepository(_folder).LoadAllAsync();

            Assert.All(all, k => Assert.Equal("", k.Program));
            Assert.All(all, k => Assert.Equal("", k.Arguments));
        }
    }
}
