#nullable enable
using System;
using System.IO;
using System.Runtime.Versioning;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    // PATH as Windows keeps it: the entries split on semicolons, compared without case, with %VARIABLES% expanded (moved from
    // keycuts.Tests, where they ran only because it targets Windows)
    [SupportedOSPlatform("windows")]
    public class PathListTests
    {
        [WindowsFact]
        public void Matches_whole_entries_not_substrings()
        {
            Assert.False(PathList.Contains(@"C:\Shortcuts2;C:\Other", @"C:\Shortcuts"));
            Assert.True(PathList.Contains(@"C:\Other;c:\shortcuts\", @"C:\Shortcuts"));
        }

        [WindowsFact]
        public void Appends_a_new_entry_and_keeps_unexpanded_variables()
        {
            Assert.Equal(@"%USERPROFILE%\bin;C:\Shortcuts", PathList.AddOrReplace(@"%USERPROFILE%\bin;", null, @"C:\Shortcuts"));
        }

        [WindowsFact]
        public void Does_not_add_a_duplicate()
        {
            Assert.Equal(@"C:\Shortcuts\;C:\Other", PathList.AddOrReplace(@"C:\Shortcuts\;C:\Other", null, @"C:\Shortcuts"));
        }

        [WindowsFact]
        public void Replaces_the_old_entry()
        {
            Assert.Equal(@"C:\Other;D:\Keys", PathList.AddOrReplace(@"C:\Shortcuts;C:\Other", @"C:\Shortcuts", @"D:\Keys"));
        }

        [WindowsFact]
        public void Leaves_a_similar_entry_alone_when_replacing()
        {
            Assert.Equal(@"C:\Shortcuts2;D:\Keys", PathList.AddOrReplace(@"C:\Shortcuts2", @"C:\Shortcuts", @"D:\Keys"));
        }

        [WindowsFact]
        public void Handles_an_empty_path()
        {
            Assert.Equal(@"C:\Shortcuts", PathList.AddOrReplace(null!, null!, @"C:\Shortcuts"));
        }

        [WindowsFact]
        public void IsOnPath_ChecksTheUserAndMachinePath_AsWrittenAndExpanded()
        {
            var profile = Environment.GetEnvironmentVariable("USERPROFILE")!;
            var folder = Path.Combine(profile, "Shortcuts");

            Assert.True(UserPathUpdater.IsOnPath(folder, @"C:\Other;" + folder + @"\", null!));
            Assert.True(UserPathUpdater.IsOnPath(folder, @"%USERPROFILE%\Shortcuts", null!));
            Assert.True(UserPathUpdater.IsOnPath(folder, null!, @"C:\Windows;" + folder));
            Assert.False(UserPathUpdater.IsOnPath(folder, @"%USERPROFILE%\Shortcuts2", @"C:\Windows"));
            Assert.False(UserPathUpdater.IsOnPath(folder, null!, null!));
        }
    }
}
