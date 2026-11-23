using System;
using System.IO;
using System.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Finder Quick Action (Phase 7): installed into a temp home, never the real ~/Library/Services, and the shipped workflow itself.</summary>
    public class QuickActionInstallerTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "keycuts-qa-" + Guid.NewGuid().ToString("N"));

        private string Home => Path.Combine(_root, "home");

        private string Source => Path.Combine(_root, "source", QuickActionInstaller.WorkflowName);

        public QuickActionInstallerTests()
        {
            Directory.CreateDirectory(Path.Combine(Source, "Contents"));
            File.WriteAllText(Path.Combine(Source, "Contents", "Info.plist"), "info");
            File.WriteAllText(Path.Combine(Source, "Contents", "document.wflow"), "flow");
        }

        public void Dispose()
        {
            Directory.Delete(_root, true);
        }

        [Fact]
        public void Install_CopiesTheWorkflowIntoServices_AndAsksForARefresh()
        {
            var refreshed = 0;
            var installer = new QuickActionInstaller(Home, Source, () => refreshed++);

            Assert.False(installer.IsInstalled);
            Assert.True(installer.Install());

            Assert.True(installer.IsInstalled);
            Assert.Equal(Path.Combine(Home, "Library", "Services", "keycut this!.workflow"), installer.InstalledPath);
            Assert.Equal("flow", File.ReadAllText(Path.Combine(installer.InstalledPath, "Contents", "document.wflow")));
            Assert.Equal(1, refreshed);
        }

        [Fact]
        public void Install_ReplacesACopyThatIsThere()
        {
            var installer = new QuickActionInstaller(Home, Source);
            installer.Install();
            File.WriteAllText(Path.Combine(installer.InstalledPath, "Contents", "document.wflow"), "old");
            File.WriteAllText(Path.Combine(installer.InstalledPath, "Contents", "stale"), "x");

            Assert.True(installer.Install());

            Assert.Equal("flow", File.ReadAllText(Path.Combine(installer.InstalledPath, "Contents", "document.wflow")));
            Assert.False(File.Exists(Path.Combine(installer.InstalledPath, "Contents", "stale")));
        }

        [Fact]
        public void Uninstall_TakesItOut_AndIsFineWhenItIsNotThere()
        {
            var refreshed = 0;
            var installer = new QuickActionInstaller(Home, Source, () => refreshed++);

            Assert.True(installer.Uninstall());
            Assert.Equal(0, refreshed);

            installer.Install();
            Assert.True(installer.Uninstall());
            Assert.False(installer.IsInstalled);
            Assert.Equal(2, refreshed);
        }

        [Fact]
        public void EnsureInstalled_InstallsOnce_AndAgainWhenTheShippedWorkflowChanges()
        {
            var refreshed = 0;
            var installer = new QuickActionInstaller(Home, Source, () => refreshed++);

            Assert.False(installer.IsUpToDate);
            Assert.True(installer.EnsureInstalled());
            Assert.True(installer.IsUpToDate);

            // Already current: nothing is copied and pbs isn't asked again
            Assert.True(installer.EnsureInstalled());
            Assert.Equal(1, refreshed);

            // A new version of the app ships a changed workflow
            File.WriteAllText(Path.Combine(Source, "Contents", "document.wflow"), "newer");
            Assert.False(installer.IsUpToDate);
            Assert.True(installer.EnsureInstalled());
            Assert.Equal("newer", File.ReadAllText(Path.Combine(installer.InstalledPath, "Contents", "document.wflow")));
            Assert.Equal(2, refreshed);
        }

        [Fact]
        public void Install_WithNoWorkflowToCopy_Fails_AndChangesNothing()
        {
            var installer = new QuickActionInstaller(Home, Path.Combine(_root, "missing.workflow"));

            Assert.False(installer.Install());
            Assert.False(installer.IsInstalled);
            Assert.False(Directory.Exists(Path.Combine(Home, "Library")));
        }

        [Fact]
        public void TheShippedWorkflow_OpensTheAppWithTheSelectedItems()
        {
            var flow = File.ReadAllText(RepoFiles.Find("installer/macos/keycut this!.workflow/Contents/document.wflow"));
            var info = File.ReadAllText(RepoFiles.Find("installer/macos/keycut this!.workflow/Contents/Info.plist"));

            // The items arrive as arguments (inputMethod 1) and go to the app by its bundle id, which the running app receives
            Assert.Contains("<string>open -b " + GuiLauncher.BundleId + " \"$@\"</string>", flow);
            Assert.Contains("<key>inputMethod</key>\n\t\t\t\t\t<integer>1</integer>", flow.Replace("\r\n", "\n"));
            Assert.Contains("com.apple.Automator.fileSystemObject", flow);

            // Finder's menu shows it for any item, under the name Settings and the CLI use
            Assert.Contains("<string>keycut this!</string>", info);
            Assert.Contains("com.apple.finder", info);
            Assert.Contains("public.item", info);
        }

        [MacFact]
        public void TheShippedWorkflowFiles_AreValidPropertyLists()
        {
            foreach (var name in new[] { "Info.plist", "document.wflow" })
            {
                var path = RepoFiles.Find("installer/macos/keycut this!.workflow/Contents/" + name);
                var start = new System.Diagnostics.ProcessStartInfo("/usr/bin/plutil", new[] { "-lint", path }.Select(a => "\"" + a + "\"").Aggregate((a, b) => a + " " + b))
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using var process = System.Diagnostics.Process.Start(start);
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }
        }
    }
}
