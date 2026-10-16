using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class KeycutCreatorTests : IDisposable
    {
        private class FakePathUpdater : IPathUpdater
        {
            public int Calls;
            public bool Result = true;

            public bool AddToOrReplace(string oldPath, string newPath)
            {
                Calls++;
                return Result;
            }
        }

        private readonly string _root;
        private readonly string _out;
        private readonly string _target;
        private readonly FakePathUpdater _path = new FakePathUpdater();

        public KeycutCreatorTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _out = Path.Combine(_root, "out");
            Directory.CreateDirectory(_root);
            _target = Path.Combine(_root, "target.txt");
            File.WriteAllText(_target, "x");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private CreateOptions Options(bool overwrite = false, string description = "")
        {
            return new CreateOptions { OutputFolder = _out, Overwrite = overwrite, PathUpdater = _path, Description = description };
        }

        [Fact]
        public async Task Success_WritesFileAndReturnsKeycut()
        {
            var result = await KeycutCreator.CreateAsync(_target, "mine", Options(description: "hello"));

            Assert.Equal(CreateOutcome.Success, result.Outcome);
            Assert.True(File.Exists(Path.Combine(_out, "mine.bat")));
            Assert.Equal("mine", result.Keycut.Name);
            Assert.Equal("hello", result.Keycut.Description);
            Assert.Equal(KeycutKind.File, result.Keycut.Kind);
        }

        [Fact]
        public async Task Success_CreatesMissingOutputFolder()
        {
            Assert.False(Directory.Exists(_out));

            var result = await KeycutCreator.CreateAsync(_target, "mine", Options());

            Assert.True(result.IsSuccess);
            Assert.True(Directory.Exists(_out));
        }

        [Fact]
        public async Task Url_Succeeds()
        {
            var result = await KeycutCreator.CreateAsync("https://example.com", "site", Options());

            Assert.True(result.IsSuccess);
            Assert.Equal(KeycutKind.Url, result.Keycut.Kind);
        }

        [Fact]
        public async Task NameWithDot_IsKeptIntact()
        {
            var result = await KeycutCreator.CreateAsync(_target, "notes.v2", Options());

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(Path.Combine(_out, "notes.v2.bat")));
        }

        [Fact]
        public async Task Duplicate_WithoutOverwrite_ReturnsDuplicateAndKeepsFile()
        {
            await KeycutCreator.CreateAsync(_target, "dup", Options());
            var path = Path.Combine(_out, "dup.bat");
            var before = File.ReadAllText(path);

            var result = await KeycutCreator.CreateAsync("https://example.com", "dup", Options());

            Assert.Equal(CreateOutcome.DuplicateName, result.Outcome);
            Assert.Equal(before, File.ReadAllText(path));
        }

        [Fact]
        public async Task Duplicate_IsCaseInsensitive()
        {
            await KeycutCreator.CreateAsync(_target, "dup", Options());

            var result = await KeycutCreator.CreateAsync(_target, "DUP", Options());

            Assert.Equal(CreateOutcome.DuplicateName, result.Outcome);
        }

        [Fact]
        public async Task Duplicate_WithOverwrite_ReplacesSilently()
        {
            await KeycutCreator.CreateAsync(_target, "dup", Options());

            var result = await KeycutCreator.CreateAsync("https://example.com", "dup", Options(overwrite: true));

            Assert.Equal(CreateOutcome.Success, result.Outcome);
            Assert.Equal(KeycutKind.Url, result.Keycut.Kind);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("a:b")]
        [InlineData("a*b")]
        [InlineData("a?b")]
        [InlineData("a\"b")]
        [InlineData("a|b")]
        [InlineData("a<b")]
        [InlineData("con")]
        [InlineData("NUL")]
        [InlineData("Com1")]
        [InlineData("lpt9")]
        [InlineData("nul.txt")]
        [InlineData("name.")]
        [InlineData(" padded")]
        [InlineData("padded ")]
        public async Task InvalidName_IsRejected(string name)
        {
            var result = await KeycutCreator.CreateAsync(_target, name, Options());

            Assert.Equal(CreateOutcome.InvalidName, result.Outcome);
            Assert.False(Directory.Exists(_out));
        }

        [Fact]
        public async Task FullPathAsName_IsInvalid()
        {
            var result = await KeycutCreator.CreateAsync(_target, Path.Combine(_root, "elsewhere"), Options());

            Assert.Equal(CreateOutcome.InvalidName, result.Outcome);
        }

        [Theory]
        [InlineData("con2")]
        [InlineData("my keycut")]
        [InlineData("a.b")]
        [InlineData("コンソール")]
        public void ValidNames_Accepted(string name)
        {
            Assert.True(KeycutCreator.IsValidName(name));
        }

        [Fact]
        public async Task MissingDestination_IsDestinationInvalid()
        {
            var result = await KeycutCreator.CreateAsync(Path.Combine(_root, "nope.txt"), "x", Options());

            Assert.Equal(CreateOutcome.DestinationInvalid, result.Outcome);
            Assert.Equal(DestinationError.NotFound, result.DestinationError);
            Assert.False(Directory.Exists(_out));
        }

        [Fact]
        public async Task EmptyDestination_IsDestinationInvalid()
        {
            var result = await KeycutCreator.CreateAsync("", "x", Options());

            Assert.Equal(CreateOutcome.DestinationInvalid, result.Outcome);
            Assert.Equal(DestinationError.Empty, result.DestinationError);
        }

        [Fact]
        public async Task DestinationDeletedBetweenSelectionAndCreate_IsDestinationInvalid()
        {
            Assert.True(DestinationResolver.Resolve(_target).IsValid);
            File.Delete(_target);

            var result = await KeycutCreator.CreateAsync(_target, "x", Options());

            Assert.Equal(CreateOutcome.DestinationInvalid, result.Outcome);
        }

        [Fact]
        public async Task OutputFolderIsAFile_IsOutputFolderError()
        {
            File.WriteAllText(_out, "not a folder");

            var result = await KeycutCreator.CreateAsync(_target, "x", Options());

            Assert.Equal(CreateOutcome.OutputFolderError, result.Outcome);
        }

        [Fact]
        public async Task PathUpdateFailure_IsPathUpdateError()
        {
            _path.Result = false;

            var result = await KeycutCreator.CreateAsync(_target, "x", Options());

            Assert.Equal(CreateOutcome.PathUpdateError, result.Outcome);
            Assert.False(File.Exists(Path.Combine(_out, "x.bat")));
        }

        [Fact]
        public async Task Create_UsesInjectedPathUpdater()
        {
            var result = await KeycutCreator.CreateAsync(_target, "x", Options());

            Assert.True(result.IsSuccess);
            Assert.Equal(1, _path.Calls);
        }

        [Fact]
        public async Task Unexpected_ReturnsMessage()
        {
            // A directory squatting on the .bat path makes the write throw
            Directory.CreateDirectory(_out);
            Directory.CreateDirectory(Path.Combine(_out, "blocked.bat"));

            var result = await KeycutCreator.CreateAsync(_target, "blocked", Options(overwrite: true));

            Assert.Equal(CreateOutcome.Unexpected, result.Outcome);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }

        [Fact]
        public async Task CreatedBat_MatchesRunnerOutput_ApartFromPath()
        {
            var expectedFolder = Path.Combine(_root, "expected");
            Directory.CreateDirectory(expectedFolder);
            var shortcut = new Shortcut(_target, "same", expectedFolder);
            new Runner().CreateShortcutFile(shortcut, true, "");

            await KeycutCreator.CreateAsync(_target, "same", Options());

            var expected = File.ReadAllLines(shortcut.FullPath).Select(l => l.Replace(expectedFolder, "<out>"));
            var actual = File.ReadAllLines(Path.Combine(_out, "same.bat")).Select(l => l.Replace(_out, "<out>"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task Cancelled_Throws()
        {
            var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => KeycutCreator.CreateAsync(_target, "x", Options(), cts.Token));
        }
    }
}
