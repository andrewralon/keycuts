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
            var lines = new List<string> { "@ECHO OFF", "REM keycuts 1.0", $"REM <type>url</type>", $"REM <destination>{url}</destination>" };
            if (descriptionLine != null)
            {
                lines.Add(descriptionLine);
            }
            lines.Add($"START {url}");
            lines.Add("EXIT");
            return WriteBat(name, lines.ToArray());
        }

        private string CreateWithRunner(string name, string destination, string description = "")
        {
            var shortcut = new Shortcut(destination, name, _folder);
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
            Assert.Equal(targetFolder, all["afolder"].Destination);
            Assert.Equal(KeycutKind.File, all["afile"].Kind);
            Assert.Equal(targetFile, all["afile"].Destination);
            Assert.Equal(KeycutKind.Command, all["cmd"].Kind);
            Assert.Equal(Path.Combine(_folder, "site.bat"), all["site"].FilePath);
            Assert.All(all.Values, k => Assert.Equal(DestinationStatus.Unknown, k.DestinationStatus));
        }

        [Fact]
        public async Task LoadAll_OldFormatFile_HasNoQuotes()
        {
            WriteBat("npp", "@ECHO OFF", "START \"\" /B \"C:\\Program Files\\npp.exe\"", "EXIT");

            Assert.Equal(@"C:\Program Files\npp.exe", (await Repo().LoadAllAsync()).Single().Destination);
        }

        [Theory]
        [InlineData("START \"\" /B \"cmd.exe\" /k echo hi", "\"cmd.exe\" /k echo hi")]
        [InlineData("START tool.exe -Command \"& 'x.ps1'\"", "tool.exe -Command \"& 'x.ps1'\"")]
        public async Task LoadAll_QuotesThatBelongToTheCommand_AreKept(string launchLine, string expected)
        {
            WriteBat("c", "@ECHO OFF", launchLine, "EXIT");

            Assert.Equal(expected, (await Repo().LoadAllAsync()).Single().Destination);
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
            Assert.Equal(target, k.Destination);
            Assert.Contains("explorer.exe", k.Description);
            Assert.DoesNotContain("wrong", k.Destination);
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
        public async Task CheckDestinations_MarksMissingAndOk_AndSkipsUrlsAndCommands()
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

            await repo.CheckDestinationsAsync(all);

            var byName = all.ToDictionary(k => k.Name);
            Assert.Equal(DestinationStatus.Ok, byName["okfile"].DestinationStatus);
            Assert.Equal(DestinationStatus.Ok, byName["okdir"].DestinationStatus);
            Assert.Equal(DestinationStatus.Missing, byName["missfile"].DestinationStatus);
            Assert.Equal(DestinationStatus.Missing, byName["missdir"].DestinationStatus);
            Assert.Equal(DestinationStatus.Unknown, byName["site"].DestinationStatus);
            Assert.Equal(DestinationStatus.Unknown, byName["cmd"].DestinationStatus);
        }

        [Fact]
        public async Task CheckDestinations_ExpandsEnvironmentVariables()
        {
            var k = new Keycut
            {
                Name = "env",
                Kind = KeycutKind.Folder,
                RawType = ShortcutType.Folder,
                Destination = "%SystemRoot%"
            };

            await Repo().CheckDestinationsAsync(new[] { k });

            Assert.Equal(DestinationStatus.Ok, k.DestinationStatus);
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

            var sw = Stopwatch.StartNew();
            var all = await repo.LoadAllAsync();
            sw.Stop();

            Assert.Equal(100, all.Count);
            Assert.True(sw.ElapsedMilliseconds < 500, $"took {sw.ElapsedMilliseconds} ms");
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
