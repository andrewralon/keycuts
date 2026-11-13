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

        // Only the supplied folders are searched: the output folder counts when it is on PATH, and not when it isn't
        [Fact]
        public async Task BareProgramName_ABatchFileInTheOutputFolderOnThePath_IsFound()
        {
            File.WriteAllText(Path.Combine(_folder, "tool.bat"), "@ECHO OFF\r\nSTART https://example.com\r\n");
            WriteLaunch("alias", "START \"\" /D \"\" \"tool\"");

            var keycut = await LoadAndCheck("alias", Lookup(_folder));

            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task BareProgramName_InTheOutputFolderWhenItIsNotOnThePath_IsNotFound()
        {
            File.WriteAllText(Path.Combine(_folder, "tool.exe"), "x");
            WriteLaunch("alias", "START \"\" /D \"\" \"tool.exe\"");

            var keycut = await LoadAndCheck("alias", Lookup(_programs));

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
        }

        [Fact]
        public async Task AQuestionMarkWildcard_IsNotMarked_ButAnExtendedPathPrefixIsNotOne()
        {
            WriteLaunch("wild", "START \"\" /B \"C:\\Tools\\app?.exe\"");
            WriteLaunch("extended", "START \"\" /B \"\\\\?\\C:\\no-such-folder-for-keycuts-tests\\app.exe\"");
            var repository = new KeycutRepository(_folder, new PermanentDeleter(), () => Lookup());
            var all = await repository.LoadAllAsync();

            await repository.CheckTargetsAsync(all);

            Assert.Equal(TargetStatus.Unknown, all.Single(k => k.Name == "wild").TargetStatus);
            Assert.Equal(TargetStatus.Missing, all.Single(k => k.Name == "extended").TargetStatus);
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

        #region START "" /D "<folder>" <program>: where Windows finds the program (#203)

        // What experiments with a throwaway program showed, through cmd /c file.bat and ShellExecute (Win+R) alike: the /D
        // folder is a place the program is looked for, a bad one stops START for a program found by name on PATH or in System32
        // ("The current directory is invalid"), and a program that is only in App Paths is found whatever the folder is,
        // because START hands a name it can't resolve itself to ShellExecute. The maintainer's Chrome launchers name a
        // "Program Files (x86)" folder that doesn't exist and work, because Chrome registers itself in App Paths.

        private string GoneFolder() => Path.Combine(_root, "no such folder");

        // The case that was wrong: the folder is missing, and the program is in App Paths. It starts, so not Missing; the stale
        // folder is a Warning.
        [Fact]
        public async Task StartInFolderGone_ProgramInAppPaths_IsAWarning_NotMissing()
        {
            var chrome = Program("chrome.exe");
            WriteLaunch("h", $"START \"\" /D \"{GoneFolder()}\" chrome.exe --profile-directory=\"Default\"");

            var keycut = await LoadAndCheck("h", Lookup(appPath: chrome));

            Assert.Equal(TargetStatus.Warning, keycut.TargetStatus);
            Assert.Equal(Path.Combine(GoneFolder(), "chrome.exe"), keycut.CheckedPath);
            Assert.Contains(GoneFolder(), keycut.CheckedDetail);
            Assert.Contains("doesn't exist", keycut.CheckedDetail);
            Assert.Contains("App Paths", keycut.CheckedDetail);
        }

        // Checked with a throwaway program: a program that START finds by name stops at a missing folder even when App Paths
        // has it too, so that launcher doesn't start
        [Fact]
        public async Task StartInFolderGone_ProgramOnThePathAndInAppPaths_IsMissing()
        {
            var tool = Program("tool.exe");
            WriteLaunch("h", $"START \"\" /D \"{GoneFolder()}\" tool.exe");

            var keycut = await LoadAndCheck("h", Lookup(_programs, tool));

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Contains("The current directory is invalid", keycut.CheckedDetail);
        }

        // With the folder there, App Paths alone is simply Ok
        [Fact]
        public async Task StartInFolderThere_ProgramOnlyInAppPaths_HasNoWarning()
        {
            var chrome = Program("chrome.exe");
            WriteLaunch("h", $"START \"\" /D \"{_folder}\" chrome.exe");

            Assert.Equal(TargetStatus.Ok, (await LoadAndCheck("h", Lookup(appPath: chrome))).TargetStatus);
        }

        [Fact]
        public async Task StartInFolderGone_ProgramFoundNowhere_IsMissing_AndSaysWhatWasSearched()
        {
            WriteLaunch("h", $"START \"\" /D \"{GoneFolder()}\" chrome.exe --profile-directory=\"Default\"");

            var keycut = await LoadAndCheck("h", Lookup(_programs));

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Equal(Path.Combine(GoneFolder(), "chrome.exe"), keycut.CheckedPath);
            Assert.Contains("start-in folder doesn't exist", keycut.CheckedDetail);
            Assert.Contains("App Paths", keycut.CheckedDetail);
        }

        // Found by name on PATH, but START stops at the missing folder before it gets to launch it
        [Fact]
        public async Task StartInFolderGone_ProgramOnlyOnThePath_IsMissing()
        {
            Program("tool.exe");
            WriteLaunch("h", $"START \"\" /D \"{GoneFolder()}\" tool.exe");

            var keycut = await LoadAndCheck("h", Lookup(_programs));

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Contains("start-in folder doesn't exist", keycut.CheckedDetail);
        }

        // The case that already passed: the program is in the start-in folder, on neither PATH nor App Paths
        [Fact]
        public async Task ProgramInTheStartInFolder_IsOk()
        {
            Program("tool.exe");
            WriteLaunch("h", $"START \"\" /D \"{_programs}\" tool.exe --x");

            var keycut = await LoadAndCheck("h", Lookup());

            Assert.Equal(TargetStatus.Ok, keycut.TargetStatus);
        }

        [Fact]
        public async Task ProgramInTheStartInFolder_WithoutItsExtension_IsOk()
        {
            Program("tool.exe");
            WriteLaunch("h", $"START \"\" /D \"{_programs}\" tool");

            Assert.Equal(TargetStatus.Ok, (await LoadAndCheck("h", Lookup())).TargetStatus);
        }

        // An existing folder that doesn't hold the program: it is found by name, as without a folder
        [Fact]
        public async Task StartInFolderThere_ProgramOnThePathOrInAppPaths_IsOk()
        {
            var elsewhere = Program("tool.exe");
            WriteLaunch("onpath", $"START \"\" /D \"{_folder}\" tool.exe");
            WriteLaunch("inapps", $"START \"\" /D \"{_folder}\" registered.exe");
            var registered = Program("registered.exe");

            Assert.Equal(TargetStatus.Ok, (await LoadAndCheck("onpath", Lookup(_programs))).TargetStatus);
            Assert.Equal(TargetStatus.Ok, (await LoadAndCheck("inapps", Lookup(appPath: registered))).TargetStatus);
            Assert.True(File.Exists(elsewhere));
        }

        [Fact]
        public async Task StartInFolderThere_ProgramFoundNowhere_IsMissing_AndSaysItSearchedTheFolderToo()
        {
            WriteLaunch("h", $"START \"\" /D \"{_programs}\" nothere.exe");

            var keycut = await LoadAndCheck("h", Lookup());

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Equal(Path.Combine(_programs, "nothere.exe"), keycut.CheckedPath);
            Assert.Equal("searched the start-in folder, the Windows folder, System32, PATH and App Paths", keycut.CheckedDetail);
        }

        // /D "" has no folder to be wrong
        [Fact]
        public async Task NoStartInFolder_ProgramFoundNowhere_SaysItSearchedByName()
        {
            WriteLaunch("h", "START \"\" /D \"\" nothere.exe");

            var keycut = await LoadAndCheck("h", Lookup());

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Equal("nothere.exe", keycut.CheckedPath);
            Assert.Equal("searched the Windows folder, System32, PATH and App Paths", keycut.CheckedDetail);
        }

        // A program given as a full path isn't searched for, so it's the plain check, whatever /D says (and App Paths can't help)
        [Fact]
        public async Task AFullPathProgram_IsAPlainExistsCheck_WhateverTheStartInFolderIs()
        {
            var tool = Program("tool.exe");
            WriteLaunch("there", $"START \"\" /D \"{GoneFolder()}\" {tool} --x");
            WriteLaunch("gone", $"START \"\" /D \"{GoneFolder()}\" {Path.Combine(_root, "missing", "tool.exe")} --x");

            Assert.Equal(TargetStatus.Ok, (await LoadAndCheck("there", Lookup(appPath: tool))).TargetStatus);

            var gone = await LoadAndCheck("gone", Lookup(appPath: tool));
            Assert.Equal(TargetStatus.Missing, gone.TargetStatus);
            Assert.Equal("", gone.CheckedDetail);
        }

        // START "" /B "<full path>" has no folder and no search either
        [Fact]
        public async Task AFullPathWithNoStartInFolder_IsAPlainExistsCheck()
        {
            WriteLaunch("gone", $"START \"\" /B \"{Path.Combine(_root, "missing", "tool.exe")}\"");

            var keycut = await LoadAndCheck("gone", Lookup());

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Equal("", keycut.CheckedDetail);
        }

        #endregion START "" /D "<folder>" <program>: where Windows finds the program (#203)

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

        // Step 4: don't claim what wasn't checked
        [Theory]
        // A quoted program and a quoted argument under /B: the parser holds them in one string and can't say where the program ends
        [InlineData("START \"\" /B \"C:\\a b\\app.exe\" \"C:\\c d\\file.txt\"")]
        // A START switch before the program: not read as a program, and its target has quotes in it
        [InlineData("START \"\" /D \"C:\\Tools\" /MIN \"app.exe\"")]
        // A wildcard isn't a file
        [InlineData("START \"\" /B \"C:\\Tools\\*.exe\"")]
        public async Task ProgramThatIsNotAPlausiblePath_IsNotMarked(string line)
        {
            WriteLaunch("odd", line);

            var keycut = await LoadAndCheck("odd");

            Assert.Equal(TargetStatus.Unknown, keycut.TargetStatus);
            Assert.Equal("", keycut.CheckedPath);
        }

        // readme.txt is searched for as typed only if PATHEXT lists .txt, so coming up empty says nothing about it
        [Fact]
        public async Task BareName_WithAnExtensionPathExtDoesNotList_IsNotMarked()
        {
            Program("readme.txt");
            WriteLaunch("bare", "START \"\" /D \"\" \"readme.txt\"");

            var keycut = await LoadAndCheck("bare", Lookup(_programs));

            Assert.Equal(TargetStatus.Unknown, keycut.TargetStatus);
        }

        // Step 5: say what was checked
        [Fact]
        public async Task Missing_RemembersTheExpandedPathThatWasLookedFor()
        {
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            WriteLaunch("gone", "START \"\" /D \"%SystemRoot%\" \"no-such-program-for-keycuts-tests.exe\" --flag");

            var keycut = await LoadAndCheck("gone");

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Equal(Path.Combine(systemRoot, "no-such-program-for-keycuts-tests.exe"), keycut.CheckedPath);
        }

        [Fact]
        public async Task Missing_ForABareName_RemembersTheName()
        {
            WriteLaunch("gone", "START \"\" /D \"\" \"no-such-program.exe\"");

            var keycut = await LoadAndCheck("gone");

            Assert.Equal(TargetStatus.Missing, keycut.TargetStatus);
            Assert.Equal("no-such-program.exe", keycut.CheckedPath);
        }

        [Fact]
        public async Task Ok_RemembersThePathItFound()
        {
            var tool = Program("tool.exe");
            WriteLaunch("here", $"START \"\" /B \"{tool}\"");

            var keycut = await LoadAndCheck("here");

            Assert.Equal(tool, keycut.CheckedPath);
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
