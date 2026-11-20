using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The repository on macOS, where keycuts are <see cref="ShFormat"/> scripts with no extension (docs/macos-port.md, "A keycut
    /// on macOS"): the twins of the <c>.bat</c> tests that are Windows only. A temp folder and a permanent deleter only.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public class ShKeycutRepositoryTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _folder;

        public ShKeycutRepositoryTests()
        {
            _folder = Path.Combine(_root, "Shortcuts");
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_root, true);
        }

        private string Create(string name, string target = "https://example.com", string description = "")
        {
            var shortcut = new Shortcut(target, name, _folder);
            new Runner().CreateShortcutFile(shortcut, true, description);
            return shortcut.FullPath;
        }

        private KeycutRepository Repository => new KeycutRepository(_folder, new PermanentDeleter());

        private string Write(string name, params string[] lines)
        {
            var path = Path.Combine(_folder, name);
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            return path;
        }

        [MacFact]
        public async Task TheList_HasOnlyScriptsWithKeycutsHeader_NotTheShimsOrOtherScripts()
        {
            Create("gmail");
            Create("notes.v2");
            Write("keycuts", ShFormat.Shebang, "# The keycuts command: runs keycuts from the folder the app recorded.", "exec x \"$@\"");
            Write("backup", ShFormat.Shebang, "rsync -a ~/x ~/y");
            Write("headerless", "open 'https://example.com'");
            Write(".DS_Store", "\0\0junk");

            var all = await Repository.LoadAllAsync();

            Assert.Equal(new[] { "gmail", "notes.v2" }, all.Select(k => k.Name));
            Assert.All(all, k => Assert.True(k.CanEditInForm));
            Assert.All(all, k => Assert.True(k.CountsRuns));
        }

        [MacFact]
        public void Find_LeavesOtherScriptsAlone()
        {
            Write("backup", ShFormat.Shebang, "rsync -a ~/x ~/y");

            Create("gmail");

            Assert.Null(Repository.Find("backup"));
            Assert.Equal("gmail", Repository.Find("GMAIL").Name);
        }

        [MacFact]
        public async Task Rename_MovesTheFile_AndRewritesOnlyTheShortcutLine()
        {
            var path = Create("mail");
            var before = File.ReadAllLines(path);
            var keycut = Repository.Find("mail");

            var newPath = await Repository.RenameAsync(keycut, "gmail");

            Assert.False(File.Exists(path));
            Assert.Equal(Path.Combine(_folder, "gmail"), newPath);
            var after = File.ReadAllLines(newPath);
            Assert.Equal($"# <shortcut>{newPath}</shortcut>", after[2]);
            Assert.Equal(before.Where((_, i) => i != 2), after.Where((_, i) => i != 2));
        }

        [MacFact]
        public async Task SetDescription_RewritesOnlyItsLine_WithNothingEscaped()
        {
            var path = Create("mail", description: "old");
            var before = File.ReadAllLines(path);

            var keycut = await Repository.SetDescriptionAsync(Repository.Find("mail"), "100% $HOME `x`");

            Assert.Equal("100% $HOME `x`", keycut.Description);
            var after = File.ReadAllLines(path);
            Assert.Equal("# <description>100% $HOME `x`</description>", after[5]);
            Assert.Equal(before.Where((_, i) => i != 5), after.Where((_, i) => i != 5));
        }

        [MacFact]
        public async Task SetDescription_OnAFileWithNone_AddsTheLineBeforeTheLaunchLine()
        {
            var path = Write("mail", ShFormat.Shebang, "# keycuts 2.0.0", "# <type>url</type>", "# <target>https://example.com</target>", "open 'https://example.com'");

            await Repository.SetDescriptionAsync(Repository.Find("mail"), "Inbox");

            Assert.Equal("# <description>Inbox</description>", File.ReadAllLines(path)[4]);
            Assert.Equal("open 'https://example.com'", File.ReadAllLines(path)[5]);
        }

        [MacFact]
        public async Task TheEditForm_TakesWhatTheEmitterWrote_AndRefusesAHandEditedLaunchLine()
        {
            var path = Create("mail");
            Assert.True(KeycutRepository.CanEditInForm(path));

            var edited = await Repository.UpdateAsync(Repository.Find("mail"), new KeycutEdit("https://mail.google.com", "gmail", "Inbox"));

            Assert.Equal(("gmail", "https://mail.google.com", "Inbox"), (edited.Name, edited.Target, edited.Description));
            Assert.Contains("open 'https://mail.google.com'", File.ReadAllLines(edited.FilePath));
            Assert.False(File.Exists(path));

            File.WriteAllLines(edited.FilePath, File.ReadAllLines(edited.FilePath).Select(l => l.StartsWith("open ") ? l + " --args x" : l));
            Assert.False(KeycutRepository.CanEditInForm(edited.FilePath));
        }

        [MacFact]
        public async Task DeleteThenRestore_PutsTheSameBytesBack()
        {
            var path = Create("mail");
            var bytes = File.ReadAllBytes(path);

            var deleted = await Repository.DeleteAsync(Repository.Find("mail"));
            Assert.False(File.Exists(path));

            await Repository.RestoreAsync(deleted);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        /// <summary>A Trash of its own: moves the file into a folder and says where, as <see cref="TrashRecycler"/> does.</summary>
        private sealed class FolderBin : IFileRecycler
        {
            private readonly string _bin;

            public FolderBin(string bin)
            {
                _bin = bin;
                Directory.CreateDirectory(bin);
            }

            public string Remove(string path)
            {
                // The Trash renames a second item of the same name ("mail 2"), as this does
                var target = Path.Combine(_bin, Path.GetFileName(path));
                for (var i = 2; File.Exists(target); i++)
                {
                    target = Path.Combine(_bin, Path.GetFileName(path) + " " + i);
                }

                File.Move(path, target);
                return target;
            }

            public bool UsesRecycleBin(string path)
            {
                return true;
            }
        }

        [MacFact]
        public async Task Undo_MovesTheFileBackOutOfTheTrash_LikePutBack()
        {
            var bin = Path.Combine(_root, "Trash");
            var repository = new KeycutRepository(_folder, new FolderBin(bin));
            var path = Create("mail");
            var bytes = File.ReadAllBytes(path);

            var deleted = await repository.DeleteAsync(repository.Find("mail"));
            Assert.Equal(Path.Combine(bin, "mail"), deleted.BinPath);

            await repository.RestoreAsync(deleted);

            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Empty(Directory.GetFiles(bin));
        }

        [MacFact]
        public async Task Undo_WritesTheKeptBytes_WhenTheTrashedFileChangedOrWent_AndNeverOverwrites()
        {
            var bin = Path.Combine(_root, "Trash");
            var repository = new KeycutRepository(_folder, new FolderBin(bin));
            var path = Create("mail");
            var bytes = File.ReadAllBytes(path);

            var deleted = await repository.DeleteAsync(repository.Find("mail"));
            File.AppendAllText(deleted.BinPath, "changed in the Trash\n");
            await repository.RestoreAsync(deleted);

            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.True(File.Exists(deleted.BinPath));   // not ours any more: it stays in the Trash

            var again = await repository.DeleteAsync(repository.Find("mail"));
            Create("mail", "https://other.example.com");
            var error = await Assert.ThrowsAsync<KeycutRepositoryException>(() => repository.RestoreAsync(again));
            Assert.Equal(KeycutRepositoryError.AlreadyExists, error.Error);
            Assert.True(File.Exists(again.BinPath));
        }

        [MacFact]
        public void MoveAside_RenamesAKeycutNamedKeycuts_ButNotTheShimOfThatName()
        {
            var shim = Write("keycuts", ShFormat.Shebang, "# The keycuts command.", "exec x \"$@\"");
            Create("keypaste_ish");
            File.Move(Path.Combine(_folder, "keypaste_ish"), Path.Combine(_folder, "keypaste"));

            var renamed = Repository.MoveAsideCommandNames();

            Assert.Equal(new[] { ("keypaste", "keypaste_previous") }, renamed.Select(r => (r.OldName, r.NewName)));
            Assert.True(File.Exists(shim));
        }

        [MacFact]
        public void TheDefaultRecycler_IsTheTrash()
        {
            Assert.IsType<TrashRecycler>(KeycutRepository.DefaultRecycler());
        }

        // Never a real file: Finder's Trash is off limits to tests. A missing one goes through the same call and fails.
        [MacFact]
        public void TheTrash_RefusesAFileThatIsntThere_WithItsReason()
        {
            var error = Assert.Throws<IOException>(() => new TrashRecycler().Remove(Path.Combine(_folder, "missing")));
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }

        [MacFact]
        public void Paste_CopiesAKeycutsTarget_AndAnyOtherFilesText()
        {
            var keycut = Create("mail");
            var other = Write("snippet", "hello");

            Assert.Equal("https://example.com", KeycutPaste.GetText(keycut, File.ReadAllLines, File.ReadAllText, s => s));
            Assert.Equal("hello\n", KeycutPaste.GetText(other, File.ReadAllLines, File.ReadAllText, s => s));
        }
    }
}
