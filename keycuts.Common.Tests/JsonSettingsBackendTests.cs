using System;
using System.IO;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The macOS settings file (Phase 4.2), in a temp folder.</summary>
    public class JsonSettingsBackendTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));

        private string SettingsPath => Path.Combine(_root, "keycuts", "settings.json");

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Fact]
        public void EachKind_ReadsBackAsTheRegistryWouldGiveIt()
        {
            var backend = new JsonSettingsBackend(SettingsPath);

            backend.WriteString("OutputFolder", "/Users/me/Shortcuts");
            backend.WriteDword("CheckForUpdates", 1);
            backend.WriteMultiString("MigratedFolders", new[] { "/a", "/b \"c\"" });

            var reread = new JsonSettingsBackend(SettingsPath);
            Assert.Equal("/Users/me/Shortcuts", reread.Read("OutputFolder"));
            Assert.Equal(1, reread.Read("CheckForUpdates"));
            Assert.Equal(new[] { "/a", "/b \"c\"" }, reread.Read("MigratedFolders"));
            Assert.Null(reread.Read("Missing"));
        }

        [Fact]
        public void AWrite_KeepsValuesItDoesntKnow_AndLeavesNoTempFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, "{ \"FromANewerVersion\": { \"x\": true }, \"Theme\": \"Dark\" }");

            new JsonSettingsBackend(SettingsPath).WriteString("Theme", "Light");

            var text = File.ReadAllText(SettingsPath);
            Assert.Contains("FromANewerVersion", text);
            Assert.Equal("Light", new JsonSettingsBackend(SettingsPath).Read("Theme"));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("[1, 2]")]
        [InlineData("")]
        public void AFileThatIsntAnObject_ReadsAsEmpty_AndAWriteReplacesIt(string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, contents);
            var backend = new JsonSettingsBackend(SettingsPath);

            Assert.Null(backend.Read("Theme"));

            backend.WriteDword("WelcomeDismissed", 1);
            Assert.Equal(1, backend.Read("WelcomeDismissed"));
        }

        [Fact]
        public void AValueOfTheWrongShape_ReadsAsMissing()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, "{ \"A\": 1.5, \"B\": true, \"C\": [1], \"D\": null }");
            var backend = new JsonSettingsBackend(SettingsPath);

            foreach (var name in new[] { "A", "B", "C", "D" })
            {
                Assert.Null(backend.Read(name));
            }
        }

        [Fact]
        public void DeleteValues_RemovesThoseThatExist_AndNeverThrows()
        {
            var backend = new JsonSettingsBackend(SettingsPath);
            backend.DeleteValues(new[] { "Nothing", "Yet" });
            Assert.False(File.Exists(SettingsPath));

            backend.WriteString("LastUpdateCheck", "x");
            backend.WriteString("Theme", "Dark");
            backend.DeleteValues(new[] { "LastUpdateCheck", "Absent" });

            Assert.Null(backend.Read("LastUpdateCheck"));
            Assert.Equal("Dark", backend.Read("Theme"));
        }
    }
}
