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
