using System;
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
        public void GetDefault_HasUserProfile_AndNoTempOrDriveRoots()
        {
            var all = PathVariables.GetDefault();

            Assert.Contains(all, v => v.Name == "USERPROFILE" && v.Value == Environment.GetEnvironmentVariable("USERPROFILE")!.TrimEnd('\\'));
            Assert.DoesNotContain(all, v => v.Name.Equals("TEMP", StringComparison.OrdinalIgnoreCase) || v.Name.Equals("TMP", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(all, v => System.IO.Path.GetPathRoot(v.Value) == v.Value);
            Assert.Equal(all.Count, all.Select(v => v.Name.ToUpperInvariant()).Distinct().Count());
        }
    }
}
