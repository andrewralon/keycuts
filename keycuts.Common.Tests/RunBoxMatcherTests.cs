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
        public void TheTextIsTrimmed_AndTheLimitHolds()
        {
            Assert.Equal(new[] { "ga", "gb" }, RunBoxMatcher.Find(new[] { K("gb"), K("gc"), K("ga") }, "  g ", 2).Select(m => m.Keycut.Name));
        }
    }
}
