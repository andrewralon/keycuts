using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class KeycutRepositoryTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;

        public KeycutRepositoryTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "out");
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private string WriteBat(string name, params string[] lines)
        {
            var path = Path.Combine(_folder, name + ".bat");
            File.WriteAllLines(path, lines);
            return path;
        }

        private string WriteUrl(string name, string url = "https://example.com", string descriptionLine = null)
        {
            var lines = new List<string> { "@ECHO OFF", "REM keycuts 1.0", $"REM <type>url</type>", $"REM <target>{url}</target>" };
            if (descriptionLine != null)
            {
                lines.Add(descriptionLine);
            }
            lines.Add($"START {url}");
            lines.Add("EXIT");
            return WriteBat(name, lines.ToArray());
        }

        private string CreateWithRunner(string name, string target, string description = "")
        {
            var shortcut = new Shortcut(target, name, _folder);
            new Runner().CreateShortcutFile(shortcut, true, description);
            return shortcut.FullPath;
        }

        private KeycutRepository Repo() => new KeycutRepository(_folder);

        [Fact]
        public async Task LoadAll_EmptyFolder_ReturnsEmpty()
        {
            Assert.Empty(await Repo().LoadAllAsync());
        }

        [Fact]
        public async Task LoadAll_MissingFolder_ReturnsEmptyAndDoesNotCreateIt()
        {
            var missing = Path.Combine(_root, "nope");

            var result = await new KeycutRepository(missing).LoadAllAsync();

            Assert.Empty(result);
            Assert.False(Directory.Exists(missing));
        }

        [Fact]
        public async Task LoadAll_MixOfKinds_ParsesEachOne()
        {
            var targetFile = Path.Combine(_root, "notes.txt");
            File.WriteAllText(targetFile, "x");
            var targetFolder = Path.Combine(_root, "target dir");
            Directory.CreateDirectory(targetFolder);

            WriteUrl("site");
            CreateWithRunner("afolder", targetFolder);
            CreateWithRunner("afile", targetFile);
            WriteBat("cmd", "@ECHO OFF", "START \"\" /B \"cmd.exe\" /k echo hi", "EXIT");

            var all = (await Repo().LoadAllAsync()).ToDictionary(k => k.Name);

            Assert.Equal(4, all.Count);
            Assert.Equal(KeycutKind.Url, all["site"].Kind);
            Assert.Equal(ShortcutType.Url, all["site"].RawType);
            Assert.Equal(KeycutKind.Folder, all["afolder"].Kind);
            Assert.Equal(targetFolder, all["afolder"].Target);
            Assert.Equal(KeycutKind.File, all["afile"].Kind);
            Assert.Equal(targetFile, all["afile"].Target);
            Assert.Equal(KeycutKind.Command, all["cmd"].Kind);
            Assert.Equal(Path.Combine(_folder, "site.bat"), all["site"].FilePath);
            Assert.All(all.Values, k => Assert.Equal(TargetStatus.Unknown, k.TargetStatus));
        }

        [Fact]
        public async Task LoadAll_SetsModifiedFromTheFilesLastWriteTime()
        {
            var older = WriteUrl("older");
            var newer = WriteUrl("newer");
            var olderTime = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Local);
            var newerTime = new DateTime(2026, 11, 10, 12, 30, 0, DateTimeKind.Local);
            File.SetLastWriteTime(older, olderTime);
            File.SetLastWriteTime(newer, newerTime);

            var all = (await Repo().LoadAllAsync()).ToDictionary(k => k.Name);

            Assert.Equal(olderTime, all["older"].Modified);
            Assert.Equal(newerTime, all["newer"].Modified);
        }

        [Fact]
        public async Task Rename_UpdatesModified_BecauseItRewritesTheFile()
        {
            // Known and accepted (#167): the date shown is the file's, and a rename rewrites the shortcut line
            var path = CreateWithRunner("before", "https://example.com");
            var old = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Local);
            File.SetLastWriteTime(path, old);
            var keycut = (await Repo().LoadAllAsync()).Single();
            Assert.Equal(old, keycut.Modified);

            var newPath = await Repo().RenameAsync(keycut, "after");

            Assert.True(File.GetLastWriteTime(newPath) > old);
            Assert.True((await Repo().LoadAllAsync()).Single().Modified > old);
        }

        [Fact]
        public async Task LoadAll_ReadsTheTargetTag_AndTheOlderDestinationTag()
        {
            // #195: a keycut made now carries REM <target>, one made before carries REM <destination>, and both load the
            // same way. The launch lines differ from the REM values, so only the tag can be where the target came from.
            WriteBat("fresh", "@ECHO OFF", "REM keycuts 1.3.0", "REM <type>url</type>", "REM <target>https://example.com/new</target>", "START https://example.com/launch", "EXIT");
            WriteBat("older", "@ECHO OFF", "REM keycuts 1.2.0", "REM <type>url</type>", "REM <destination>https://example.com/old</destination>", "START https://example.com/launch", "EXIT");

            var all = (await Repo().LoadAllAsync()).ToDictionary(k => k.Name);

            Assert.Equal("https://example.com/new", all["fresh"].Target);
            Assert.Equal("https://example.com/old", all["older"].Target);
            Assert.Equal(KeycutKind.Url, all["fresh"].Kind);
            Assert.Equal(KeycutKind.Url, all["older"].Kind);
        }

        [Fact]
        public async Task LoadAll_OldFormatFile_HasNoQuotes()
        {
            WriteBat("npp", "@ECHO OFF", "START \"\" /B \"C:\\Program Files\\npp.exe\"", "EXIT");

            Assert.Equal(@"C:\Program Files\npp.exe", (await Repo().LoadAllAsync()).Single().Target);
        }

        [Theory]
        [InlineData("START \"\" /B \"cmd.exe\" /k echo hi", "\"cmd.exe\" /k echo hi")]
        [InlineData("START tool.exe -Command \"& 'x.ps1'\"", "tool.exe -Command \"& 'x.ps1'\"")]
        public async Task LoadAll_QuotesThatBelongToTheCommand_AreKept(string launchLine, string expected)
        {
            WriteBat("c", "@ECHO OFF", launchLine, "EXIT");

            Assert.Equal(expected, (await Repo().LoadAllAsync()).Single().Target);
        }

        [Fact]
        public async Task LoadAll_UnparseableFile_IsSkipped()
        {
            WriteUrl("good");
            WriteBat("junk", "this is not a keycut", "nothing here");
            File.WriteAllText(Path.Combine(_folder, "empty.bat"), "");
            File.WriteAllText(Path.Combine(_folder, "other.txt"), "START https://x.com");

            var all = await Repo().LoadAllAsync();

            Assert.Equal(new[] { "good" }, all.Select(k => k.Name));
        }

        [Fact]
        public async Task LoadAll_FolderPathIsAFile_ReturnsEmpty()
        {
            var notAFolder = Path.Combine(_root, "file.txt");
            File.WriteAllText(notAFolder, "x");

            // A file path isn't a directory, so it behaves like a missing folder
            Assert.Empty(await new KeycutRepository(notAFolder).LoadAllAsync());
        }

        [Fact]
        public async Task LoadAll_InvalidPath_ThrowsTypedFailure()
        {
            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => new KeycutRepository(_folder + "\0bad").LoadAllAsync());

            Assert.Equal(KeycutRepositoryError.InvalidPath, ex.Error);
        }

        [Fact]
        public async Task Description_Present_IsTrimmed()
        {
            WriteUrl("a", descriptionLine: "REM <description>  Work portal  </description>");

            Assert.Equal("Work portal", (await Repo().LoadAllAsync()).Single().Description);
        }

        [Fact]
        public async Task Description_CaseInsensitive()
        {
            WriteUrl("a", descriptionLine: "rem <DESCRIPTION>Hello</Description>");

            Assert.Equal("Hello", (await Repo().LoadAllAsync()).Single().Description);
        }

        [Fact]
        public async Task Description_Empty_IsEmpty()
        {
            WriteUrl("a", descriptionLine: "REM <description></description>");

            Assert.Equal("", (await Repo().LoadAllAsync()).Single().Description);
        }

        [Fact]
        public async Task Description_Absent_IsEmpty()
        {
            WriteUrl("a");

            Assert.Equal("", (await Repo().LoadAllAsync()).Single().Description);
        }

        [Theory]
        [InlineData("REM <description>no closing tag")]
        [InlineData("REM <description>")]
        [InlineData("REM description text")]
        [InlineData("REM <description>x</description> trailing junk")]
        public async Task Description_Malformed_IsEmptyAndStillLoads(string line)
        {
            WriteUrl("a", descriptionLine: line);

            var all = await Repo().LoadAllAsync();

            Assert.Equal("", all.Single().Description);
            Assert.Equal(ShortcutType.Url, all.Single().RawType);
        }

        [Fact]
        public async Task Description_MentioningExplorer_IsNotMistakenForTheCommand()
        {
            var target = Path.Combine(_root, "dir");
            Directory.CreateDirectory(target);
            var path = CreateWithRunner("f", target, "opens via \"%SystemRoot%\\explorer.exe\" \"C:\\wrong\"");

            var k = (await Repo().LoadAllAsync()).Single();

            Assert.Equal(KeycutKind.Folder, k.Kind);
            Assert.Equal(target, k.Target);
            Assert.Contains("explorer.exe", k.Description);
            Assert.DoesNotContain("wrong", k.Target);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task RunnerCreatedFile_HasEmptyDescriptionPlaceholderByDefault()
        {
            var path = CreateWithRunner("p", "https://example.com");

            Assert.Contains("REM <description></description>", File.ReadAllLines(path));
            Assert.Equal("", (await Repo().LoadAllAsync()).Single().Description);
        }

        [Fact]
        public async Task RunnerCreatedFile_DescriptionRoundTripsAndNewlinesAreFlattened()
        {
            CreateWithRunner("p", "https://example.com", "line one\r\nline two");

            Assert.Equal("line one  line two", (await Repo().LoadAllAsync()).Single().Description);
        }

        [Fact]
        public void ParseDescription_NoLines_IsEmpty()
        {
            Assert.Equal("", ShortcutFile.ParseDescription(Array.Empty<string>()));
        }

        [Fact]
        public async Task Delete_RemovesFile()
        {
            var path = WriteUrl("gone");
            var k = (await Repo().LoadAllAsync()).Single();

            await Repo().DeleteAsync(k);

            Assert.False(File.Exists(path));
        }

        [Fact]
        public async Task Delete_AlreadyGone_ThrowsNotFound()
        {
            var path = WriteUrl("gone");
            var k = (await Repo().LoadAllAsync()).Single();
            File.Delete(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().DeleteAsync(k));

            Assert.Equal(KeycutRepositoryError.NotFound, ex.Error);
        }

        [Fact]
        public async Task Delete_LockedFile_ThrowsTypedFailure()
        {
            var path = WriteUrl("locked");
            var k = (await Repo().LoadAllAsync()).Single();

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().DeleteAsync(k));

                Assert.True(ex.Error == KeycutRepositoryError.InUse || ex.Error == KeycutRepositoryError.AccessDenied);
            }

            Assert.True(File.Exists(path));
        }

        #region Recycle and restore (#179)

        private sealed class FakeRecycler : IFileRecycler
        {
            public List<string> Removed { get; } = new List<string>();

            public bool InBin { get; set; } = true;

            public Exception Error { get; set; }

            public void Remove(string path)
            {
                if (Error != null)
                {
                    throw Error;
                }

                Removed.Add(path);
                File.Delete(path);
            }

            public bool UsesRecycleBin(string path)
            {
                return InBin;
            }
        }

        // Hand-edited and odd on purpose: an old <destination> tag, an added %*, CRLF and LF mixed, a BOM and a byte that isn't UTF-8
        private static readonly byte[] AwkwardBytes =
            new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(System.Text.Encoding.UTF8.GetBytes("@ECHO OFF\r\nREM keycuts 1.2.0\r\nREM <destination>C:\\old tags\\a.exe</destination>\nREM <type>file</type>\r\nSTART \"\" /B \"C:\\old tags\\a.exe\" %*\r\nEXIT\r\n"))
                .Concat(new byte[] { 0xFF, 0xFE })
                .ToArray();

        [Fact]
        public async Task Delete_ReturnsWhatARestoreNeeds_AndRemovesTheFile()
        {
            var path = Path.Combine(_folder, "awkward.bat");
            File.WriteAllBytes(path, AwkwardBytes);
            var time = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Local);
            File.SetLastWriteTime(path, time);
            var keycut = new Keycut { Name = "awkward", FilePath = path, Target = @"C:\old tags\a.exe" };

            var deleted = await new KeycutRepository(_folder, new FakeRecycler()).DeleteAsync(keycut);

            Assert.False(File.Exists(path));
            Assert.Same(keycut, deleted.Keycut);
            Assert.Equal(AwkwardBytes, deleted.Contents);
            Assert.Equal(time, deleted.Modified);
            Assert.True(deleted.Recycled);
        }

        [Fact]
        public async Task Delete_GoesThroughTheRecycler_AndSaysWhenThereIsNoBin()
        {
            var path = WriteUrl("viabin");
            var recycler = new FakeRecycler { InBin = false };
            var repository = new KeycutRepository(_folder, recycler);
            var keycut = (await repository.LoadAllAsync()).Single();

            var deleted = await repository.DeleteAsync(keycut);

            Assert.Equal(new[] { path }, recycler.Removed);
            Assert.False(deleted.Recycled);
        }

        [Fact]
        public async Task Delete_ARecyclerThatFails_LeavesTheFileAndThrowsATypedError()
        {
            var path = WriteUrl("stays");
            var recycler = new FakeRecycler { Error = new UnauthorizedAccessException("denied") };
            var repository = new KeycutRepository(_folder, recycler);
            var keycut = (await repository.LoadAllAsync()).Single();

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => repository.DeleteAsync(keycut));

            Assert.Equal(KeycutRepositoryError.AccessDenied, ex.Error);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task Restore_PutsTheExactBytesAndTheWriteTimeBack_WithoutLeavingATempFile()
        {
            var path = Path.Combine(_folder, "awkward.bat");
            File.WriteAllBytes(path, AwkwardBytes);
            var time = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Local);
            File.SetLastWriteTime(path, time);
            var repository = new KeycutRepository(_folder, new FakeRecycler());
            var deleted = await repository.DeleteAsync(new Keycut { Name = "awkward", FilePath = path });
            Assert.False(File.Exists(path));

            await repository.RestoreAsync(deleted);

            Assert.Equal(AwkwardBytes, File.ReadAllBytes(path));
            Assert.Equal(time, File.GetLastWriteTime(path));
            Assert.Equal(new[] { path }, Directory.GetFileSystemEntries(_folder));
        }

        [Fact]
        public async Task Restore_ADeletedKeycutLoadsAgainAsItWas()
        {
            CreateWithRunner("kept", "https://example.com/kept", "Team wiki");
            var repository = new KeycutRepository(_folder, new FakeRecycler());
            var before = (await repository.LoadAllAsync()).Single();

            var deleted = await repository.DeleteAsync(before);
            Assert.Empty(await repository.LoadAllAsync());
            await repository.RestoreAsync(deleted);

            var after = (await repository.LoadAllAsync()).Single();
            Assert.Equal(before.Name, after.Name);
            Assert.Equal(before.Target, after.Target);
            Assert.Equal(before.Description, after.Description);
            Assert.Equal(before.Modified, after.Modified);
        }

        [Fact]
        public async Task Restore_ANameTakenAgain_IsRefused_AndTheNewFileIsKeptByteForByte()
        {
            var path = WriteUrl("taken", "https://example.com/original");
            var repository = new KeycutRepository(_folder, new FakeRecycler());
            var deleted = await repository.DeleteAsync((await repository.LoadAllAsync()).Single());

            // A new keycut with the same name, made before the undo
            var replacement = WriteUrl("taken", "https://example.com/replacement");
            var replacementBytes = File.ReadAllBytes(replacement);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => repository.RestoreAsync(deleted));

            Assert.Equal(KeycutRepositoryError.AlreadyExists, ex.Error);
            Assert.Equal(replacementBytes, File.ReadAllBytes(path));
            Assert.Equal(new[] { path }, Directory.GetFileSystemEntries(_folder));
        }

        [Fact]
        public async Task Restore_ANameTakenInADifferentCase_IsRefusedToo()
        {
            WriteUrl("Mixed");
            var repository = new KeycutRepository(_folder, new FakeRecycler());
            var deleted = await repository.DeleteAsync((await repository.LoadAllAsync()).Single());
            var newer = Path.Combine(_folder, "mixed.bat");
            File.WriteAllText(newer, "@ECHO OFF\r\n");

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => repository.RestoreAsync(deleted));

            Assert.Equal(KeycutRepositoryError.AlreadyExists, ex.Error);
            Assert.Equal("@ECHO OFF\r\n", File.ReadAllText(newer));
            Assert.Single(Directory.GetFileSystemEntries(_folder));
        }

        [Fact]
        public async Task Restore_AFolderThatIsGone_ThrowsNotFound()
        {
            var path = WriteUrl("lost");
            var repository = new KeycutRepository(_folder, new FakeRecycler());
            var deleted = await repository.DeleteAsync((await repository.LoadAllAsync()).Single());
            Directory.Delete(_folder, true);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => repository.RestoreAsync(deleted));

            Assert.Equal(KeycutRepositoryError.NotFound, ex.Error);
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void TheRecycleBin_IsOnlyUsedOnALocalDrive()
        {
            // A UNC path is never a drive with a bin, and this one needs no network to know it
            Assert.False(RecycleBinRecycler.HasRecycleBin(@"\\fileserver\share\keycuts\gmail.bat"));
            Assert.False(new RecycleBinRecycler().UsesRecycleBin(@"\\fileserver\share\keycuts\gmail.bat"));
            Assert.False(new PermanentDeleter().UsesRecycleBin(Path.Combine(_folder, "x.bat")));
        }

        [Fact]
        public async Task ThePlainConstructorDeletesForGood_AndOnlyCreateDefaultUsesTheBin()
        {
            // So a test that deletes can't put files in the user's Recycle Bin
            var path = WriteUrl("plain");
            var keycut = new Keycut { Name = "plain", FilePath = path };

            var deleted = await new KeycutRepository(_folder).DeleteAsync(keycut);

            Assert.False(deleted.Recycled);
            Assert.False(File.Exists(path));
        }

        #endregion Recycle and restore (#179)

        [Fact]
        public async Task Rename_MovesTheFile_AndRewritesOnlyTheShortcutLine()
        {
            var oldPath = Path.Combine(_folder, "kp.bat");
            var newPath = Path.Combine(_folder, "paste.bat");
            File.WriteAllText(oldPath,
                "@ECHO OFF\r\n" +
                "REM keycuts 1.0.6\r\n" +
                $"REM <shortcut>{oldPath}</shortcut>\r\n" +
                "REM <type>file</type>\r\n" +
                "REM <destination>C:\\tools\\keypaste.exe</destination>\r\n" +
                "START \"\" /B \"C:\\tools\\keypaste.exe\" %*\r\n" +
                "EXIT\r\n");
            var k = (await Repo().LoadAllAsync()).Single();

            var result = await Repo().RenameAsync(k, "paste");

            Assert.Equal(newPath, result);
            Assert.False(File.Exists(oldPath));
            Assert.Equal(new[] { newPath }, Directory.GetFiles(_folder));
            Assert.Equal(
                "@ECHO OFF\r\n" +
                "REM keycuts 1.0.6\r\n" +
                $"REM <shortcut>{newPath}</shortcut>\r\n" +
                "REM <type>file</type>\r\n" +
                "REM <destination>C:\\tools\\keypaste.exe</destination>\r\n" +
                "START \"\" /B \"C:\\tools\\keypaste.exe\" %*\r\n" +
                "EXIT\r\n",
                File.ReadAllText(newPath));
        }

        [Fact]
        public async Task Rename_KeepsTheDownloadedFromTheInternetMark()
        {
            // A Zone.Identifier stream makes Windows warn before running a downloaded .bat; rewriting must not drop it
            var oldPath = Path.Combine(_folder, "dl.bat");
            File.WriteAllText(oldPath, $"REM <shortcut>{oldPath}</shortcut>\r\nSTART https://example.com\r\n");
            File.WriteAllText(oldPath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            var k = (await Repo().LoadAllAsync()).Single();

            var newPath = await Repo().RenameAsync(k, "web");

            Assert.Contains($"<shortcut>{newPath}</shortcut>", File.ReadAllText(newPath));
            Assert.Equal("[ZoneTransfer]\r\nZoneId=3\r\n", File.ReadAllText(newPath + ":Zone.Identifier"));
            Assert.Equal(new[] { newPath }, Directory.GetFiles(_folder));
        }

        [Fact]
        public async Task Rename_KeepsAUtf8ByteOrderMark()
        {
            var oldPath = Path.Combine(_folder, "a.bat");
            File.WriteAllText(oldPath, $"REM <shortcut>{oldPath}</shortcut>\r\nSTART https://example.com\r\n", new System.Text.UTF8Encoding(true));
            var k = (await Repo().LoadAllAsync()).Single();

            var newPath = await Repo().RenameAsync(k, "b");

            var bytes = File.ReadAllBytes(newPath);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Contains($"<shortcut>{newPath}</shortcut>", File.ReadAllText(newPath));
        }

        [Fact]
        public async Task Rename_LegacyFileWithoutAShortcutLine_IsJustMoved()
        {
            var oldPath = WriteBat("gm", "@ECHO OFF", @"REM keycuts 0.1.1.5", @"REM <url>C:\Shortcuts\gm.bat</url>", "START https://mail.google.com", "EXIT");
            var before = File.ReadAllBytes(oldPath);
            var k = (await Repo().LoadAllAsync()).Single();

            var newPath = await Repo().RenameAsync(k, "mail");

            Assert.Equal(before, File.ReadAllBytes(newPath));
        }

        [Fact]
        public async Task Rename_NonUtf8File_IsMovedWithItsBytesUntouched()
        {
            var oldPath = Path.Combine(_folder, "old.bat");
            var before = new byte[] { 0x52, 0x45, 0x4D, 0x20, 0x3C, 0x73, 0x68, 0x6F, 0x72, 0x74, 0x63, 0x75, 0x74, 0x3E, 0xE9, 0x3C, 0x2F, 0x73, 0x68, 0x6F, 0x72, 0x74, 0x63, 0x75, 0x74, 0x3E, 0x0D, 0x0A, 0x53, 0x54, 0x41, 0x52, 0x54, 0x20, 0x68, 0x74, 0x74, 0x70, 0x73, 0x3A, 0x2F, 0x2F, 0x61, 0x2E, 0x62, 0x0D, 0x0A };
            File.WriteAllBytes(oldPath, before);
            var k = (await Repo().LoadAllAsync()).Single();

            var newPath = await Repo().RenameAsync(k, "new");

            Assert.Equal(before, File.ReadAllBytes(newPath));
        }

        [Fact]
        public async Task Rename_ToATakenName_IsRefused_AndChangesNothing()
        {
            var a = WriteUrl("a");
            var b = WriteUrl("b", "https://b.example");
            var k = (await Repo().LoadAllAsync()).Single(x => x.Name == "a");

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().RenameAsync(k, "B"));

            Assert.Equal(KeycutRepositoryError.AlreadyExists, ex.Error);
            Assert.True(File.Exists(a));
            Assert.Contains("https://b.example", File.ReadAllText(b));
        }

        [Fact]
        public async Task Rename_ChangingOnlyTheCase_IsAllowed()
        {
            WriteUrl("npp");
            var k = (await Repo().LoadAllAsync()).Single();

            var newPath = await Repo().RenameAsync(k, "NPP");

            Assert.Equal("NPP.bat", Path.GetFileName(Directory.GetFiles(_folder).Single()));
            Assert.Equal(Path.Combine(_folder, "NPP.bat"), newPath);
        }

        [Theory]
        [InlineData("")]
        [InlineData("a/b")]
        [InlineData("con")]
        [InlineData(" padded")]
        public async Task Rename_InvalidName_IsRefused(string name)
        {
            var path = WriteUrl("a");
            var k = (await Repo().LoadAllAsync()).Single();

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().RenameAsync(k, name));

            Assert.Equal(KeycutRepositoryError.InvalidName, ex.Error);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task Rename_FileAlreadyGone_ThrowsNotFound()
        {
            var path = WriteUrl("a");
            var k = (await Repo().LoadAllAsync()).Single();
            File.Delete(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().RenameAsync(k, "b"));

            Assert.Equal(KeycutRepositoryError.NotFound, ex.Error);
        }

        [Fact]
        public async Task Rename_SameName_IsANoOp()
        {
            var path = WriteUrl("a");
            var k = (await Repo().LoadAllAsync()).Single();

            Assert.Equal(path, await Repo().RenameAsync(k, "a"));
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void Exists_IsCaseInsensitive()
        {
            WriteUrl("MyKeycut");
            var repo = Repo();

            Assert.True(repo.Exists("MyKeycut"));
            Assert.True(repo.Exists("mykeycut"));
            Assert.True(repo.Exists("MYKEYCUT"));
            Assert.False(repo.Exists("other"));
            Assert.False(repo.Exists(""));
        }

        [Fact]
        public void Exists_MissingFolder_IsFalse()
        {
            Assert.False(new KeycutRepository(Path.Combine(_root, "nope")).Exists("x"));
        }

        [Fact]
        public async Task Count_MatchesLoadedKeycuts()
        {
            WriteUrl("a");
            WriteUrl("b");
            WriteBat("junk", "nothing");

            Assert.Equal(2, await Repo().CountAsync());
        }

        [Fact]
        public async Task CheckTargets_MarksMissingAndOk_AndSkipsUrlsAndCommands()
        {
            var existing = Path.Combine(_root, "here.txt");
            File.WriteAllText(existing, "x");
            var existingDir = Path.Combine(_root, "hereDir");
            Directory.CreateDirectory(existingDir);
            var gone = Path.Combine(_root, "gone.txt");
            File.WriteAllText(gone, "x");
            var goneDir = Path.Combine(_root, "goneDir");
            Directory.CreateDirectory(goneDir);

            CreateWithRunner("okfile", existing);
            CreateWithRunner("okdir", existingDir);
            CreateWithRunner("missfile", gone);
            CreateWithRunner("missdir", goneDir);
            WriteUrl("site");
            WriteBat("cmd", "@ECHO OFF", "START \"\" /B \"cmd.exe\" /k echo hi", "EXIT");

            var repo = Repo();
            var all = (await repo.LoadAllAsync()).ToList();

            File.Delete(gone);
            Directory.Delete(goneDir);

            await repo.CheckTargetsAsync(all);

            var byName = all.ToDictionary(k => k.Name);
            Assert.Equal(TargetStatus.Ok, byName["okfile"].TargetStatus);
            Assert.Equal(TargetStatus.Ok, byName["okdir"].TargetStatus);
            Assert.Equal(TargetStatus.Missing, byName["missfile"].TargetStatus);
            Assert.Equal(TargetStatus.Missing, byName["missdir"].TargetStatus);
            Assert.Equal(TargetStatus.Unknown, byName["site"].TargetStatus);
            Assert.Equal(TargetStatus.Unknown, byName["cmd"].TargetStatus);
        }

        [Fact]
        public async Task CheckTargets_ExpandsEnvironmentVariables()
        {
            var k = new Keycut
            {
                Name = "env",
                Kind = KeycutKind.Folder,
                RawType = ShortcutType.Folder,
                Target = "%SystemRoot%"
            };

            await Repo().CheckTargetsAsync(new[] { k });

            Assert.Equal(TargetStatus.Ok, k.TargetStatus);
        }

        [Fact]
        public async Task LoadAll_HundredFiles_LoadsQuickly()
        {
            for (var i = 0; i < 100; i++)
            {
                WriteUrl("k" + i, "https://example.com/" + i);
            }

            var repo = Repo();
            await repo.LoadAllAsync(); // warm up JIT/regex

            // Judge the fastest of three loads: one stall on a busy CI runner
            // (612 ms once, with the other tests running in parallel) shouldn't
            // fail it, but a load that is slow every time still does.
            var timings = new List<long>();
            for (var run = 0; run < 3; run++)
            {
                var sw = Stopwatch.StartNew();
                var all = await repo.LoadAllAsync();
                sw.Stop();

                Assert.Equal(100, all.Count);
                timings.Add(sw.ElapsedMilliseconds);
            }

            Assert.True(timings.Min() < 500, $"took {string.Join(", ", timings)} ms");
        }

        [Fact]
        public async Task LoadAll_Cancelled_Throws()
        {
            WriteUrl("a");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repo().LoadAllAsync(cts.Token));
        }
    }
}
