using System;
using System.IO;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class ShortcutTests
    {
        [Theory]
        [InlineData("a%b", "a%%b")]
        [InlineData("a%%b", "a%%%%b")]
        [InlineData("a&b", "a^&b")]
        [InlineData("a&&b", "a^&^&b")]
        [InlineData("&a", "^&a")]
        [InlineData("a^&b", "a^^^&b")]
        [InlineData("a|b<c>d", "a^|b^<c^>d")]
        [InlineData("q=\"a&b\"&c", "q=\"a&b\"^&c")]
        [InlineData("q=\"50%\"", "q=\"50%%\"")]
        [InlineData("a\"b&c", "a\"b&c")]
        [InlineData("plain", "plain")]
        public void SanitizeBatEscapeCharacters_EscapesSpecialCharacters(string input, string expected)
        {
            Assert.Equal(expected, Shortcut.SanitizeBatEscapeCharacters(input));
        }

        [Theory]
        [InlineData(@"D:\Windows\System32\drivers\etc\hosts", true)]   // Windows installed on D:
        [InlineData(@"d:\windows\system32\DRIVERS\etc\HOSTS", true)]
        [InlineData(@"C:\Windows\System32\drivers\etc\hosts", true)]   // the usual path still counts
        [InlineData(@"D:\Windows\System32\drivers\etc\hosts.bak", false)]
        [InlineData(@"D:\notes\hosts", false)]
        public void IsHostsFile_UsesThisMachinesSystemFolder(string fullPath, bool expected)
        {
            Assert.Equal(expected, Shortcut.IsHostsFile(fullPath, @"D:\Windows\System32"));
        }

        [Fact]
        public void GetShortcutTypeFromDestination_TheRealHostsFile_IsHostsFile()
        {
            var hosts = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
            Assert.True(File.Exists(hosts), "This machine has no hosts file at " + hosts);

            Assert.Equal(ShortcutType.HostsFile, Shortcut.GetShortcutTypeFromDestination(hosts, out _));
        }

        [Fact]
        public void GetShortcutTypeFromDestination_MissingPath_IsUnknownInsteadOfThrowing()
        {
            var missing = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"), "missing.txt");

            var type = Shortcut.GetShortcutTypeFromDestination(missing, out var newDestination);

            Assert.Equal(ShortcutType.Unknown, type);
            Assert.Equal(missing, newDestination);
        }

        [Fact]
        public void GetShortcutTypeFromDestination_ExistingFileAndFolder_AreStillClassified()
        {
            var folder = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "notes.txt");
            File.WriteAllText(file, "");

            try
            {
                Assert.Equal(ShortcutType.Folder, Shortcut.GetShortcutTypeFromDestination(folder, out _));
                Assert.Equal(ShortcutType.File, Shortcut.GetShortcutTypeFromDestination(file, out _));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
