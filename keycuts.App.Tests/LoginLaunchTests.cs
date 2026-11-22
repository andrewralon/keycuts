using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Whether a start is the login's (issue #294): the rules, with the macOS reads faked.</summary>
    public class LoginLaunchTests
    {
        [Theory]
        // eventPresent, legacyLoginItem, registered, parentIsLaunchd, expected
        [InlineData(true, true, false, false, true)]    // the legacy login-item marker always counts
        [InlineData(true, false, true, true, false)]    // Finder, the Dock or open: a launch event without the marker
        [InlineData(false, false, true, true, true)]    // SMAppService: no event, registered, started by launchd
        [InlineData(false, false, false, true, false)]  // not registered: a direct or dev launch
        [InlineData(false, false, true, false, false)]  // started from a shell or an IDE
        [InlineData(false, false, false, false, false)]
        public void Decide_FollowsTheRules(bool eventPresent, bool legacy, bool registered, bool launchd, bool expected)
        {
            Assert.Equal(expected, LoginLaunch.Decide(eventPresent, legacy, registered, launchd));
        }
    }
}
