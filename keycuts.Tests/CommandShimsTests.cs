using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#248: keycuts.cmd and keypaste.cmd in the keycuts folder. Temp folders only.</summary>
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

            Assert.Equal(new[] { "keycuts.cmd", "keypaste.cmd" }, written.Select(Path.GetFileName));
            Assert.Equal(CommandShims.Bytes("keycuts"), File.ReadAllBytes(Path.Combine(_folder, "keycuts.cmd")));
            Assert.Equal(CommandShims.Bytes("keypaste"), File.ReadAllBytes(Path.Combine(_folder, "keypaste.cmd")));

            Assert.Empty(CommandShims.Ensure(_folder));
            Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        }

        [Fact]
        public void Ensure_PutsBackAShimThatWasChanged_OrMadeReadOnly()
        {
            CommandShims.Ensure(_folder);
            var path = Path.Combine(_folder, "keycuts.cmd");
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
            File.WriteAllText(Path.Combine(_other, "keypaste.cmd"), "@ECHO the user's own keypaste");

            var removed = CommandShims.RemoveFrom(_other);

            Assert.Equal(new[] { Path.Combine(_other, "keycuts.cmd") }, removed);
            Assert.True(File.Exists(Path.Combine(_other, "keypaste.cmd")));
        }

        [Fact]
        public void Move_PutsTheShimsInTheNewFolder_AndTakesThemOutOfTheOld()
        {
            CommandShims.Ensure(_other);

            CommandShims.Move(_other, _folder);

            Assert.Equal(2, Directory.GetFiles(_folder, "*.cmd").Length);
            Assert.Empty(Directory.GetFiles(_other, "*.cmd"));
        }

        [Fact]
        public void Move_ToTheSameFolder_KeepsThem()
        {
            CommandShims.Move(_folder, _folder + @"\");

            Assert.Equal(2, Directory.GetFiles(_folder, "*.cmd").Length);
        }

        [Theory]
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
            var path = Path.Combine(_folder, "keycuts.cmd");
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

        // In one folder Win+R runs keycuts.bat before keycuts.cmd (#209), so a keycut with the name would take the command over:
        // that is why the names stay refused and an existing one is renamed at startup
        [Fact]
        public void AKeycutNamedLikeAShim_WouldWinOverIt_SoTheNamesStayRefused()
        {
            var shim = Path.Combine(_folder, "keycuts.cmd");
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
