using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#248: keycuts.cmd and keypaste.cmd in the keycuts folder (keycuts and keypaste, sh, on macOS). Temp folders only.</summary>
    public class CommandShimsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _folder;
        private readonly string _other;

        public CommandShimsTests()
        {
            _folder = Path.Combine(_root, "Shortcuts");
            _other = Path.Combine(_root, "Other");
            Directory.CreateDirectory(_folder);
            Directory.CreateDirectory(_other);
        }

        public void Dispose()
        {
            Directory.Delete(_root, true);
        }

        [Fact]
        public void Ensure_WritesBothShims_ExactlyAndOnce()
        {
            var written = CommandShims.Ensure(_folder);

            Assert.Equal(new[] { "keycuts" + CommandShims.Extension, "keypaste" + CommandShims.Extension }, written.Select(Path.GetFileName));
            Assert.Equal(CommandShims.Bytes("keycuts"), File.ReadAllBytes(Path.Combine(_folder, "keycuts" + CommandShims.Extension)));
            Assert.Equal(CommandShims.Bytes("keypaste"), File.ReadAllBytes(Path.Combine(_folder, "keypaste" + CommandShims.Extension)));

            Assert.Empty(CommandShims.Ensure(_folder));
            Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        }

        [Fact]
        public void Ensure_PutsBackAShimThatWasChanged_OrMadeReadOnly()
        {
            CommandShims.Ensure(_folder);
            var path = Path.Combine(_folder, "keycuts" + CommandShims.Extension);
            File.WriteAllText(path, "@ECHO broken");
            File.SetAttributes(path, FileAttributes.ReadOnly);

            Assert.Equal(new[] { path }, CommandShims.Ensure(_folder));
            Assert.Equal(CommandShims.Bytes("keycuts"), File.ReadAllBytes(path));
        }

        [Fact]
        public void Ensure_DoesNothingForAFolderThatIsntThere()
        {
            Assert.Empty(CommandShims.Ensure(Path.Combine(_root, "missing")));
            Assert.False(Directory.Exists(Path.Combine(_root, "missing")));
        }

        [Fact]
        public void RemoveFrom_DeletesOnlyFilesThatAreExactlyTheShims()
        {
            CommandShims.Ensure(_other);
            File.WriteAllText(Path.Combine(_other, "keypaste" + CommandShims.Extension), "@ECHO the user's own keypaste");

            var removed = CommandShims.RemoveFrom(_other);

            Assert.Equal(new[] { Path.Combine(_other, "keycuts" + CommandShims.Extension) }, removed);
            Assert.True(File.Exists(Path.Combine(_other, "keypaste" + CommandShims.Extension)));
        }

        [Fact]
        public void Move_PutsTheShimsInTheNewFolder_AndTakesThemOutOfTheOld()
        {
            CommandShims.Ensure(_other);

            CommandShims.Move(_other, _folder);

            Assert.Equal(2, Directory.GetFiles(_folder, "*" + CommandShims.Extension).Length);
            Assert.Empty(Directory.GetFiles(_other, "*" + CommandShims.Extension));
        }

        [WindowsFact]
        public void Move_ToTheSameFolder_KeepsThem()
        {
            CommandShims.Move(_folder, _folder + @"\");

            Assert.Equal(2, Directory.GetFiles(_folder, "*" + CommandShims.Extension).Length);
        }

        // A folder changed while the app was closed (the CLI's -o): the next start finds the old one in the record and cleans it
        [Fact]
        public void Keep_CleansTheRecordedFolder_AndRecordsTheNewOne()
        {
            CommandShims.Ensure(_other);
            var recorded = new System.Collections.Generic.List<string>();

            CommandShims.Keep(_other, _folder, recorded.Add);

            Assert.Equal(2, Directory.GetFiles(_folder, "*" + CommandShims.Extension).Length);
            Assert.Empty(Directory.GetFiles(_other, "*" + CommandShims.Extension));
            Assert.Equal(new[] { _folder }, recorded);

            CommandShims.Keep(_folder, _folder, recorded.Add);   // nothing new to record
            Assert.Single(recorded);
        }

        // Found installing the MSI: it puts the shims in the keycuts folder without a record, and the first folder change (the
        // CLI's -o before the app ever ran) left them in the old folder. With no record, the folder moved from is cleaned.
        [Fact]
        public void Keep_WithNoRecord_CleansTheFolderMovedFrom()
        {
            CommandShims.Ensure(_other);
            var recorded = new System.Collections.Generic.List<string>();

            CommandShims.Keep(null, _folder, recorded.Add, previousFolder: _other);

            Assert.Equal(2, Directory.GetFiles(_folder, "*" + CommandShims.Extension).Length);
            Assert.Empty(Directory.GetFiles(_other, "*" + CommandShims.Extension));
            Assert.Equal(new[] { _folder }, recorded);
        }

        [Fact]
        public void Keep_PrefersTheRecord_OverTheFolderMovedFrom()
        {
            var recordedFolder = Path.Combine(_root, "Recorded");
            Directory.CreateDirectory(recordedFolder);
            CommandShims.Ensure(recordedFolder);
            CommandShims.Ensure(_other);

            CommandShims.Keep(recordedFolder, _folder, null, previousFolder: _other);

            Assert.Empty(Directory.GetFiles(recordedFolder, "*" + CommandShims.Extension));
            Assert.Equal(2, Directory.GetFiles(_other, "*" + CommandShims.Extension).Length);   // not ours to clean: the record says where they were
        }

        [Fact]
        public void Keep_WithNoRecord_JustWritesTheShims()
        {
            var recorded = new System.Collections.Generic.List<string>();

            CommandShims.Keep(null, _folder, recorded.Add);

            Assert.Equal(2, Directory.GetFiles(_folder, "*" + CommandShims.Extension).Length);
            Assert.Equal(new[] { _folder }, recorded);
        }

        [Fact]
        public void Keep_ForAMissingFolder_TouchesNothing_AndRecordsNothing()
        {
            CommandShims.Ensure(_other);
            var recorded = new System.Collections.Generic.List<string>();

            CommandShims.Keep(_other, Path.Combine(_root, "missing"), recorded.Add);

            Assert.Equal(2, Directory.GetFiles(_other, "*" + CommandShims.Extension).Length);
            Assert.Empty(recorded);
        }

        [WindowsTheory]
        [InlineData(@"C:\Users\me\AppData\Local\Programs\keycuts\", @"C:\Users\me\AppData\Local\Programs\keycuts\", false, true)]
        [InlineData(@"C:\Users\me\AppData\Local\Programs\keycuts\", @"c:\users\me\appdata\local\programs\keycuts", false, true)]
        [InlineData(@"C:\Users\me\AppData\Local\Programs\keycuts\", @"C:\git\keycuts\keycuts.GUI\bin\Debug\", false, false)]   // a build, not the installed copy
        [InlineData(@"C:\Users\me\AppData\Local\Programs\keycuts\", @"C:\Users\me\AppData\Local\Programs\keycuts\", true, false)]   // the Debug folder override
        [InlineData(null, @"C:\Portable\keycuts\", false, false)]   // never installed
        public void OnlyTheInstalledCopy_KeepsTheShims(string installFolder, string runningFolder, bool usesDebugFolder, bool expected)
        {
            Assert.Equal(expected, CommandShims.ShouldKeep(installFolder, runningFolder, usesDebugFolder));
        }

        [Fact]
        public async Task TheRepository_RefusesToDeleteAShim()
        {
            CommandShims.Ensure(_folder);
            var path = Path.Combine(_folder, "keycuts" + CommandShims.Extension);
            var repository = new KeycutRepository(_folder, new PermanentDeleter());

            var error = await Assert.ThrowsAsync<KeycutRepositoryException>(() => repository.DeleteAsync(new Keycut { Name = "keycuts", FilePath = path }));

            Assert.Equal(KeycutRepositoryError.InvalidName, error.Error);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task TheShims_ArentListedAsKeycuts()
        {
            CommandShims.Ensure(_folder);

            Assert.Empty(await new KeycutRepository(_folder, new PermanentDeleter()).LoadAllAsync());
        }

        #region macOS

        [MacFact]
        [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
        public void OnTheMac_TheShimsAreExecutableShScripts_ThatArentKeycuts()
        {
            CommandShims.Ensure(_folder);
            var path = Path.Combine(_folder, "keycuts");

            Assert.Equal("755", Convert.ToString((int)File.GetUnixFileMode(path), 8));
            Assert.Equal(ShFormat.Shebang, File.ReadLines(path).First());
            Assert.False(new ShFormat().IsKeycut(File.ReadAllLines(path)));
            Assert.DoesNotContain("\r", File.ReadAllText(path));
        }

        /// <summary>The shim as Terminal runs it, with a temp HOME and a fake keycuts that prints its arguments.</summary>
        private (int Code, string Output, string Error) RunShim(string command, string home, params string[] args)
        {
            var start = new System.Diagnostics.ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(_folder, command));
            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            start.Environment["HOME"] = home;
            using var process = System.Diagnostics.Process.Start(start);
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output, error);
        }

        [MacFact]
        [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
        public void OnTheMac_TheShimsRunTheRecordedKeycuts_WithTheirArguments()
        {
            CommandShims.Ensure(_folder);
            var home = Path.Combine(_root, "home");
            var install = Path.Combine(_root, "keycuts.app", "Contents", "MacOS");
            Directory.CreateDirectory(install);
            File.WriteAllText(Path.Combine(install, "keycuts"), "#!/bin/sh\nprintf '%s|' \"$@\"\n");
            File.SetUnixFileMode(Path.Combine(install, "keycuts"), (UnixFileMode)0b111_101_101);
            var pointer = Path.Combine(home, "Library", "Application Support", "keycuts", "install-folder");

            Assert.Equal(2, RunShim("keycuts", home, "list").Code);   // no pointer yet
            Assert.Contains("isn't installed", RunShim("keycuts", home).Error);

            Assert.True(CommandShims.RecordInstallFolder(pointer, install + "/", File.Exists));

            Assert.Equal((0, "list|a b|"), (RunShim("keycuts", home, "list", "a b").Code, RunShim("keycuts", home, "list", "a b").Output));
            Assert.Equal("paste|db|", RunShim("keypaste", home, "db").Output);
        }

        [Fact]
        public void TheInstallFolder_IsRecordedOnlyForACopyWithTheCliBesideIt_AndOnlyWhenItChanges()
        {
            var pointer = Path.Combine(_root, "support", "install-folder");

            Assert.False(CommandShims.RecordInstallFolder(pointer, "/Apps/build/", _ => false));
            Assert.False(File.Exists(pointer));

            Assert.True(CommandShims.RecordInstallFolder(pointer, "/Applications/keycuts.app/Contents/MacOS/", _ => true));
            Assert.Equal("/Applications/keycuts.app/Contents/MacOS", CommandShims.ReadInstallFolder(pointer));
            var written = File.GetLastWriteTimeUtc(pointer);
            File.SetLastWriteTimeUtc(pointer, written.AddHours(-1));

            Assert.True(CommandShims.RecordInstallFolder(pointer, "/Applications/keycuts.app/Contents/MacOS", _ => true));
            Assert.Equal(written.AddHours(-1), File.GetLastWriteTimeUtc(pointer));
            Assert.Null(CommandShims.ReadInstallFolder(Path.Combine(_root, "none")));
        }

        #endregion macOS

        // In one folder Win+R runs keycuts.bat before keycuts.cmd (#209), so a keycut with the name would take the command over:
        // that is why the names stay refused and an existing one is renamed at startup
        [WindowsFact]
        public void AKeycutNamedLikeAShim_WouldWinOverIt_SoTheNamesStayRefused()
        {
            var shim = Path.Combine(_folder, "keycuts" + CommandShims.Extension);
            var environment = new NameConflictEnvironment
            {
                WindowsFolder = @"C:\Windows",
                SystemFolder = @"C:\Windows\System32",
                UserPath = _folder,
                FileExists = path => string.Equals(path, shim, StringComparison.OrdinalIgnoreCase)
            };

            var conflict = Assert.Single(NameConflicts.Find("keycuts", _folder, environment));

            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
            Assert.False(KeycutCreator.IsValidName("keycuts"));
            Assert.False(KeycutCreator.IsValidName("keypaste"));
        }
    }
}
