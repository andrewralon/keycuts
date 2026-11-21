using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary><c>keycuts list</c> (#173, #237): what it prints and in what order. Temp folders and a temp usage log only.</summary>
    public class KeycutListingTests
    {
        private static Keycut K(string name, KeycutKind kind = KeycutKind.Url, string target = "https://example.com", string description = "",
            int? runs = null, DateTime lastRun = default)
        {
            return new Keycut { Name = name, Kind = kind, Target = target, Description = description, Runs = runs, LastRun = lastRun };
        }

        [Fact]
        public void Format_LinesUpTheColumns_UnderAHeader()
        {
            var lines = KeycutListing.Format(new[]
            {
                K("gmail", description: "Work email", target: "https://mail.google.com"),
                K("dl", KeycutKind.Folder, @"C:\Users\me\Downloads")
            }, usage: false);

            Assert.Equal(new[]
            {
                "Name   Type    Target                   Description",
                "gmail  url     https://mail.google.com  Work email",
                @"dl     folder  C:\Users\me\Downloads"
            }, lines);
        }

        [Fact]
        public void Format_WithUsage_AddsRunsAndLastRun_InIsoTime_AndADashForNoRecord()
        {
            var lines = KeycutListing.Format(new[]
            {
                K("gmail", runs: 12, lastRun: new DateTime(2026, 11, 15, 9, 30, 5)),
                K("new", runs: 0),
                K("old")
            }, usage: true);

            Assert.Equal("Name   Type  Runs  Last run             Target               Description", lines[0]);
            Assert.Equal("gmail  url   12    2026-11-15T09:30:05  https://example.com", lines[1]);
            Assert.Equal("new    url   0     -                    https://example.com", lines[2]);
            Assert.Equal("old    url   -     -                    https://example.com", lines[3]);
        }

        [Fact]
        public void Format_KeepsEachKeycutOnOneLine()
        {
            var lines = KeycutListing.Format(new[] { K("a", description: "two\r\nlines") }, usage: false);

            Assert.Equal(2, lines.Count);
            Assert.EndsWith("two  lines", lines[1]);
        }

        [Fact]
        public void Sort_MatchesTheManager_MostRunsAndLatestFirst_NoRecordLast_TiesByName()
        {
            var keycuts = new[]
            {
                K("b", runs: 3, lastRun: new DateTime(2026, 1, 1)),
                K("a", runs: 3, lastRun: new DateTime(2026, 3, 1)),
                K("none"),
                K("zero", runs: 0),
                K("C", runs: 9, lastRun: new DateTime(2026, 2, 1))
            };

            Assert.Equal(new[] { "a", "b", "C", "none", "zero" }, KeycutListing.Sort(keycuts, KeycutListSort.Name).Select(k => k.Name));
            Assert.Equal(new[] { "C", "a", "b", "zero", "none" }, KeycutListing.Sort(keycuts, KeycutListSort.Runs).Select(k => k.Name));
            Assert.Equal(new[] { "a", "C", "b", "none", "zero" }, KeycutListing.Sort(keycuts, KeycutListSort.LastRun).Select(k => k.Name));
        }

        [Theory]
        [InlineData(null, true, KeycutListSort.Name)]
        [InlineData("", true, KeycutListSort.Name)]
        [InlineData("NAME", true, KeycutListSort.Name)]
        [InlineData("runs", true, KeycutListSort.Runs)]
        [InlineData("last-run", true, KeycutListSort.LastRun)]
        [InlineData("modified", false, KeycutListSort.Name)]
        public void TryParseSort_ReadsTheSortNames(string text, bool ok, KeycutListSort expected)
        {
            Assert.Equal(ok, KeycutListing.TryParseSort(text, out var sort));
            Assert.Equal(expected, sort);
        }

        [Fact]
        public void Filter_MatchesNameTypeTargetOrDescription_AnyCase()
        {
            var keycuts = new[]
            {
                K("gmail", target: "https://mail.google.com"),
                K("docs", KeycutKind.Folder, @"C:\Docs", "My PAPERS"),
                K("np", KeycutKind.File, @"C:\Windows\notepad.exe")
            };

            Assert.Equal(new[] { "gmail" }, KeycutListing.Filter(keycuts, "MAIL").Select(k => k.Name));
            Assert.Equal(new[] { "docs" }, KeycutListing.Filter(keycuts, "papers").Select(k => k.Name));
            Assert.Equal(new[] { "docs" }, KeycutListing.Filter(keycuts, "folder").Select(k => k.Name));
            Assert.Equal(3, KeycutListing.Filter(keycuts, "  ").Count);
        }

        // Review of #253: the same rule as the Manager's search, so a resolved target (#212) is found by what it shows
        [Fact]
        public void Filter_MatchesAResolvedTarget_AsTheManagerDoes()
        {
            var g = K("g", target: "%BASE%%QUERY%") with
            {
                ResolvedTarget = new ResolvedTarget("https://github.com/GolfNowEng", Array.Empty<ScriptVariable>(), true)
            };

            Assert.Equal(new[] { "g" }, KeycutListing.Filter(new[] { g, K("b") }, "github").Select(k => k.Name));
            Assert.Equal(new[] { "g" }, KeycutListing.Filter(new[] { g, K("b") }, "%QUERY%").Select(k => k.Name));
        }

        [Fact]
        public async System.Threading.Tasks.Task TheListing_ReadsRunCounts_ThroughTheRepository_WithItsUsageLog()
        {
            var root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            var folder = Path.Combine(root, "out");
            var usage = new UsageLog(Path.Combine(root, "usage"));
            Directory.CreateDirectory(folder);
            usage.EnsureFolder();

            try
            {
                foreach (var name in new[] { "often", "never" })
                {
                    new Runner().CreateShortcutFile(new Shortcut("https://example.com/" + name, name + ShortcutFile.Extension, folder), true, "");
                }

                File.WriteAllText(Path.Combine(usage.Folder, "often" + UsageLog.Extension), "1\r\n1\r\n1\r\n");
                File.SetLastWriteTime(Path.Combine(usage.Folder, "often" + UsageLog.Extension), new DateTime(2026, 11, 16, 8, 0, 0));

                var keycuts = await new KeycutRepository(folder, new PermanentDeleter(), null, usage).LoadAllAsync();
                var lines = KeycutListing.Format(KeycutListing.Sort(keycuts, KeycutListSort.Runs), usage: true);

                Assert.StartsWith("often  url   3     2026-11-16T08:00:00", lines[1]);
                Assert.StartsWith("never  url   0     -", lines[2]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
