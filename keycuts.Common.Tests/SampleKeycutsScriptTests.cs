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
            await repository.CheckTargetsAsync(keycuts);

            return keycuts;
        }

        // The fixtures load with the real repository, one keycut per name
        [WindowsFact]
        public async Task Generates_TheRequestedNumber_OfLoadableKeycuts()
        {
            var keycuts = await Generate(20);

            Assert.Equal(20, keycuts.Count);
            Assert.Equal(20, keycuts.Select(k => k.Name.ToLowerInvariant()).Distinct().Count());
        }

        [WindowsFact]
        public void Refuses_the_real_output_folder()
        {
            var result = RunScript("-Folder C:\\Shortcuts -Count 5 -AllowNonTemp");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Refusing", result.Output);
        }

        [WindowsFact]
        public void Refuses_a_folder_outside_temp_without_the_switch()
        {
            var outside = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "keycuts-sample-" + Guid.NewGuid().ToString("N"));

            var result = RunScript($"-Folder \"{outside}\" -Count 5");

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(Directory.Exists(outside));
        }

        [WindowsFact]
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

        [WindowsFact]
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
