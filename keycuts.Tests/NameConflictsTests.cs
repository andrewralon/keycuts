using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#153: what else a keycut's name opens from Win+R. Every input is a fake; nothing reads the registry or disk.</summary>
    public class NameConflictsTests
    {
        const string Windows = @"C:\Windows";

        const string System32 = @"C:\Windows\System32";

        const string Output = @"C:\Users\me\Shortcuts";

        const string Before = @"C:\Tools\Before";

        const string After = @"C:\Tools\After";

        const string Apps = @"C:\Users\me\AppData\Local\Microsoft\WindowsApps";

        static NameConflictEnvironment Environment(IEnumerable<string> files, string userPath = null, string machinePath = null,
            string pathExt = null, Dictionary<string, string> appPaths = null)
        {
            var set = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

            return new NameConflictEnvironment
            {
                WindowsFolder = Windows,
                SystemFolder = System32,
                MachinePath = machinePath ?? System32 + ";" + Windows + ";" + Before,
                UserPath = userPath ?? Output + ";" + After + ";" + Apps,
                PathExt = pathExt,
                FileExists = set.Contains,
                ExpandVariables = value => value.Replace("%LOCALAPPDATA%", @"C:\Users\me\AppData\Local"),
                FindAppPath = name => appPaths != null && appPaths.TryGetValue(name, out var path) ? path : null
            };
        }

        static IReadOnlyList<NameConflict> Find(string name, params string[] files)
        {
            return NameConflicts.Find(name, Output, Environment(files));
        }

        [Fact]
        public void NothingElseWithTheName_IsNoConflict()
        {
            Assert.Empty(Find("gmail", Path.Combine(Output, "gmail.bat")));
        }

        [Fact]
        public void TheKeycutsOwnBat_IsLeftToTheOverwriteCheck()
        {
            Assert.Empty(Find("db", Path.Combine(Output, "db.bat")));
        }

        [Fact]
        public void TheWindowsFolder_HidesTheKeycut()
        {
            var conflict = Assert.Single(Find("notepad", @"C:\Windows\notepad.exe"));

            Assert.Equal(@"C:\Windows\notepad.exe", conflict.Path);
            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
            Assert.Equal(NameConflictSource.WindowsFolder, conflict.Source);
        }

        [Fact]
        public void System32_HidesTheKeycut_AndIsFoundOnceThoughItIsAlsoOnPath()
        {
            var conflict = Assert.Single(Find("calc", @"C:\Windows\System32\calc.exe"));

            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
            Assert.Equal(NameConflictSource.SystemFolder, conflict.Source);
        }

        [Fact]
        public void WindowsFolderAndSystem32_AreSearchedInThatOrder()
        {
            var conflicts = Find("notepad", @"C:\Windows\System32\notepad.exe", @"C:\Windows\notepad.exe");

            Assert.Equal(new[] { NameConflictSource.WindowsFolder, NameConflictSource.SystemFolder }, conflicts.Select(c => c.Source));
        }

        [Fact]
        public void APathFolderBeforeTheOutputFolder_HidesTheKeycut()
        {
            var conflict = Assert.Single(Find("tool", @"C:\Tools\Before\tool.cmd"));

            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
            Assert.Equal(NameConflictSource.PathBefore, conflict.Source);
        }

        [Fact]
        public void APathFolderAfterTheOutputFolder_IsShadowed()
        {
            var conflict = Assert.Single(Find("tool", @"C:\Tools\After\tool.exe"));

            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
            Assert.Equal(NameConflictSource.PathAfter, conflict.Source);
        }

        [Fact]
        public void AnAppExecutionAlias_IsShadowed()
        {
            var conflict = Assert.Single(Find("winget", Path.Combine(Apps, "winget.exe")));

            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
            Assert.Equal(NameConflictSource.PathAfter, conflict.Source);
        }

        [Fact]
        public void TheMachinePath_ComesBeforeTheUserPath()
        {
            // The output folder is on the user PATH, so everything on the machine PATH is searched first
            var env = Environment(new[] { @"C:\Tools\Machine\tool.exe" }, machinePath: @"C:\Tools\Machine");

            var conflict = Assert.Single(NameConflicts.Find("tool", Output, env));
            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
        }

        [Theory]
        [InlineData("tool.com")]
        [InlineData("tool.exe")]
        public void InTheOutputFolder_AnExtensionTriedBeforeBat_HidesTheKeycut(string file)
        {
            var conflict = Assert.Single(Find("tool", Path.Combine(Output, file), Path.Combine(Output, "tool.bat")));

            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
            Assert.Equal(NameConflictSource.OutputFolder, conflict.Source);
        }

        [Fact]
        public void InTheOutputFolder_AnExtensionTriedAfterBat_IsShadowed()
        {
            var conflict = Assert.Single(Find("tool", Path.Combine(Output, "tool.cmd")));

            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
            Assert.Equal(NameConflictSource.OutputFolder, conflict.Source);
        }

        [Fact]
        public void EveryPathExtExtension_IsTried_InPathExtOrder()
        {
            var conflicts = Find("x", @"C:\Tools\After\x.vbs", @"C:\Tools\After\x.com", @"C:\Tools\After\x.msc");

            Assert.Equal(new[] { @"C:\Tools\After\x.com", @"C:\Tools\After\x.vbs", @"C:\Tools\After\x.msc" }, conflicts.Select(c => c.Path));
        }

        [Fact]
        public void ACustomPathExt_DecidesTheOrder_AndWhatCounts()
        {
            var env = Environment(new[] { Path.Combine(Output, "x.py"), Path.Combine(Output, "x.exe") }, pathExt: ".PY;.BAT");

            var conflict = Assert.Single(NameConflicts.Find("x", Output, env));
            Assert.Equal(Path.Combine(Output, "x.py"), conflict.Path);
            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
        }

        [Fact]
        public void ParsePathExt_AddsMissingDots_AndDropsDuplicatesAndBlanks()
        {
            Assert.Equal(new[] { ".COM", ".EXE", ".PY" }, NameConflicts.ParsePathExt(" .com;EXE;;.Com; .py "));
            Assert.Equal(NameConflicts.ParsePathExt(NameConflicts.DefaultPathExt), NameConflicts.ParsePathExt(null));
        }

        [Fact]
        public void AppPaths_IsShadowed_AndReportsTheRegisteredProgram()
        {
            const string word = @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE";
            var env = Environment(new[] { word }, appPaths: new Dictionary<string, string> { ["winword.exe"] = "\"" + word + "\"" });

            var conflict = Assert.Single(NameConflicts.Find("winword", Output, env));

            Assert.Equal(word, conflict.Path);
            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
            Assert.Equal(NameConflictSource.AppPaths, conflict.Source);
        }

        [Fact]
        public void AppPaths_ComesLast()
        {
            const string word = @"C:\Office\WINWORD.EXE";
            var env = Environment(new[] { word, @"C:\Tools\After\winword.cmd" }, appPaths: new Dictionary<string, string> { ["winword.exe"] = word });

            Assert.Equal(new[] { NameConflictSource.PathAfter, NameConflictSource.AppPaths }, NameConflicts.Find("winword", Output, env).Select(c => c.Source));
        }

        [Fact]
        public void AnAppPathsEntryForAMissingProgram_IsIgnored()
        {
            var env = Environment(Array.Empty<string>(), appPaths: new Dictionary<string, string> { ["gone.exe"] = @"C:\Gone\gone.exe" });

            Assert.Empty(NameConflicts.Find("gone", Output, env));
        }

        [Fact]
        public void AKeycutInAnotherFolder_StillCounts()
        {
            var conflict = Assert.Single(Find("db", @"C:\Tools\Before\db.bat"));

            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
        }

        [Fact]
        public void AnOutputFolderThatIsNotOnPath_CountsAsTheLastEntry()
        {
            var env = Environment(new[] { @"C:\Tools\After\tool.exe" }, userPath: After);

            var conflict = Assert.Single(NameConflicts.Find("tool", Output, env));
            Assert.Equal(NameConflictEffect.Hidden, conflict.Effect);
        }

        [Fact]
        public void PathEntries_AreExpanded_Unquoted_AndComparedWithoutATrailingSlash()
        {
            var env = Environment(new[] { Path.Combine(Apps, "code.exe") },
                userPath: "\"" + Output + "\\\"; ;relative;%LOCALAPPDATA%\\Microsoft\\WindowsApps");

            var conflict = Assert.Single(NameConflicts.Find("code", Output + @"\", env));
            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
        }

        [Fact]
        public void APathVariableHoldingSeveralFolders_IsExpandedBeforeSplitting()
        {
            var env = Environment(new[] { @"C:\Tools\B\tool.exe" }, userPath: Output + ";%TOOLS%");
            env.ExpandVariables = value => value.Replace("%TOOLS%", @"C:\Tools\A;C:\Tools\B");

            var conflict = Assert.Single(NameConflicts.Find("tool", Output, env));
            Assert.Equal(@"C:\Tools\B\tool.exe", conflict.Path);
            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);
        }

        [Theory]
        [InlineData(@"C:\Users\me\Shortcuts\.")]
        [InlineData(@"C:\Users\me\Other\..\Shortcuts")]
        [InlineData(@"c:\users\me\shortcuts\")]
        public void TheOutputFolder_IsMatchedOnPath_HoweverItIsSpelled(string outputFolder)
        {
            var env = Environment(new[] { Path.Combine(Output, "db.bat"), @"C:\Tools\After\db.exe" });

            var conflict = Assert.Single(NameConflicts.Find("db", outputFolder, env));
            Assert.Equal(NameConflictEffect.Shadows, conflict.Effect);   // not hidden: the output folder is found on PATH
        }

        [Fact]
        public void APathFolderListedTwiceInDifferentSpellings_IsSearchedOnce()
        {
            var env = Environment(new[] { @"C:\Tools\After\tool.exe" }, userPath: Output + @";C:\Tools\After;C:\Tools\After\.\");

            Assert.Single(NameConflicts.Find("tool", Output, env));
        }

        [Fact]
        public void WithoutBatInPathExt_TheKeycutNeverRuns_SoEveryMatchHidesIt()
        {
            const string word = @"C:\Office\WINWORD.EXE";
            var env = Environment(new[] { word, @"C:\Tools\After\winword.cmd", Path.Combine(Output, "winword.cmd") },
                pathExt: ".EXE;.CMD", appPaths: new Dictionary<string, string> { ["winword.exe"] = word });

            var conflicts = NameConflicts.Find("winword", Output, env);

            Assert.Equal(3, conflicts.Count);
            Assert.All(conflicts, c => Assert.Equal(NameConflictEffect.Hidden, c.Effect));
        }

        [Fact]
        public void MovingTheOutputFolder_TakesTheOldOneOffTheUserPath_AndAddsTheNewOne()
        {
            var env = Environment(new[] { @"C:\Users\me\Shortcuts\foo.bat" }, userPath: Output + ";" + After);

            var moved = env.WithOutputFolderMoved(Output, @"D:\Keycuts");

            Assert.Equal(After + @";D:\Keycuts", moved.UserPath);
            Assert.Equal(env.MachinePath, moved.MachinePath);
            Assert.Equal(Output + ";" + After, env.UserPath);   // the original is left alone
            Assert.Empty(NameConflicts.Find("foo", @"D:\Keycuts", moved));   // the old folder's foo.bat is off PATH
        }

        [Fact]
        public void MovingTheOutputFolder_DoesNotAddOneTheMachinePathHas()
        {
            var env = Environment(Array.Empty<string>(), userPath: Output, machinePath: @"D:\Keycuts");

            Assert.Equal(string.Empty, env.WithOutputFolderMoved(Output, @"D:\Keycuts").UserPath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"C:\Users\me\Shortcuts\")]
        public void NotMovingTheOutputFolder_ChangesNothing(string newFolder)
        {
            var env = Environment(Array.Empty<string>());

            Assert.Equal(env.UserPath, env.WithOutputFolderMoved(Output, newFolder).UserPath);
        }

        [Theory]
        [InlineData(Windows)]
        [InlineData(System32)]
        public void TheKeycutsOwnBat_IsSkipped_EvenWhenTheOutputFolderIsSearchedEarly(string outputFolder)
        {
            var env = Environment(new[] { Path.Combine(outputFolder, "db.bat") });

            Assert.Empty(NameConflicts.Find("db", outputFolder, env));
        }

        [Fact]
        public void ANameThatAlreadyEndsWithAPathExtExtension_IsAlsoTriedAsTyped()
        {
            var conflict = Assert.Single(Find("tool.exe", @"C:\Tools\After\tool.exe"));

            Assert.Equal(@"C:\Tools\After\tool.exe", conflict.Path);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        [InlineData("a/b")]
        public void AnEmptyOrUnusableName_HasNoConflicts(string name)
        {
            Assert.Empty(NameConflicts.Find(name, Output, Environment(new[] { @"C:\Windows\a.exe" })));
        }

        [Fact]
        public void GetMain_PrefersTheFirstConflictThatHidesTheKeycut()
        {
            var shadows = new NameConflict(@"C:\a.exe", NameConflictEffect.Shadows, NameConflictSource.AppPaths);
            var hidden = new NameConflict(@"C:\b.exe", NameConflictEffect.Hidden, NameConflictSource.PathBefore);

            Assert.Same(hidden, NameConflicts.GetMain(new[] { shadows, hidden }));
            Assert.Same(shadows, NameConflicts.GetMain(new[] { shadows }));
            Assert.Null(NameConflicts.GetMain(Array.Empty<NameConflict>()));
        }
    }
}
