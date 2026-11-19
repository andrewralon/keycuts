using System;
using System.IO;
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

        [Fact]
        public void Trims_whitespace_and_quotes()
        {
            Assert.Equal(@"C:\temp\fixtures", DebugOverrides.ParseOutputFolder("  \"C:\\temp\\fixtures\"  "));
        }
    }

#if DEBUG
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

        [Fact]
        public void Runner_and_repository_use_the_override()
        {
            Assert.Equal(_root, new Runner().GetOutputFolder());
            Assert.Equal(_root, KeycutRepository.CreateDefault().OutputFolder);
        }

        [Fact]
        public void Without_the_variable_the_override_is_inactive()
        {
            Environment.SetEnvironmentVariable(DebugOverrides.OutputFolderVariable, null);

            Assert.Null(DebugOverrides.GetOutputFolder());
        }

        [Fact]
        public void SetOutputFolder_does_not_write_the_registry()
        {
            var before = ReadRegistryOutputFolder();

            var result = new Runner().SetOutputFolder(Path.Combine(_root, "other"));

            Assert.Equal(Path.Combine(_root, "other"), result);
            Assert.Equal(before, ReadRegistryOutputFolder());
        }

        [Fact]
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
#endif
}
