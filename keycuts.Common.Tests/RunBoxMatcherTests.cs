using System;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Run box's suggestions (Phase 6.3).</summary>
    public class RunBoxMatcherTests
    {
        private static readonly DateTime Day = new DateTime(2026, 11, 20);

        private static Keycut K(string name, int? runs = null, int lastRunDay = 0, string description = "", string target = "https://example.com")
        {
            return new Keycut { Name = name, Runs = runs, LastRun = lastRunDay == 0 ? default : Day.AddDays(lastRunDay), Description = description, Target = target };
        }

        private static string Names(string text, params Keycut[] keycuts)
        {
            return string.Join(" ", RunBoxMatcher.Find(keycuts, text).Select(m => m.Keycut.Name + ":" + m.Kind));
        }

        [Fact]
        public void ExactFirst_ThenPrefix_ThenContains_InAnyCase()
        {
            Assert.Equal("Gm:Exact gmail:Prefix gmaps:Prefix work:Contains",
                Names("gm", K("gmaps"), K("work", description: "GMail at work"), K("gmail"), K("Gm"), K("docs")));
        }

        [Fact]
        public void Contains_LooksAtTheNameDescriptionAndTarget()
        {
            Assert.Equal("dl:Contains notes:Contains xdocs:Contains",
                Names("docs", K("dl", target: "/Users/me/Google Docs"), K("notes", description: "Team docs"), K("xdocs"), K("other")));
        }

        [Fact]
        public void Ties_GoToMoreRuns_ThenTheLatestRun_ThenTheName()
        {
            Assert.Equal("ga:Prefix gd:Prefix gc:Prefix gb:Prefix",
                Names("g", K("gb"), K("gc", runs: 1, lastRunDay: 1), K("gd", runs: 1, lastRunDay: 2), K("ga", runs: 5)));
        }

        [Fact]
        public void RunsNeverLiftAMatchAboveABetterKind()
        {
            Assert.Equal("g:Exact gmail:Prefix", Names("g", K("gmail", runs: 99), K("g")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void NoText_IsNoSuggestions(string text)
        {
            Assert.Empty(RunBoxMatcher.Find(new[] { K("gmail") }, text));
        }

        [Fact]
        public void Recent_IsTheLatestRunFirst_ThenTheName_WhateverTheRuns()
        {
            var recent = RunBoxMatcher.Recent(new[] { K("often", runs: 99, lastRunDay: 1), K("b", runs: 1, lastRunDay: 3), K("A", runs: 1, lastRunDay: 3), K("last", runs: 2, lastRunDay: 4) });

            Assert.Equal(new[] { "last", "A", "b", "often" }, recent.Select(k => k.Name));
        }

        [Fact]
        public void Recent_LeavesOutKeycutsWithNoRun()
        {
            Assert.Empty(RunBoxMatcher.Recent(new[] { K("never", runs: 0), K("unknown") }));
            Assert.Empty(RunBoxMatcher.Recent(null));
        }

        [Fact]
        public void Recent_HoldsTheLimit()
        {
            var keycuts = Enumerable.Range(1, 9).Select(day => K("k" + day, runs: 1, lastRunDay: day)).ToArray();

            Assert.Equal(new[] { "k9", "k8", "k7" }, RunBoxMatcher.Recent(keycuts, 3).Select(k => k.Name));
            Assert.Empty(RunBoxMatcher.Recent(keycuts, 0));
        }

        [Fact]
        public void NotRun_IsWhatRecentLeavesOut_ByNameInAnyCase()
        {
            var keycuts = new[] { K("ran", runs: 1, lastRunDay: 1), K("zoo", runs: 0), K("Bee"), K("ant", runs: 3), K("cat", runs: 0) };

            Assert.Equal(new[] { "ant", "Bee", "cat", "zoo" }, RunBoxMatcher.NotRun(keycuts).Select(k => k.Name));
            Assert.Equal(new[] { "ran" }, RunBoxMatcher.Recent(keycuts).Select(k => k.Name));
            Assert.Empty(RunBoxMatcher.NotRun(null));
        }

        [Fact]
        public void TheTextIsTrimmed_AndTheLimitHolds()
        {
            Assert.Equal(new[] { "ga", "gb" }, RunBoxMatcher.Find(new[] { K("gb"), K("gc"), K("ga") }, "  g ", 2).Select(m => m.Keycut.Name));
        }
    }
}
