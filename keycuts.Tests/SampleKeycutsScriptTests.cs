using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Runs tools/New-SampleKeycuts.ps1 against temp folders only and loads the result with the real repository.</summary>
    public class SampleKeycutsScriptTests : IDisposable
    {
        private readonly string _root;

        public SampleKeycutsScriptTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-sample-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private static string FindScript()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tools", "New-SampleKeycuts.ps1");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new FileNotFoundException("tools\\New-SampleKeycuts.ps1 not found above " + AppContext.BaseDirectory);
        }

        private static (int ExitCode, string Output) RunScript(string arguments)
        {
            var info = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{FindScript()}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(info))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                process.WaitForExit();

                return (process.ExitCode, stdout.Result + stderr.Result);
            }
        }

        private async Task<System.Collections.Generic.IReadOnlyList<Keycut>> Generate(int count)
        {
            var folder = Path.Combine(_root, "fixtures-" + count);
            var result = RunScript($"-Folder \"{folder}\" -Count {count}");

            Assert.True(result.ExitCode == 0, result.Output);

            var repository = new KeycutRepository(folder);
            var keycuts = await repository.LoadAllAsync();
            await repository.CheckDestinationsAsync(keycuts);

            return keycuts;
        }

        [Theory]
        [InlineData(20)]
        [InlineData(100)]
        public async Task Generates_requested_number_of_loadable_keycuts(int count)
        {
            var keycuts = await Generate(count);

            Assert.Equal(count, keycuts.Count);
            Assert.Equal(count, keycuts.Select(k => k.Name.ToLowerInvariant()).Distinct().Count());
        }

        [Fact]
        public async Task Includes_canonical_entries_with_descriptions()
        {
            var keycuts = await Generate(20);

            var vicki = keycuts.Single(k => k.Name == "l-vicki");
            Assert.Equal(KeycutKind.Url, vicki.Kind);
            Assert.Equal("logs for vicki", vicki.Description);
            Assert.StartsWith("https://logs.example.com/vicki", vicki.Destination);

            var downloads = keycuts.Single(k => k.Name == "downloads");
            Assert.Equal(KeycutKind.Folder, downloads.Kind);
            Assert.Equal("Downloads folder", downloads.Description);
            Assert.Equal(@"%USERPROFILE%\Downloads", downloads.Destination);
        }

        [Fact]
        public async Task Mixes_all_kinds_and_edge_cases()
        {
            var keycuts = await Generate(100);

            foreach (var kind in Enum.GetValues<KeycutKind>())
            {
                Assert.Contains(keycuts, k => k.Kind == kind);
            }

            Assert.Contains(keycuts, k => k.Name.Length > 40);
            Assert.Contains(keycuts, k => k.Description.Length > 100);
            Assert.Contains(keycuts, k => k.Destination.Length > 200);
            Assert.Contains(keycuts, k => k.Kind == KeycutKind.File && k.DestinationStatus == DestinationStatus.Missing);
            Assert.Contains(keycuts, k => k.Kind == KeycutKind.File && k.DestinationStatus == DestinationStatus.Ok);
        }

        [Fact]
        public void Refuses_the_real_output_folder()
        {
            var result = RunScript("-Folder C:\\Shortcuts -Count 5 -AllowNonTemp");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Refusing", result.Output);
        }

        [Fact]
        public void Refuses_a_folder_outside_temp_without_the_switch()
        {
            var outside = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "keycuts-sample-" + Guid.NewGuid().ToString("N"));

            var result = RunScript($"-Folder \"{outside}\" -Count 5");

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(Directory.Exists(outside));
        }

        [Fact]
        public void Refuses_a_folder_with_foreign_bat_files()
        {
            var folder = Path.Combine(_root, "foreign");
            Directory.CreateDirectory(folder);
            var existing = Path.Combine(folder, "mine.bat");
            File.WriteAllText(existing, "@ECHO OFF");

            var result = RunScript($"-Folder \"{folder}\" -Count 5");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal("@ECHO OFF", File.ReadAllText(existing));
        }

        [Fact]
        public async Task Regenerating_replaces_only_a_previous_sample_set()
        {
            await Generate(20);
            var folder = Path.Combine(_root, "fixtures-20");

            var result = RunScript($"-Folder \"{folder}\" -Count 5");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(5, Directory.GetFiles(folder, "*.bat").Length);
        }
    }
}
