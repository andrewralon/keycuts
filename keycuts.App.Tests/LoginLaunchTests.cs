using System;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Whether a start is the login's (issue #294): the rules, with the macOS reads faked.</summary>
    public class LoginLaunchTests
    {
        private static readonly TimeSpan Young = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan Old = TimeSpan.FromHours(5);

        [Theory]
        // legacyLoginItem, registered, parentIsLaunchd, session seconds (-1: unknown), expected
        [InlineData(true, false, false, 99999, true)]   // the legacy login-item marker always counts
        [InlineData(false, true, true, 20, true)]       // SMAppService: registered, started by launchd, just after the login
        [InlineData(false, true, true, 179, true)]
        [InlineData(false, true, true, 180, false)]     // a manual launch much later (open, Finder, the Dock)
        [InlineData(false, true, true, 18000, false)]
        [InlineData(false, false, true, 20, false)]     // not registered: a direct or dev launch
        [InlineData(false, true, false, 20, false)]     // started from a shell or an IDE
        [InlineData(false, true, true, -1, false)]      // the session's age couldn't be read
        public void Decide_FollowsTheRules(bool legacy, bool registered, bool launchd, int sessionSeconds, bool expected)
        {
            TimeSpan? age = sessionSeconds < 0 ? null : TimeSpan.FromSeconds(sessionSeconds);

            Assert.Equal(expected, LoginLaunch.Decide(legacy, registered, launchd, age));
        }

        [Theory]
        [InlineData("00:05", 5)]
        [InlineData("12:34", 754)]
        [InlineData("02:03:04", 7384)]
        [InlineData("1-02:03:04", 93784)]
        [InlineData("  00:09 ", 9)]
        public void ParseElapsed_ReadsPsTimes(string text, int seconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(seconds), LoginLaunch.ParseElapsed(text));
        }

        [Theory]
        [InlineData("")]
        [InlineData("5")]
        [InlineData("a:b")]
        [InlineData("1:2:3:4")]
        [InlineData("x-01:02")]
        public void ParseElapsed_ReturnsNull_ForAnythingElse(string text)
        {
            Assert.Null(LoginLaunch.ParseElapsed(text));
        }

        [Fact]
        public void OldestOf_TakesTheFirstNameThatRuns_AndItsOldestProcess()
        {
            const string ps = "   05:00 Dock\n 02:00:10 loginwindow\n 10:00 loginwindow\n   01:00 Finder\n";

            Assert.Equal(TimeSpan.Parse("02:00:10"), LoginLaunch.OldestOf(ps, "loginwindow", "Dock"));
            Assert.Equal(TimeSpan.FromMinutes(5), LoginLaunch.OldestOf(ps, "Dock"));
            Assert.Equal(TimeSpan.FromMinutes(5), LoginLaunch.OldestOf("   05:00 Dock\n", "loginwindow", "Dock"));
            Assert.Null(LoginLaunch.OldestOf(ps, "keycuts"));
            Assert.Null(LoginLaunch.OldestOf(string.Empty, "Dock"));
        }
    }
}
