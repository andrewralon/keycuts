using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using keycuts.Common;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #196 and #207: keycuts written by older versions brought to the current format in one pass, with a backup, keeping bytes
    /// and dates. Temp folders only; nothing reads the real keycuts folder or the registry.
    /// </summary>
    public class KeycutMigrationTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));

        private static readonly DateTime Now = new DateTime(2026, 11, 17, 15, 30, 0);

        private static readonly DateTime Old = new DateTime(2019, 4, 5, 6, 7, 8);

        public KeycutMigrationTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_folder, true);
        }

        private string Write(string name, string text, bool bom = false, DateTime? modified = null)
        {
            var path = Path.Combine(_folder, name);
            var body = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(path, bom ? Encoding.UTF8.GetPreamble().Concat(body).ToArray() : body);
            File.SetLastWriteTime(path, modified ?? Old);
            return path;
        }

        private static string OldFolderKeycut(string path, string target = @"%USERPROFILE%\Downloads", string newline = "\r\n")
        {
            return string.Join(newline, new[]
            {
                "@ECHO OFF",
                "REM keycuts 1.2.0",
                $"REM <shortcut>{path}</shortcut>",
                "REM <type>folder</type>",
                $"REM <destination>{target}</destination>",
                "REM <description></description>",
                $"\"%SystemRoot%\\explorer.exe\" \"{target}\"",
                "EXIT"
            }) + newline;
        }

        [Fact]
        public void AnOldFolderKeycut_GetsTheTargetTag_AndTheStartLine_AndNothingElse()
        {
            var path = Path.Combine(_folder, "dl.bat");
            Write("dl.bat", OldFolderKeycut(path));
            var before = new ShortcutFile(path);

            var result = KeycutMigration.Run(_folder, Now);

            Assert.Equal(new[] { "dl.bat" }, result.Migrated);
            Assert.Equal((1, 1), (result.TagsUpdated, result.LaunchLinesUpdated));
            Assert.Equal(OldFolderKeycut(path)
                .Replace("<destination>", "<target>").Replace("</destination>", "</target>")
                .Replace("\"%SystemRoot%", "START \"\" /B \"%SystemRoot%"), File.ReadAllText(path));

            var after = new ShortcutFile(path);
            Assert.Equal((before.Type, before.Target), (after.Type, after.Target));
        }

        [Fact]
        public void TheDateModified_IsKept_AndTheCreationTimeToo()
        {
            var path = Write("dl.bat", OldFolderKeycut(Path.Combine(_folder, "dl.bat")));
            var created = File.GetCreationTimeUtc(path);

            KeycutMigration.Run(_folder, Now);

            Assert.Equal(Old, File.GetLastWriteTime(path));
            Assert.Equal(created, File.GetCreationTimeUtc(path));
        }

        [Fact]
        public void TheOriginals_AreBackedUpByteForByte_InAHiddenFolderOffPath()
        {
            var path = Write("dl.bat", OldFolderKeycut(Path.Combine(_folder, "dl.bat")));
            var original = File.ReadAllBytes(path);
            Write("new.bat", "@ECHO OFF\r\nREM <type>url</type>\r\nREM <target>https://example.com</target>\r\nSTART https://example.com\r\nEXIT\r\n");

            var result = KeycutMigration.Run(_folder, Now);

            var expected = Path.Combine(_folder, KeycutMigration.BackupFolderName, "20261117-153000");
            Assert.Equal(expected, result.BackupFolder);
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(expected, "dl.bat")));
            Assert.False(File.Exists(Path.Combine(expected, "new.bat")));   // only what changed
            Assert.True((File.GetAttributes(Path.Combine(_folder, KeycutMigration.BackupFolderName)) & FileAttributes.Hidden) != 0);
        }

        [Fact]
        public void AnExistingBackup_IsNeverOverwritten()
        {
            Write("dl.bat", OldFolderKeycut(Path.Combine(_folder, "dl.bat")));
            var taken = Path.Combine(_folder, KeycutMigration.BackupFolderName, "20261117-153000");
            Directory.CreateDirectory(taken);
            File.WriteAllText(Path.Combine(taken, "dl.bat"), "an earlier backup");

            var result = KeycutMigration.Run(_folder, Now);

            Assert.Equal(taken + "-2", result.BackupFolder);
            Assert.Equal("an earlier backup", File.ReadAllText(Path.Combine(taken, "dl.bat")));
        }

        [Fact]
        public void ASecondRun_ChangesNothing_AndMakesNoBackup()
        {
            var path = Write("dl.bat", OldFolderKeycut(Path.Combine(_folder, "dl.bat")));
            KeycutMigration.Run(_folder, Now);
            var once = File.ReadAllBytes(path);

            var again = KeycutMigration.Run(_folder, Now.AddMinutes(1));

            Assert.Empty(again.Migrated);
            Assert.Null(again.BackupFolder);
            Assert.True(again.IsComplete);
            Assert.Equal(once, File.ReadAllBytes(path));
            Assert.Single(Directory.GetDirectories(Path.Combine(_folder, KeycutMigration.BackupFolderName)));
        }

        [Theory]
        [InlineData("\r\n", false)]
        [InlineData("\n", false)]
        [InlineData("\r\n", true)]
        public void LineBreaksAndABom_Survive(string newline, bool bom)
        {
            var path = Path.Combine(_folder, "dl.bat");
            Write("dl.bat", OldFolderKeycut(path, newline: newline), bom);

            KeycutMigration.Run(_folder, Now);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(bom, bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            Assert.Equal(newline == "\r\n" ? 8 : 0, text.Split("\r\n").Length - 1);
            Assert.Contains("START \"\" /B \"%SystemRoot%\\explorer.exe\"", text);
        }

        [Fact]
        public void OnlyTheActiveExplorerLine_Changes_NotACommentedOutCopy()
        {
            var path = Write("s.bat", "@ECHO OFF\r\nREM \"%SystemRoot%\\explorer.exe\" \"C:\\Old\"\r\n\"%SystemRoot%\\explorer.exe\" \"C:\\Shortcuts\"\r\nEXIT\r\n");

            KeycutMigration.Run(_folder, Now);

            Assert.Equal("@ECHO OFF\r\nREM \"%SystemRoot%\\explorer.exe\" \"C:\\Old\"\r\nSTART \"\" /B \"%SystemRoot%\\explorer.exe\" \"C:\\Shortcuts\"\r\nEXIT\r\n", File.ReadAllText(path));
        }

        [Theory]
        [InlineData("@ECHO OFF\r\nREM \"%SystemRoot%\\explorer.exe\" \"C:\\Old\"\r\nEXIT\r\n")]          // only a commented-out line
        [InlineData("@ECHO OFF\r\n\"%SystemRoot%\\explorer.exe\" %SSFOLDER%\r\nEXIT\r\n")]              // unquoted argument
        [InlineData("@ECHO OFF\r\n\"%SystemRoot%\\explorer.exe\" \"a\" \"b\"\r\nEXIT\r\n")]             // two arguments
        [InlineData("@ECHO OFF\r\nSTART \"\" /B \"%SystemRoot%\\explorer.exe\" \"C:\\x\"\r\nEXIT\r\n")]  // already migrated
        [InlineData("@ECHO OFF\r\nREM <target>https://a.com</target>\r\nSTART https://a.com\r\nEXIT\r\n")]
        public void FilesThatArentTheOldForm_AreLeftAlone(string text)
        {
            var path = Write("x.bat", text);

            var result = KeycutMigration.Run(_folder, Now);

            Assert.Empty(result.Migrated);
            Assert.Equal(text, File.ReadAllText(path));
            Assert.False(Directory.Exists(Path.Combine(_folder, KeycutMigration.BackupFolderName)));
        }

        [Theory]
        [InlineData(@"::{645FF040-5081-101B-9F08-00AA002F954E}", ShortcutType.CLSIDKey)]
        [InlineData(@"::{26EE0668-A00A-44D7-9371-BEB064C98683}\0\::{7B81BE6A-CE2B-4676-A29E-EB907A5126C5}", ShortcutType.CLSIDKey)]
        [InlineData(@"\\fileserver\share\Team Docs", ShortcutType.Folder)]
        [InlineData(@"C:\Path With (Parens) & Ampersand", ShortcutType.Folder)]
        public void TheTarget_IsNeverTouched_AndParsesTheSame(string target, ShortcutType type)
        {
            var path = Path.Combine(_folder, "t.bat");
            Write("t.bat", OldFolderKeycut(path, target).Replace("<type>folder</type>", "<type>" + (type == ShortcutType.CLSIDKey ? "clsidkey" : "folder") + "</type>"));
            var before = new ShortcutFile(path);

            KeycutMigration.Run(_folder, Now);

            var after = new ShortcutFile(path);
            Assert.Equal((before.Type, before.Target), (after.Type, after.Target));
            Assert.Contains($"START \"\" /B \"%SystemRoot%\\explorer.exe\" \"{target}\"", File.ReadAllText(path));
        }

        [Fact]
        public void AnEmitterFormFolderKeycut_IsEditableInTheForm_AfterItsMigrated()
        {
            // As the emitter writes a Folder keycut now, then put back to the old tag and launch line
            var shortcut = new Shortcut(@"C:\Users", "users.bat", _folder);
            new Runner().CreateShortcutFile(shortcut, true, "");
            var path = shortcut.FullPath;
            var current = File.ReadAllText(path);
            File.WriteAllText(path, current.Replace("<target>", "<destination>").Replace("</target>", "</destination>").Replace("START \"\" /B \"%SystemRoot%", "\"%SystemRoot%"));

            KeycutMigration.Run(_folder, Now);

            Assert.Equal(current, File.ReadAllText(path));
            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void ANonUtf8File_IsLeftAsItIs_AndDoesntKeepTheFolderFromBeingDone()
        {
            var path = Path.Combine(_folder, "cafe.bat");
            var bytes = Encoding.Latin1.GetBytes("@ECHO OFF\r\nREM <destination>C:\\caf\u00e9</destination>\r\n\"%SystemRoot%\\explorer.exe\" \"C:\\caf\u00e9\"\r\nEXIT\r\n");
            File.WriteAllBytes(path, bytes);

            var result = KeycutMigration.Run(_folder, Now);

            Assert.Equal(new[] { "cafe.bat" }, result.NotUtf8);
            Assert.Empty(result.Migrated);
            Assert.True(result.IsComplete);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Fact]
        public void AReadOnlyFile_IsSkipped_AndTheFolderIsntDone_SoTheNextStartTriesAgain()
        {
            var locked = Write("a.bat", OldFolderKeycut(Path.Combine(_folder, "a.bat")));
            var other = Write("b.bat", OldFolderKeycut(Path.Combine(_folder, "b.bat")));
            var original = File.ReadAllBytes(locked);
            File.SetAttributes(locked, FileAttributes.ReadOnly);
            var done = new List<string>();

            var result = KeycutMigration.RunOnce(_folder, Now, done.Contains, done.Add);

            Assert.Equal(new[] { "a.bat" }, result.Skipped);
            Assert.Equal(new[] { "b.bat" }, result.Migrated);
            Assert.Equal(original, File.ReadAllBytes(locked));
            Assert.Empty(done);

            File.SetAttributes(locked, FileAttributes.Normal);
            var retry = KeycutMigration.RunOnce(_folder, Now.AddDays(1), done.Contains, done.Add);
            Assert.Equal(new[] { "a.bat" }, retry.Migrated);
            Assert.Equal(new[] { _folder }, done);
        }

        [Fact]
        public void AFolderAlreadyDone_IsntRead()
        {
            var path = Write("dl.bat", OldFolderKeycut(Path.Combine(_folder, "dl.bat")));
            var before = File.ReadAllBytes(path);

            var result = KeycutMigration.RunOnce(_folder, Now, folder => true, folder => throw new InvalidOperationException());

            Assert.Empty(result.Migrated);
            Assert.Equal(before, File.ReadAllBytes(path));
        }

        [Fact]
        public void TheCorpus_ParsesTheSame_AfterMigrating()
        {
            var corpus = Path.Combine(FindRepoRoot(), "tests", "corpus");
            foreach (var file in Directory.EnumerateFiles(corpus, "*.bat", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(_folder, Path.GetFileName(file)), true);
            }

            var before = Directory.GetFiles(_folder, "*.bat").ToDictionary(f => f, f => new ShortcutFile(f));

            var result = KeycutMigration.Run(_folder, Now);

            Assert.NotEmpty(result.Migrated);
            Assert.True(result.TagsUpdated > 0 && result.LaunchLinesUpdated > 0);
            foreach (var (file, parsed) in before)
            {
                var after = new ShortcutFile(file);
                Assert.Equal((parsed.Type, parsed.Target, parsed.Description), (after.Type, after.Target, after.Description));
            }

            Assert.Empty(KeycutMigration.Run(_folder, Now.AddMinutes(1)).Migrated);
        }

        [Fact]
        public void TheNotice_SaysHowManyAndWhereTheBackupIs()
        {
            var result = new MigrationResult { BackupFolder = @"C:\Shortcuts\.keycuts-backup\20261117-153000", LaunchLinesUpdated = 2, TagsUpdated = 3 };
            result.Migrated.AddRange(new[] { "a.bat", "b.bat", "c.bat" });

            var text = MigrationNotice.Format(result);

            Assert.StartsWith("keycuts updated 3 keycuts written by an older version", text);
            Assert.Contains("Folder keycuts among them now close their console window sooner.", text);
            Assert.EndsWith(@"C:\Shortcuts\.keycuts-backup\20261117-153000", text);
            Assert.Null(MigrationNotice.Format(new MigrationResult()));
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
    }
}
