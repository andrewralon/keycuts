using System;
using System.Collections.Generic;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#31: saving file and folder paths with environment variables. Every test passes its own variables.</summary>
    public class PathVariablesTests
    {
        private static readonly PathVariable[] Vars =
        {
            new PathVariable("LOCALAPPDATA", @"C:\Users\me\AppData\Local"),
            new PathVariable("USERPROFILE", @"C:\Users\me"),
            new PathVariable("ProgramFiles", @"C:\Program Files"),
            new PathVariable("ProgramFiles(x86)", @"C:\Program Files (x86)"),
        };

        [Theory]
        [InlineData(@"C:\Users\me\Desktop\notes.txt", @"%USERPROFILE%\Desktop\notes.txt")]
        [InlineData(@"C:\Users\me\AppData\Local\Programs\app.exe", @"%LOCALAPPDATA%\Programs\app.exe")]   // longest wins
        [InlineData(@"c:\users\ME\Desktop", @"%USERPROFILE%\Desktop")]                                     // any case
        [InlineData(@"C:\Users\me", "%USERPROFILE%")]                                                      // the folder itself
        [InlineData(@"C:\Program Files (x86)\asdf", @"%ProgramFiles(x86)%\asdf")]                          // the example in #31
        [InlineData(@"C:\Program Files\app\app.exe", @"%ProgramFiles%\app\app.exe")]
        public void Substitute_ReplacesTheLongestMatchingFolder(string path, string expected)
        {
            Assert.Equal(expected, PathVariables.Substitute(path, Vars));
        }

        [Theory]
        [InlineData(@"C:\Users\meow\Desktop")]          // a prefix, but not at a folder boundary
        [InlineData(@"D:\Data\file.txt")]               // nothing matches
        [InlineData(@"%USERPROFILE%\Desktop")]          // already a variable
        [InlineData(@"\\server\share\file.txt")]
        [InlineData("")]
        public void Substitute_LeavesOtherPathsAlone(string path)
        {
            Assert.Equal(path, PathVariables.Substitute(path, Vars));
        }

        [Fact]
        public void Substitute_ATieGoesToTheEarlierVariable()
        {
            var tied = new[] { new PathVariable("OneDrive", @"C:\Users\me\OneDrive"), new PathVariable("OneDriveConsumer", @"C:\Users\me\OneDrive") };

            Assert.Equal(@"%OneDrive%\doc.txt", PathVariables.Substitute(@"C:\Users\me\OneDrive\doc.txt", tied));
        }

        [Theory]
        [InlineData("1DATA")]      // %1 is the first argument
        [InlineData("*DATA")]      // %* is all the arguments
        [InlineData("~DATA")]      // %~ is an argument modifier
        [InlineData("DA:TA")]      // %VAR:x=y% is cmd's replace syntax
        [InlineData("DATA\r\nnotepad")]  // a line break would add a command to the .bat
        [InlineData("DA\"TA")]     // a quote would end the launch line's quoted path
        [InlineData("DA&TA")]
        [InlineData("DATA ")]
        public void Substitute_SkipsNamesABatchFileWouldMisread_AndUsesTheNextBestOrTheLiteralPath(string name)
        {
            var variables = new[] { new PathVariable("USERPROFILE", @"C:\Users\me"), new PathVariable(name, @"C:\Users\me\Data") };

            Assert.Equal(@"%USERPROFILE%\Data\a.txt", PathVariables.Substitute(@"C:\Users\me\Data\a.txt", variables));
            Assert.Equal(@"D:\Data\a.txt", PathVariables.Substitute(@"D:\Data\a.txt", new[] { new PathVariable(name, @"D:\Data") }));
        }

        [Theory]
        [InlineData(ShortcutType.File, true)]
        [InlineData(ShortcutType.Folder, true)]
        [InlineData(ShortcutType.HostsFile, true)]
        [InlineData(ShortcutType.Url, false)]
        [InlineData(ShortcutType.Command, false)]
        [InlineData(ShortcutType.CLSIDKey, false)]
        public void AppliesTo_FilesAndFoldersOnly(ShortcutType type, bool expected)
        {
            Assert.Equal(expected, PathVariables.AppliesTo(type));
        }

        [Fact]
        public void GetDefault_KnownThenExistingCustomFolders_WithoutTempDriveRootsOrDuplicates()
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["USERPROFILE"] = @"C:\Users\me\",              // trailing separator trimmed
                ["APPDATA"] = @"C:\Users\me\AppData\Roaming",
                ["ProgramFiles"] = "relative",                   // not fully qualified
                ["DROPBOX"] = @"C:\Users\me\Dropbox",
                ["Archive"] = @"\\server\share\Archive",         // a share, but its folder exists
                ["GONE"] = @"C:\Gone",                           // folder doesn't exist
                ["TEMP"] = @"C:\Users\me\AppData\Local\Temp",    // never used
                ["DATA"] = @"D:\",                               // a drive root
                ["LIST"] = @"C:\a;C:\b",                         // a list, not a folder
                ["SPACED"] = @"C:\Users\me\Dropbox ",            // expands with the space, so it can't stand for the folder
                ["SLASHED"] = "C:/Users/me/Dropbox",             // would save a mixed-separator path
            };
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\Users\me\Dropbox", @"\\server\share\Archive", @"D:\" };
            var asked = new List<string>();

            var all = PathVariables.GetDefault(
                name => environment.TryGetValue(name, out var value) ? value : null!,
                new[] { "TEMP", "GONE", "DROPBOX", "DATA", "LIST", "Archive", "dropbox", "SPACED", "SLASHED" },
                folder => { asked.Add(folder); return existing.Contains(folder); });

            Assert.Equal(
                new[] { @"APPDATA=C:\Users\me\AppData\Roaming", @"USERPROFILE=C:\Users\me", @"Archive=\\server\share\Archive", @"DROPBOX=C:\Users\me\Dropbox" },
                all.Select(v => v.Name + "=" + v.Value).ToArray());

            // Only custom variables are checked on disk, and only once they've passed the cheap checks
            Assert.Equal(new[] { @"\\server\share\Archive", @"C:\Users\me\Dropbox", @"C:\Gone" }, asked.ToArray());
        }

        [Fact]
        public void GetDefault_AShareRootCanBeAVariable_ButADriveRootCant()
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NAS"] = @"\\server\share\",    // one named place, like a folder
                ["DATA"] = @"D:\",               // a whole drive
                ["DEVICE"] = @"\\?\E:\",         // a whole drive, written as a device path
            };

            var all = PathVariables.GetDefault(
                name => environment.TryGetValue(name, out var value) ? value : null!,
                environment.Keys,
                folder => true);

            Assert.Equal(new[] { @"NAS=\\server\share" }, all.Select(v => v.Name + "=" + v.Value).ToArray());
            Assert.Equal(@"%NAS%\docs\a.txt", PathVariables.Substitute(@"\\server\share\docs\a.txt", all));
        }
    }
}
