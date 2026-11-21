using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using keycuts.Common;
using Microsoft.Win32;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Process-wide environment variable, so these tests must not run alongside each other or anything touching it.</summary>
    [CollectionDefinition("OutputFolderOverride", DisableParallelization = true)]
    public class OutputFolderOverrideCollection
    {
    }

    public class DebugOverridesParseTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("relative\\folder")]
        [InlineData("bad|path:C:\\x")]
        public void Rejects_empty_and_non_absolute_values(string value)
        {
            Assert.Null(DebugOverrides.ParseOutputFolder(value));
        }

        [WindowsFact]
        public void Trims_whitespace_and_quotes()
        {
            Assert.Equal(@"C:\temp\fixtures", DebugOverrides.ParseOutputFolder("  \"C:\\temp\\fixtures\"  "));
        }
    }

#if DEBUG
    // Checks the registry is left alone
    [SupportedOSPlatform("windows")]
    [Collection("OutputFolderOverride")]
    public class OutputFolderOverrideTests : IDisposable
    {
        private readonly string _root;
        private readonly string _previous;

        public OutputFolderOverrideTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-override-tests-" + Guid.NewGuid().ToString("N"));
            _previous = Environment.GetEnvironmentVariable(DebugOverrides.OutputFolderVariable);
            Environment.SetEnvironmentVariable(DebugOverrides.OutputFolderVariable, _root);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(DebugOverrides.OutputFolderVariable, _previous);

            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private static object ReadRegistryOutputFolder()
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\TeamRalon\keycuts", "OutputFolder", null);
        }

        [WindowsFact]
        public void Runner_and_repository_use_the_override()
        {
            Assert.Equal(_root, new Runner().GetOutputFolder());
            Assert.Equal(_root, KeycutRepository.CreateDefault().OutputFolder);
        }

        [WindowsFact]
        public void Without_the_variable_the_override_is_inactive()
        {
            Environment.SetEnvironmentVariable(DebugOverrides.OutputFolderVariable, null);

            Assert.Null(DebugOverrides.GetOutputFolder());
        }

        [WindowsFact]
        public void SetOutputFolder_does_not_write_the_registry()
        {
            var before = ReadRegistryOutputFolder();

            var result = new Runner().SetOutputFolder(Path.Combine(_root, "other"));

            Assert.Equal(Path.Combine(_root, "other"), result);
            Assert.Equal(before, ReadRegistryOutputFolder());
        }

        [WindowsFact]
        public void Run_creates_the_keycut_in_the_override_folder_without_touching_registry_or_path()
        {
            var registryBefore = ReadRegistryOutputFolder();
            var pathBefore = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
            var target = Path.Combine(_root, "target");
            Directory.CreateDirectory(target);

            var exitCode = new Runner().Run(target, "sample-run", "");

            Assert.Equal(ExitCode.Success, exitCode);
            Assert.True(File.Exists(Path.Combine(_root, "sample-run.bat")));
            Assert.Equal(registryBefore, ReadRegistryOutputFolder());
            Assert.Equal(pathBefore, Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
        }
    }

    // The macOS twin (Phase 4.3): the override is the folder, and the settings file and the shell profiles are left alone
    [SupportedOSPlatform("macos")]
    [Collection("OutputFolderOverride")]
    public class MacOutputFolderOverrideTests : IDisposable
    {
        private readonly string _root;
        private readonly string _previous;

        public MacOutputFolderOverrideTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-override-tests-" + Guid.NewGuid().ToString("N"));
            _previous = Environment.GetEnvironmentVariable(DebugOverrides.OutputFolderVariable);
            Environment.SetEnvironmentVariable(DebugOverrides.OutputFolderVariable, _root);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(DebugOverrides.OutputFolderVariable, _previous);

            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        // Read only, to see that nothing was written
        private static string Snapshot()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.Join("|", new[] { JsonSettingsBackend.DefaultPath, Path.Combine(home, ".zprofile"), Path.Combine(home, ".bash_profile") }
                .Select(file => File.Exists(file) ? File.GetLastWriteTimeUtc(file).Ticks + ":" + new FileInfo(file).Length : "none"));
        }

        [MacFact]
        public void TheDefault_IsShortcutsInTheHomeFolder_AndTheOverrideReplacesIt()
        {
            Assert.Equal(Path.Combine(Environment.GetEnvironmentVariable("HOME"), "Shortcuts"), Runner.DefaultOutputFolder);
            Assert.Equal(_root, new Runner().GetOutputFolder());
        }

        [MacFact]
        public void SetOutputFolder_DoesNotWriteTheSettingsFile()
        {
            var before = Snapshot();

            Assert.Equal(Path.Combine(_root, "other"), new Runner().SetOutputFolder(Path.Combine(_root, "other")));
            Assert.Equal(before, Snapshot());
        }

        [MacFact]
        public void Run_CreatesAnExecutableKeycutInTheOverrideFolder_WithoutTouchingSettingsOrProfiles()
        {
            var before = Snapshot();
            var target = Path.Combine(_root, "target");
            Directory.CreateDirectory(target);

            Assert.Equal(ExitCode.Success, new Runner().Run(target, "sample-run", ""));

            var file = Path.Combine(_root, "sample-run");
            Assert.Equal(ShFormat.Shebang, File.ReadLines(file).First());
            Assert.Equal("755", Convert.ToString((int)File.GetUnixFileMode(file), 8));
            Assert.Equal(before, Snapshot());
        }
    }
#endif
}
