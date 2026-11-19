using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class TargetResolverTests : IDisposable
    {
        private readonly string _dir;

        public TargetResolverTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            // The shell can briefly hold link files open, so retry
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Delete(_dir, true);
                    return;
                }
                catch (IOException) when (attempt < 10)
                {
                    System.Threading.Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException) when (attempt < 10)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
        }

        private string MakeFile(string name, string content = "x")
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Url_IsValidUrl()
        {
            var r = TargetResolver.Resolve("https://example.com/a?b=1");
            Assert.True(r.IsValid);
            Assert.Equal(KeycutKind.Url, r.Kind);
            Assert.Equal(ShortcutType.Url, r.RawType);
            Assert.Equal("https://example.com/a?b=1", r.Target);
        }

        [Fact]
        public void SchemelessUrl_GetsHttpScheme()
        {
            var r = TargetResolver.Resolve("amazon.com");
            Assert.True(r.IsValid);
            Assert.Equal(KeycutKind.Url, r.Kind);
            Assert.Equal("http://amazon.com/", r.Target);
        }

        [Fact]
        public void MissingFileLikeName_IsNotAUrl()
        {
            var r = TargetResolver.Resolve("notes.txt");
            Assert.False(r.IsValid);
            Assert.Equal(TargetError.NotFound, r.Error);
        }

        [Fact]
        public void UrlFile_ReadsUrl()
        {
            var path = MakeFile("site.url", "[InternetShortcut]\r\nURL=https://example.org/\r\n");
            var r = TargetResolver.Resolve(path);
            Assert.True(r.IsValid);
            Assert.Equal(ShortcutType.Url, r.RawType);
            Assert.Equal("https://example.org/", r.Target);
        }

        [Fact]
        public void LnkFile_ResolvesTarget()
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return;
            }

            var target = MakeFile("target.txt");
            var lnk = Path.Combine(_dir, "link.lnk");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(lnk);
            shortcut.TargetPath = target;
            shortcut.Save();

            var r = TargetResolver.Resolve(lnk);
            Assert.True(r.IsValid);
            Assert.Equal(KeycutKind.File, r.Kind);
            Assert.Equal(target, r.Target, ignoreCase: true);
        }

        [Theory]
        [InlineData("{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
        [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
        [InlineData("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
        public void Clsid_IsFolderKind(string guid)
        {
            var r = TargetResolver.Resolve(guid);
            Assert.True(r.IsValid);
            Assert.Equal(ShortcutType.CLSIDKey, r.RawType);
            Assert.Equal(KeycutKind.Folder, r.Kind);
        }

        [Fact]
        public void ExistingFolder_IsFolder()
        {
            var r = TargetResolver.Resolve(_dir);
            Assert.True(r.IsValid);
            Assert.Equal(KeycutKind.Folder, r.Kind);
            Assert.Equal(_dir, r.Target);
        }

        [Fact]
        public void ExistingFile_IsFile()
        {
            var path = MakeFile("a.txt");
            var r = TargetResolver.Resolve(path);
            Assert.True(r.IsValid);
            Assert.Equal(ShortcutType.File, r.RawType);
            Assert.Equal(KeycutKind.File, r.Kind);
            Assert.Equal(path, r.Target);
        }

        [Fact]
        public void HostsFile_IsHostsFileTypeAndFileKind()
        {
            var hosts = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
            if (!File.Exists(hosts))
            {
                return;
            }

            var r = TargetResolver.Resolve(hosts);
            Assert.True(r.IsValid);
            Assert.Equal(ShortcutType.HostsFile, r.RawType);
            Assert.Equal(KeycutKind.File, r.Kind);
        }

        [Fact]
        public void MissingPath_IsNotFound()
        {
            var r = TargetResolver.Resolve(Path.Combine(_dir, "nope", "missing.txt"));
            Assert.False(r.IsValid);
            Assert.Equal(TargetError.NotFound, r.Error);
        }

        [Fact]
        public void QuotedPath_IsUnquoted()
        {
            var path = MakeFile("q.txt");
            var r = TargetResolver.Resolve("\"" + path + "\"");
            Assert.True(r.IsValid);
            Assert.Equal(path, r.Target);
        }

        [Fact]
        public void WhitespacePaddedPath_IsTrimmed()
        {
            var path = MakeFile("w.txt");
            var r = TargetResolver.Resolve("  \"" + path + "\"  \r\n");
            Assert.True(r.IsValid);
            Assert.Equal(path, r.Target);
        }

        [Fact]
        public void EnvVarPath_IsExpanded()
        {
            var path = MakeFile("e.txt");
            var name = "KEYCUTS_TEST_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(name, _dir);
            try
            {
                var r = TargetResolver.Resolve("%" + name + "%\\e.txt");
                Assert.True(r.IsValid);
                Assert.Equal(path, r.Target);
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [Fact]
        public void FileUri_IsLocalPath()
        {
            var path = MakeFile("u.txt");
            var r = TargetResolver.Resolve(new Uri(path).AbsoluteUri);
            Assert.True(r.IsValid);
            Assert.Equal(path, r.Target);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("\"\"")]
        public void Empty_IsEmptyError(string input)
        {
            var r = TargetResolver.Resolve(input);
            Assert.False(r.IsValid);
            Assert.Equal(TargetError.Empty, r.Error);
        }

        [Theory]
        [InlineData("C:\\bad|path?.txt")]
        [InlineData("a\0b")]
        [InlineData("???")]
        public void WeirdInput_DoesNotThrow(string input)
        {
            var r = TargetResolver.Resolve(input);
            Assert.False(r.IsValid);
            Assert.NotEqual(TargetError.None, r.Error);
        }

        [Fact]
        public async Task ResolveAsync_ReturnsResult()
        {
            var r = await TargetResolver.ResolveAsync(_dir, CancellationToken.None);
            Assert.True(r.IsValid);
            Assert.Equal(KeycutKind.Folder, r.Kind);
        }

        [Fact]
        public async Task ResolveAsync_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => TargetResolver.ResolveAsync(_dir, cts.Token));
        }

        [Theory]
        [InlineData(ShortcutType.Url, KeycutKind.Url)]
        [InlineData(ShortcutType.Folder, KeycutKind.Folder)]
        [InlineData(ShortcutType.CLSIDKey, KeycutKind.Folder)]
        [InlineData(ShortcutType.File, KeycutKind.File)]
        [InlineData(ShortcutType.HostsFile, KeycutKind.File)]
        [InlineData(ShortcutType.Command, KeycutKind.Command)]
        [InlineData(ShortcutType.Unknown, KeycutKind.Command)]
        public void KindMapping(ShortcutType type, KeycutKind kind)
        {
            Assert.Equal(kind, KeycutKinds.FromShortcutType(type));
        }
    }
}
