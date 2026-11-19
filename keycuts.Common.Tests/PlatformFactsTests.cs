using System;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The platform attributes run where they say and skip elsewhere, so a Windows or macOS run skips the other platform's two.</summary>
    public class PlatformFactsTests
    {
        [WindowsFact]
        public void WindowsFact_RunsOnWindows()
        {
            Assert.True(OperatingSystem.IsWindows());
        }

        [WindowsTheory]
        [InlineData(1)]
        public void WindowsTheory_RunsOnWindows(int row)
        {
            Assert.Equal(1, row);
            Assert.True(OperatingSystem.IsWindows());
        }

        [MacFact]
        public void MacFact_RunsOnMac()
        {
            Assert.True(OperatingSystem.IsMacOS());
        }

        [MacTheory]
        [InlineData(1)]
        public void MacTheory_RunsOnMac(int row)
        {
            Assert.Equal(1, row);
            Assert.True(OperatingSystem.IsMacOS());
        }
    }
}
