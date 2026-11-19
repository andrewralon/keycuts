using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// A description sits on a REM line, and cmd expands % before it reads REM (#162): a %~ stops the whole .bat with a
    /// syntax error. The emitter doubles every %, the parser halves them, and the real cmd decides whether that was enough.
    /// </summary>
    public class DescriptionEscapingTests : IDisposable
    {
        private readonly string _folder;

        public DescriptionEscapingTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        public static TheoryData<string> Descriptions => new TheoryData<string>
        {
            "path %~ here",
            "Opens %~dp0 and %~f1",
            "100% done",
            "%1 %* %PATH:~0,3%",
            "%%",
            "%%%",
            "%",
            "a & b | c < d > e ^ f",
            "ends with a caret ^",
            "&&",
            "Gmail inbox",
        };

        private static string[] Emit(string description)
        {
            var shortcut = Shortcut.FromParts(ShortcutType.Url, "https://example.com", @"C:\Shortcuts\x.bat", null);

            return Runner.BuildShortcutLines(shortcut, description).ToArray();
        }

        private static string DescriptionLine(string description)
        {
            return Emit(description).Single(line => line.StartsWith("REM <description>", StringComparison.Ordinal));
        }

        [Fact]
        public void Emitter_DoublesEveryPercentSign()
        {
            Assert.Equal("REM <description>path %%~dp0 is 100%% %%1 %%*</description>", DescriptionLine("path %~dp0 is 100% %1 %*"));
        }

        [Fact]
        public void Emitter_LeavesTheOtherSpecialCharactersAlone()
        {
            Assert.Equal("REM <description>a & b | c < d > e ^ f</description>", DescriptionLine("a & b | c < d > e ^ f"));
        }

        [Fact]
        public void Emitter_DoublesPercentSignsAfterFoldingNewlines()
        {
            // "\r\n" becomes two spaces, as in the existing description case
            Assert.Equal("REM <description>%%~a  %%~b</description>", DescriptionLine("  %~a\r\n%~b  "));
        }

        [Fact]
        public void Parser_ReadsADoubledPercentAsOne()
        {
            Assert.Equal("100% %~dp0", ShortcutFile.ParseDescription(new[] { "REM <description>100%% %%~dp0</description>" }));
        }

        [Fact]
        public void Parser_KeepsALonePercentFromAHandWrittenFile()
        {
            Assert.Equal("100% done", ShortcutFile.ParseDescription(new[] { "REM <description>100% done</description>" }));
        }

        [Theory]
        [MemberData(nameof(Descriptions))]
        public void Description_ReadsBackAsTyped(string description)
        {
            var parsed = new ShortcutFile(@"C:\Shortcuts\x.bat", Emit(description));

            Assert.Equal(description, parsed.Description);
        }

        [Theory]
        [MemberData(nameof(Descriptions))]
        public void Description_DoesNotStopTheScript(string description)
        {
            var output = RunScript(DescriptionLine(description));

            Assert.Equal("ran", output.Trim());
        }

        [Fact]
        public void ControlWithoutDoubling_AStrayPercentTildeStopsTheScript()
        {
            // The reason for the doubling: a raw %~ on a REM line is a cmd syntax error, so nothing after it runs
            var output = RunScript("REM <description>path %~ here</description>");

            // cmd's message is localized, so only that it says something is checked, not what
            Assert.DoesNotContain("ran", output);
            Assert.False(string.IsNullOrWhiteSpace(output));
        }

        /// <summary>Runs a .bat holding just <paramref name="line"/> and an ECHO, with the real cmd; returns what it printed.</summary>
        private string RunScript(string line)
        {
            var path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".bat");
            File.WriteAllText(path, "@ECHO OFF\r\n" + line + "\r\nECHO ran\r\n");

            var start = new ProcessStartInfo("cmd.exe", "/d /c \"\"" + path + "\"\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(start))
            {
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();

                Assert.True(process.WaitForExit(15000), "cmd didn't finish");

                return output.Result + error.Result;
            }
        }
    }
}
