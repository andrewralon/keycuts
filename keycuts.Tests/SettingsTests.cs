#nullable enable
using System;
using System.IO;
using keycuts.Common;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    public class PathListTests
    {
        [Fact]
        public void Matches_whole_entries_not_substrings()
        {
            Assert.False(PathList.Contains(@"C:\Shortcuts2;C:\Other", @"C:\Shortcuts"));
            Assert.True(PathList.Contains(@"C:\Other;c:\shortcuts\", @"C:\Shortcuts"));
        }

        [Fact]
        public void Appends_a_new_entry_and_keeps_unexpanded_variables()
        {
            Assert.Equal(@"%USERPROFILE%\bin;C:\Shortcuts", PathList.AddOrReplace(@"%USERPROFILE%\bin;", null, @"C:\Shortcuts"));
        }

        [Fact]
        public void Does_not_add_a_duplicate()
        {
            Assert.Equal(@"C:\Shortcuts\;C:\Other", PathList.AddOrReplace(@"C:\Shortcuts\;C:\Other", null, @"C:\Shortcuts"));
        }

        [Fact]
        public void Replaces_the_old_entry()
        {
            Assert.Equal(@"C:\Other;D:\Keys", PathList.AddOrReplace(@"C:\Shortcuts;C:\Other", @"C:\Shortcuts", @"D:\Keys"));
        }

        [Fact]
        public void Leaves_a_similar_entry_alone_when_replacing()
        {
            Assert.Equal(@"C:\Shortcuts2;D:\Keys", PathList.AddOrReplace(@"C:\Shortcuts2", @"C:\Shortcuts", @"D:\Keys"));
        }

        [Fact]
        public void Handles_an_empty_path()
        {
            Assert.Equal(@"C:\Shortcuts", PathList.AddOrReplace(null!, null!, @"C:\Shortcuts"));
        }
    }

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

        [Theory]
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

            Assert.Equal(Settings.InvalidOutputFolderMessage, settings.SaveSettings());
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
