#nullable enable
using System;
using System.IO;
using keycuts.Common;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    public class SettingsValidationTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(@"relative\folder")]
        [InlineData(@"C:folder")]
        public void Rejects_folders_that_are_not_full_paths(string? folder)
        {
            Assert.False(Settings.IsValidOutputFolder(folder));
        }

        [WindowsTheory]
        [InlineData(@"C:\Shortcuts")]
        [InlineData(@"\\server\share\keys")]
        public void Accepts_full_paths(string folder)
        {
            Assert.True(Settings.IsValidOutputFolder(folder));
        }

        [Fact]
        public void Saving_an_invalid_folder_reports_it_without_writing_anything()
        {
            var settings = new Settings { OutputFolder = "   " };

            Assert.Equal(new FolderSaveResult(false, Settings.InvalidOutputFolderMessage), settings.SaveOutputFolder());
        }
    }

    public class CrashReporterTests
    {
        [Fact]
        public void Log_appends_the_exception_and_creates_the_folder()
        {
            var root = Path.Combine(Path.GetTempPath(), "keycuts-crash-tests-" + Guid.NewGuid().ToString("N"));
            var logPath = Path.Combine(root, "nested", "crash.log");

            try
            {
                Assert.True(CrashReporter.Log(logPath, new InvalidOperationException("first")));
                Assert.True(CrashReporter.Log(logPath, new InvalidOperationException("second")));

                var text = File.ReadAllText(logPath);
                Assert.Contains("first", text);
                Assert.Contains("second", text);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Log_returns_false_when_the_path_is_unusable()
        {
            var root = Path.GetTempFileName();

            try
            {
                // A file where the log's parent folder should be
                Assert.False(CrashReporter.Log(Path.Combine(root, "crash.log"), new InvalidOperationException()));
            }
            finally
            {
                File.Delete(root);
            }
        }
    }
}
