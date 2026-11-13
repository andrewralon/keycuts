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

        /// <summary>
        /// Where a bare program name is looked for: these folders as the whole PATH, and a System32 that is a temp folder,
        /// so a check never reads this machine's PATH or registry. App Paths holds <paramref name="appPath"/> for every name.
        /// </summary>
        private NameConflictEnvironment Lookup(string pathFolders = "", string appPath = null)
        {
            return new NameConflictEnvironment
            {
                WindowsFolder = Path.Combine(_root, "windows"),
                SystemFolder = Path.Combine(_root, "system32"),
                MachinePath = "",
                UserPath = pathFolders,
                PathExt = ".EXE;.BAT",
                FileExists = File.Exists,
                FindAppPath = _ => appPath
            };
        }

        private async Task<Keycut> LoadAndCheck(string name, NameConflictEnvironment lookup = null)
        {
            var env = lookup ?? Lookup();
            var repository = new KeycutRepository(_folder, new PermanentDeleter(), () => env);
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

        // The hand-written Chrome launcher (tests/corpus/legacy/not-keycuts/chromep0.bat), with a program that exists here
        [Fact]
        public async Task UnquotedProgramAndArgument_InAStartInFolder_ChecksTheProgram()
        {
            Program("tool.exe");
            WriteLaunch("chrome", $"START \"\" /D \"{_programs}\" tool.exe --profile-directory=\"Profile 2\"");

            var keycut = await LoadAndCheck("chrome");

            // The target is the program alone, a path, not the old string with a stray quote in the middle
            Assert.Equal(Path.Combine(_programs, "tool.exe"), keycut.Target);
            Assert.Equal(Path.Combine(_programs, "tool.exe"), keycut.Program);
            Assert.Equal("--profile-directory=\"Profile 2\"", keycut.Arguments);
            Assert.Equal(KeycutKind.File, keycut.Kind);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task UnquotedProgramAndArgument_WithTheProgramGone_IsMissing()
        {
            WriteLaunch("chrome", $"START \"\" /D \"{_programs}\" tool.exe --profile-directory=\"Profile 2\"");

            var keycut = await LoadAndCheck("chrome");

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
        }

        // The start-in folder is what is missing, as for the maintainer's launchers: START doesn't launch anything then
        [Fact]
        public async Task UnquotedProgram_WithTheStartInFolderGone_IsMissing()
        {
            WriteLaunch("chrome", $"START \"\" /D \"{Path.Combine(_root, "no such folder")}\" tool.exe --x");

            var keycut = await LoadAndCheck("chrome");

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
        }

        // The emitter can't write these lines back as they are, so the Edit form must keep refusing them
        [Theory]
        [InlineData("START \"\" /D \"{0}\" tool.exe --profile-directory=\"Profile 2\"")]
        [InlineData("START \"\" /D \"{0}\" \"tool.exe\" \"data.ini\"")]
        [InlineData("START \"\" /D \"{0}\" \"tool.exe\" --flag")]
        public void StartInFolderLaunchers_CannotBeEditedInTheForm(string line)
        {
            Program("tool.exe");
            WriteLaunch("launcher", string.Format(line, _programs));

            Assert.False(KeycutRepository.CanEditInForm(Path.Combine(_folder, "launcher.bat")));
        }

        // The last row of the issue's table: no folder, so Windows finds the program, File.Exists doesn't
        [Fact]
        public async Task BareProgramName_OnThePath_IsOk()
        {
            Program("tool.exe");
            WriteLaunch("bare", "START \"\" /D \"\" \"tool.exe\"");

            var keycut = await LoadAndCheck("bare", Lookup(_programs));

            Assert.Equal("tool.exe", keycut.Target);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        // PATHEXT supplies the extension, as it does for Win+R
        [Fact]
        public async Task BareProgramName_WithoutAnExtension_IsFoundThroughPathExt()
        {
            Program("tool.exe");
            WriteLaunch("bare", "START \"\" /D \"\" \"tool\"");

            var keycut = await LoadAndCheck("bare", Lookup(_programs));

            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task BareProgramName_InTheSystemFolder_IsOk()
        {
            var system = Path.Combine(_root, "system32");
            Directory.CreateDirectory(system);
            File.WriteAllText(Path.Combine(system, "notepad.exe"), "x");
            WriteLaunch("bare", "START \"\" /D \"\" \"notepad.exe\"");

            var keycut = await LoadAndCheck("bare");

            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task BareProgramName_InAppPaths_IsOk()
        {
            var registered = Program("registered.exe");
            WriteLaunch("bare", "START \"\" /D \"\" \"registered\"");

            var keycut = await LoadAndCheck("bare", Lookup(appPath: registered));

            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task BareProgramName_FoundNowhere_IsMissing()
        {
            // It exists, but not in a folder Windows looks in
            Program("tool.exe");
            WriteLaunch("bare", "START \"\" /D \"\" \"tool.exe\"");

            var keycut = await LoadAndCheck("bare");

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
        }

        [Fact]
        public async Task BareProgramName_WithArguments_IsLookedUpWithoutThem()
        {
            Program("tool.exe");
            WriteLaunch("bare", "START \"\" /D \"\" tool.exe --profile-directory=\"Profile 2\"");

            var keycut = await LoadAndCheck("bare", Lookup(_programs));

            Assert.Equal("tool.exe", keycut.Program);
            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        // Only a keycut with a bare name needs the PATH and registry, so the others never read them
        [Fact]
        public async Task TheLookupEnvironment_IsOnlyReadForABareName()
        {
            var tool = Program("tool.exe");
            WriteLaunch("full", $"START \"\" /B \"{tool}\"");
            var repository = new KeycutRepository(_folder, new PermanentDeleter(), () => throw new InvalidOperationException("read the environment"));
            var all = await repository.LoadAllAsync();

            await repository.CheckTargetsAsync(all);

            Assert.Equal(TargetStatus.Ok, all.Single().TargetStatus);
        }

        // If the environment can't be read, nothing is claimed
        [Fact]
        public async Task BareProgramName_WithAnUnreadableEnvironment_IsNotMarked()
        {
            WriteLaunch("bare", "START \"\" /D \"\" \"tool.exe\"");
            var repository = new KeycutRepository(_folder, new PermanentDeleter(), () => throw new InvalidOperationException("no registry"));
            var all = await repository.LoadAllAsync();

            await repository.CheckTargetsAsync(all);

            Assert.Equal(TargetStatus.Unknown, all.Single().TargetStatus);
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
