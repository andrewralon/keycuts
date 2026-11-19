using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// Run counts (#187): the lines every keycut gets, what reads them back, and how renames, deletes and the upgrade of
    /// older keycuts keep them in step. Everything uses a temp usage folder, never the real %LOCALAPPDATA%.
    /// </summary>
    public class UsageTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;
        private readonly string _usageFolder;
        private readonly UsageLog _usage;

        public UsageTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "out");
            _usageFolder = Path.Combine(_root, "localappdata", "keycuts", "usage");
            Directory.CreateDirectory(_folder);
            _usage = new UsageLog(_usageFolder);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        #region Helpers

        private KeycutRepository Repo() => new KeycutRepository(_folder, new PermanentDeleter(), null, _usage);

        private string Create(string name, string target = "https://example.com", string description = "")
        {
            var shortcut = new Shortcut(target, name + ShortcutFile.Extension, _folder);
            new Runner().CreateShortcutFile(shortcut, true, description);
            return shortcut.FullPath;
        }

        private void SetRuns(string name, int runs, DateTime lastRun)
        {
            Directory.CreateDirectory(_usageFolder);
            var path = Path.Combine(_usageFolder, name + UsageLog.Extension);
            File.WriteAllText(path, string.Concat(Enumerable.Repeat("1\r\n", runs)));
            File.SetLastWriteTime(path, lastRun);
        }

        private string RunsFile(string name) => Path.Combine(_usageFolder, name + UsageLog.Extension);

        /// <summary>The lines of an older keycut: the same as the emitter's, without the ones that count runs.</summary>
        private static string[] OldLines(string path, string url = "https://example.com")
        {
            return new[]
            {
                "@ECHO OFF",
                "REM keycuts 1.5.1",
                $"REM <shortcut>{path}</shortcut>",
                "REM <type>url</type>",
                $"REM <target>{url}</target>",
                "REM <description></description>",
                $"START {url}",
                "EXIT"
            };
        }

        #endregion Helpers

        #region The lines in the .bat

        [Fact]
        public void TheEmitter_WritesTheUsageLines_AfterTheLaunchLine_AndBeforeExit()
        {
            var shortcut = new Shortcut("https://example.com", "mail.bat", @"C:\Shortcuts");

            var lines = Runner.BuildShortcutLines(shortcut);

            Assert.Equal("START https://example.com", lines[^4]);
            Assert.Equal(UsageLog.BatLines, lines.Skip(lines.Count - 3).Take(2));
            Assert.Equal("EXIT", lines[^1]);
        }

        [Fact]
        public void TheBatFolder_IsTheFolderTheAppReads()
        {
            Assert.Equal(Environment.ExpandEnvironmentVariables(UsageLog.BatFolder), UsageLog.DefaultFolder, ignoreCase: true);
        }

        [Fact]
        public void TheUsageLines_AreNeverReadAsTheLaunchLine()
        {
            Assert.All(UsageLog.BatLines, line => Assert.False(ShortcutFile.IsLaunchLine(line)));
        }

        [Theory]
        [InlineData("https://mail.google.com/mail/u/0/#inbox", ShortcutType.Url)]
        [InlineData(@"C:\Windows", ShortcutType.Folder)]
        [InlineData(@"C:\Windows\notepad.exe", ShortcutType.File)]
        public void ANewKeycut_ReadsBackTheSame_AndCanBeEditedInTheForm(string target, ShortcutType type)
        {
            var path = Create("k", target);

            var parsed = new ShortcutFile(path);

            Assert.Equal(type, parsed.Type);
            Assert.Equal(target, parsed.Target);
            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void AKeycutFromBeforeCounting_CanStillBeEditedInTheForm()
        {
            var path = Path.Combine(_folder, "old.bat");
            File.WriteAllLines(path, OldLines(path));

            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public async Task SavingTheEditForm_GivesAnOldKeycutTheLines()
        {
            var path = Path.Combine(_folder, "old.bat");
            File.WriteAllLines(path, OldLines(path));
            var keycut = (await Repo().LoadAllAsync()).Single();

            var saved = await Repo().UpdateAsync(keycut, new KeycutEdit(keycut.Target, "old", "now counts"));

            Assert.True(saved.CountsRuns);
            Assert.True(UsageLog.IsCounted(File.ReadAllLines(path)));
        }

        // The lines are pure cmd, so they're run for real: by the full path, as Win+R and Explorer start a keycut, and by a
        // name typed in another case, as a terminal does. LOCALAPPDATA points at the test's folder.
        [Theory]
        [InlineData("gmail")]
        [InlineData("a&b (x)%y")]
        [InlineData("my.app")]
        public void RunningAKeycut_AddsOneRun_EachTime(string name)
        {
            var path = Path.Combine(_folder, name + ShortcutFile.Extension);
            // A Command keycut that does nothing, so the launch line starts nothing that stays open
            File.WriteAllLines(path, Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Command, "rem", path)));

            Assert.Equal(0, RunBat("\"\"" + path + "\"\""));
            Assert.Equal(0, RunBat("\"\"" + path + "\"\""));

            var record = new UsageLog(_usageFolder).Read(name);
            Assert.NotNull(record);
            Assert.Equal(2, record.Runs);
            Assert.True(DateTime.Now - record.LastRun < TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void RunningAKeycutByName_InAnotherCase_CountsUnderItsOwnName()
        {
            var path = Path.Combine(_folder, "gm.bat");
            File.WriteAllLines(path, Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Command, "rem", path)));

            // Found on PATH, as a terminal finds a keycut
            Assert.Equal(0, RunBat("GM", pathFolder: _folder));

            Assert.Equal(1, new UsageLog(_usageFolder).Read("gm").Runs);
        }

        [Fact]
        public void AUsageFileThatCantBeWritten_StillLaunches_AndSaysNothing()
        {
            var path = Path.Combine(_folder, "blocked.bat");
            var marker = Path.Combine(_root, "launched.txt");
            // The launch line writes a marker, so the test can see it ran
            File.WriteAllLines(path, Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Command, $"echo x> \"{marker}\"", path)));
            // A folder where the record would go: the append fails
            Directory.CreateDirectory(RunsFile("blocked"));

            var exitCode = RunBat("\"\"" + path + "\"\"", captureErrors: out var errors);

            Assert.Equal(0, exitCode);
            Assert.Equal("", errors.Trim());
            Assert.True(SpinUntil(() => File.Exists(marker)));
        }

        // The exit code is the launch's, as before counting: on the first run (the folder is made), on later runs, and when
        // the folder can't be made
        [Theory]
        [InlineData("fresh")]
        [InlineData("again")]
        [InlineData("unwritable")]
        public void AKeycut_ExitsWithTheLaunchsCode(string usageState)
        {
            var path = Path.Combine(_folder, "seven.bat");
            File.WriteAllLines(path, Runner.BuildShortcutLines(Shortcut.FromParts(ShortcutType.Command, "rem", path))
                .Select(line => line.StartsWith("START", StringComparison.Ordinal) ? "CMD /C EXIT 7" : line));
            var localAppData = Path.Combine(_root, "localappdata");

            if (usageState == "again")
            {
                Directory.CreateDirectory(_usageFolder);
            }
            else if (usageState == "unwritable")
            {
                // A file where the keycuts folder would go: MD fails
                Directory.CreateDirectory(localAppData);
                File.WriteAllText(Path.Combine(localAppData, "keycuts"), "");
            }

            Assert.Equal(7, RunBat("\"\"" + path + "\"\"", captureErrors: out var errors));
            Assert.Equal("", errors.Trim());
        }

        private int RunBat(string commandLine, string pathFolder = null)
        {
            return RunBat(commandLine, out _, pathFolder);
        }

        private int RunBat(string commandLine, out string captureErrors, string pathFolder = null)
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c " + commandLine)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = _root
            };
            info.Environment["LOCALAPPDATA"] = Path.Combine(_root, "localappdata");

            if (pathFolder != null)
            {
                info.Environment["PATH"] = pathFolder + ";" + Environment.GetEnvironmentVariable("PATH");
            }

            using (var process = Process.Start(info))
            {
                var errors = process.StandardError.ReadToEndAsync();
                process.StandardOutput.ReadToEnd();
                Assert.True(process.WaitForExit(10000));
                captureErrors = errors.Result;
                return process.ExitCode;
            }
        }

        private static bool SpinUntil(Func<bool> condition)
        {
            return System.Threading.SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10));
        }

        #endregion The lines in the .bat

        #region Reading

        [Theory]
        [InlineData("", 0)]
        [InlineData("1\r\n", 1)]
        [InlineData("1\r\n1\r\n1\r\n", 3)]
        [InlineData("1\n1\n", 2)]
        [InlineData("garbage", 1)]
        [InlineData("1\r\n1", 2)]
        public void CountRuns_IsTheNumberOfLines(string contents, int runs)
        {
            Assert.Equal(runs, UsageLog.CountRuns(Encoding.ASCII.GetBytes(contents)));
        }

        [Fact]
        public void ReadAll_IsByName_InAnyCase_WithTheLastRunFromTheFile()
        {
            var lastRun = new DateTime(2026, 3, 4, 5, 6, 7);
            SetRuns("Gmail", 3, lastRun);

            var records = _usage.ReadAll();

            Assert.Equal(new UsageRecord(3, lastRun), records["gmail"]);
        }

        [Fact]
        public void AMissingFolder_HasNoRecords_AndNoStart()
        {
            Assert.Empty(_usage.ReadAll());
            Assert.Null(_usage.Read("gmail"));
            Assert.Null(_usage.CountingSince());
        }

        [Fact]
        public async Task LoadAll_GivesEachKeycutItsRuns_ZeroWhenItCountsAndHasntRun_AndNoneWhenItDoesntCount()
        {
            Create("used");
            Create("unused");
            var oldPath = Path.Combine(_folder, "old.bat");
            File.WriteAllLines(oldPath, OldLines(oldPath));
            var lastRun = DateTime.Now.AddDays(-3);
            SetRuns("used", 5, lastRun);

            var keycuts = (await Repo().LoadAllAsync()).ToDictionary(k => k.Name);

            Assert.Equal(5, keycuts["used"].Runs);
            Assert.Equal(lastRun, keycuts["used"].LastRun);
            Assert.Equal(0, keycuts["unused"].Runs);
            Assert.Equal(default, keycuts["unused"].LastRun);
            Assert.Null(keycuts["old"].Runs);
            Assert.False(keycuts["old"].CountsRuns);
        }

        [Fact]
        public async Task ARepositoryWithoutAUsageLog_ReadsNoRuns()
        {
            Create("used");
            SetRuns("used", 5, DateTime.Now);

            var keycut = (await new KeycutRepository(_folder).LoadAllAsync()).Single();

            Assert.True(keycut.CountsRuns);
            Assert.Null(keycut.Runs);
        }

        #endregion Reading

        #region Keeping in step

        [Fact]
        public async Task Rename_TakesTheRunsAlong_AndReplacesWhatTheNewNameHadLeftOver()
        {
            Create("mail");
            SetRuns("mail", 4, new DateTime(2026, 1, 2));
            SetRuns("inbox", 9, new DateTime(2025, 1, 2));
            var keycut = (await Repo().LoadAllAsync()).Single();

            await Repo().RenameAsync(keycut, "inbox");

            Assert.False(File.Exists(RunsFile("mail")));
            Assert.Equal(new UsageRecord(4, new DateTime(2026, 1, 2)), _usage.Read("inbox"));
        }

        [Fact]
        public async Task Rename_OfAKeycutThatHasntRun_ClearsWhatTheNewNameHadLeftOver()
        {
            Create("mail");
            SetRuns("inbox", 9, new DateTime(2025, 1, 2));
            var keycut = (await Repo().LoadAllAsync()).Single();

            await Repo().RenameAsync(keycut, "inbox");

            Assert.Null(_usage.Read("inbox"));
        }

        [Fact]
        public async Task Rename_ChangingOnlyTheCase_KeepsTheRuns()
        {
            Create("mail");
            SetRuns("mail", 4, new DateTime(2026, 1, 2));
            var keycut = (await Repo().LoadAllAsync()).Single();

            await Repo().RenameAsync(keycut, "Mail");

            Assert.Equal(4, _usage.Read("Mail").Runs);
            Assert.Equal("Mail" + UsageLog.Extension, Path.GetFileName(Directory.GetFiles(_usageFolder).Single()));
        }

        [Fact]
        public async Task TheEditForm_RenamingAKeycut_TakesTheRunsAlong()
        {
            Create("mail");
            SetRuns("mail", 4, new DateTime(2026, 1, 2));
            var keycut = (await Repo().LoadAllAsync()).Single();

            var saved = await Repo().UpdateAsync(keycut, new KeycutEdit(keycut.Target, "inbox", ""));

            Assert.Equal(4, saved.Runs);
            Assert.Null(_usage.Read("mail"));
        }

        [Fact]
        public async Task Delete_RemovesTheRuns_AndUndoPutsThemBack()
        {
            Create("mail");
            var lastRun = new DateTime(2026, 1, 2, 3, 4, 5);
            SetRuns("mail", 4, lastRun);
            var keycut = (await Repo().LoadAllAsync()).Single();

            var deleted = await Repo().DeleteAsync(keycut);

            Assert.False(File.Exists(RunsFile("mail")));

            await Repo().RestoreAsync(deleted);

            Assert.Equal(new UsageRecord(4, lastRun), _usage.Read("mail"));
        }

        [Fact]
        public void ANewKeycut_StartsFromNothing_AndAReplacedOneKeepsItsRuns()
        {
            SetRuns("mail", 4, DateTime.Now);
            var runner = new Runner(_folder, new NoOpPathUpdater()) { Usage = _usage };

            // A keycut named mail was deleted outside keycuts, and its record stayed
            runner.CreateShortcutFile(new Shortcut("https://example.com", "mail.bat", _folder));
            Assert.Null(_usage.Read("mail"));

            SetRuns("mail", 4, DateTime.Now);
            runner.CreateShortcutFile(new Shortcut("https://example.org", "mail.bat", _folder), overwrite: true);
            Assert.Equal(4, _usage.Read("mail").Runs);
        }

        #endregion Keeping in step

        #region Adding the lines to older keycuts

        [Fact]
        public void AddRunCounting_PutsTheLinesBeforeExit_AndChangesNothingElse()
        {
            var path = Path.Combine(_folder, "old.bat");
            var old = OldLines(path);
            File.WriteAllLines(path, old);
            var modified = new DateTime(2024, 5, 6, 7, 8, 9);
            File.SetLastWriteTime(path, modified);

            Assert.Equal(1, Repo().AddRunCounting());

            var expected = old.Take(old.Length - 1).Concat(UsageLog.BatLines).Concat(new[] { "EXIT" });
            Assert.Equal(expected, File.ReadAllLines(path));
            Assert.Equal(modified, File.GetLastWriteTime(path));
            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void AddRunCounting_KeepsTheBytes_ABomLineFeedsAndAnAnsiByte()
        {
            var path = Path.Combine(_folder, "old.bat");
            // UTF-8 BOM, LF line breaks, and a description with an ANSI é (0xE9), which isn't valid UTF-8
            var head = "@ECHO OFF\nREM <type>url</type>\nREM <target>https://example.com</target>\nREM <description>caf\u00e9</description>\nSTART https://example.com\n";
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.Latin1.GetBytes(head + "EXIT\n")).ToArray();
            File.WriteAllBytes(path, bytes);

            Assert.Equal(1, Repo().AddRunCounting());

            var expected = new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(Encoding.Latin1.GetBytes(head + string.Concat(UsageLog.BatLines.Select(l => l + "\n")) + "EXIT\n")).ToArray();
            Assert.Equal(expected, File.ReadAllBytes(path));
        }

        [Fact]
        public void AddRunCounting_IsOnlyDoneOnce()
        {
            var path = Path.Combine(_folder, "old.bat");
            File.WriteAllLines(path, OldLines(path));
            Repo().AddRunCounting();
            var once = File.ReadAllBytes(path);

            Assert.Equal(0, Repo().AddRunCounting());
            Assert.Equal(once, File.ReadAllBytes(path));
        }

        [Fact]
        public void AddRunCounting_LeavesAloneAFileThatDoesntEndWithExit_OrIsntAKeycut()
        {
            var noExit = Path.Combine(_folder, "noexit.bat");
            File.WriteAllLines(noExit, OldLines(noExit).Take(7));
            var notAKeycut = Path.Combine(_folder, "script.bat");
            File.WriteAllLines(notAKeycut, new[] { "@ECHO OFF", "echo hello", "EXIT" });
            var before = new[] { File.ReadAllBytes(noExit), File.ReadAllBytes(notAKeycut) };

            Assert.Equal(0, Repo().AddRunCounting());

            Assert.Equal(before[0], File.ReadAllBytes(noExit));
            Assert.Equal(before[1], File.ReadAllBytes(notAKeycut));
        }

        [Fact]
        public void AddRunCounting_SkipsAReadOnlyFile_AndCarriesOn()
        {
            var locked = Path.Combine(_folder, "a.bat");
            File.WriteAllLines(locked, OldLines(locked));
            File.SetAttributes(locked, FileAttributes.ReadOnly);
            var other = Path.Combine(_folder, "b.bat");
            File.WriteAllLines(other, OldLines(other));

            try
            {
                Assert.Equal(1, Repo().AddRunCounting());
                Assert.False(UsageLog.IsCounted(File.ReadAllLines(locked)));
                Assert.True(UsageLog.IsCounted(File.ReadAllLines(other)));
            }
            finally
            {
                File.SetAttributes(locked, FileAttributes.Normal);
            }
        }

        [Fact]
        public void AddRunCountingOnce_AddsTheLines_AndRecordsTheFolder_ThenLeavesItAlone()
        {
            var path = Path.Combine(_folder, "old.bat");
            File.WriteAllLines(path, OldLines(path));
            var done = new List<string>();

            Assert.True(Repo().AddRunCountingOnce(done.Contains, done.Add));
            Assert.True(UsageLog.IsCounted(File.ReadAllLines(path)));
            Assert.Equal(new[] { _folder }, done);
            Assert.True(Directory.Exists(_usageFolder));

            // Lines taken out by hand stay out: the folder had its turn (#238)
            File.WriteAllLines(path, OldLines(path));
            Assert.False(Repo().AddRunCountingOnce(done.Contains, done.Add));
            Assert.False(UsageLog.IsCounted(File.ReadAllLines(path)));
            Assert.Single(done);
        }

        [Fact]
        public void AddRunCountingOnce_DoesntRecordAFolderThatIsntThere_SoItGetsItsTurnLater()
        {
            Directory.Delete(_folder);
            var done = new List<string>();

            Repo().AddRunCountingOnce(done.Contains, done.Add);

            Assert.Empty(done);
        }

        [Fact]
        public void AddRunCountingOnce_WithoutARecord_RunsEveryTime()
        {
            var path = Path.Combine(_folder, "old.bat");
            File.WriteAllLines(path, OldLines(path));
            Assert.True(Repo().AddRunCountingOnce(null, null));

            File.WriteAllLines(path, OldLines(path));
            Assert.True(Repo().AddRunCountingOnce(null, null));
            Assert.True(UsageLog.IsCounted(File.ReadAllLines(path)));
        }

        [Theory]
        [InlineData(null, new string[0], @"C:\Shortcuts", false)]
        [InlineData(@"C:\Shortcuts", new string[0], @"c:\shortcuts\", true)]   // the value from before #238 still counts
        [InlineData(null, new[] { @"D:\Other", @"C:\Shortcuts\" }, @"C:\SHORTCUTS", true)]
        [InlineData(@"C:\Shortcuts", new[] { @"D:\Other" }, @"E:\New", false)]
        [InlineData(@"C:\Shortcuts", new[] { @"D:\Other" }, "", false)]
        public void IsRunCountingDone_ReadsTheOldValueAndTheList(string oldFolder, string[] folders, string folder, bool expected)
        {
            Assert.Equal(expected, RegistryStuff.IsRunCountingDone(oldFolder, folders, folder));
        }

        [Fact]
        public void AddFolder_AppendsANewFolderOnly()
        {
            Assert.Equal(new[] { @"C:\Shortcuts" }, RegistryStuff.AddFolder(null, @"C:\Shortcuts"));
            Assert.Equal(new[] { @"C:\Shortcuts", @"D:\Other" }, RegistryStuff.AddFolder(new[] { @"C:\Shortcuts" }, @"D:\Other"));
            Assert.Null(RegistryStuff.AddFolder(new[] { @"C:\Shortcuts" }, @"c:\shortcuts\"));
            Assert.Null(RegistryStuff.AddFolder(new[] { @"C:\Shortcuts" }, " "));
        }

        [Fact]
        public void TheCorpus_ParsesTheSame_WithTheLinesAdded()
        {
            var corpus = Path.Combine(FindRepoRoot(), "tests", "corpus");
            var copied = 0;

            foreach (var file in Directory.EnumerateFiles(corpus, "*.bat", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(_folder, Path.GetFileName(file)), true);
                copied++;
            }

            Assert.True(copied > 0);
            var before = Directory.GetFiles(_folder).ToDictionary(f => f, f => new ShortcutFile(f));

            Repo().AddRunCounting();

            foreach (var (file, parsed) in before)
            {
                var after = new ShortcutFile(file);
                Assert.Equal((parsed.Type, parsed.Target, parsed.Description, parsed.Command), (after.Type, after.Target, after.Description, after.Command));
            }
        }

        private static string FindRepoRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);

            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "keycuts.sln")))
            {
                folder = folder.Parent;
            }

            return folder?.FullName ?? throw new InvalidOperationException("Can't find keycuts.sln.");
        }

        #endregion Adding the lines to older keycuts
    }
}
