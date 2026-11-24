using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Spotlight shortcut (#328): when it is offered, and what the template that packaging signs says.</summary>
    public class SpotlightShortcutTests
    {
        private const string Folder = "/Applications/keycuts.app/Contents/Resources/Spotlight";

        [Fact]
        public void Find_NeedsMacOS26AndTheFile()
        {
            var file = Path.Combine(Folder, "Run keycut.shortcut");

            Assert.Equal(file, SpotlightShortcut.Find(Folder, true, path => path == file));
            Assert.Null(SpotlightShortcut.Find(Folder, false, path => path == file));
            Assert.Null(SpotlightShortcut.Find(Folder, true, path => false));
        }

        [Fact]
        public void FindFile_IsNull_OffMacOS_AndInADevBuild()
        {
            // A dev build's folder beside the executable has no Spotlight folder
            Assert.True(!System.OperatingSystem.IsMacOS() || SpotlightShortcut.FindFile() == null || File.Exists(SpotlightShortcut.FindFile()));
        }

        // The signed file is made from the plist by --refresh, which records the plist's SHA-256 beside it: an edited plist can't ship unsigned
        [Fact]
        public void TheSignedFile_WasMadeFromTheCurrentTemplate()
        {
            var plist = File.ReadAllBytes(RepoFiles.Find("installer/macos/Run keycut.plist")).Where(b => b != (byte)'\r').ToArray();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(plist)).ToLowerInvariant();
            var info = File.ReadAllLines(RepoFiles.Find("installer/macos/Run keycut.shortcut.info"));

            Assert.Contains("plist-sha256=" + hash, info);
            Assert.Matches(@"^expires=\d{4}-\d{2}-\d{2}$", info.Single(l => l.StartsWith("expires=")));

            // An Apple Encrypted Archive, as `shortcuts sign` writes
            var signed = File.ReadAllBytes(RepoFiles.Find("installer/macos/Run keycut.shortcut"));
            Assert.Equal("AEA1", System.Text.Encoding.ASCII.GetString(signed, 0, 4));
        }

        // The template is a property list: the keys Shortcuts reads for Show in Spotlight and Receive Input from Spotlight, and one shell step
        // that passes the typed name to the output folder's keycut
        [Fact]
        public void TheTemplate_ShowsInSpotlight_AndRunsTheNamedKeycutFromTheOutputFolder()
        {
            var plist = XDocument.Load(RepoFiles.Find("installer/macos/Run keycut.plist"));
            var strings = plist.Descendants("string").Select(e => e.Value).ToList();

            Assert.Contains("WFWorkflowTypeShowInSearch", strings);
            Assert.Contains("WFWorkflowTypeReceivesInputFromSearch", strings);
            Assert.Contains("is.workflow.actions.runshellscript", strings);
            Assert.Contains("as arguments", strings);

            Assert.Contains("/bin/sh", strings); // the Shell, so the keycut's own execute bit never matters
            var script = Assert.Single(strings, s => s.Contains("OutputFolder"));
            Assert.Contains("$HOME/Shortcuts", script);
            Assert.Contains("[ -n \"$1\" ]", script);
            Assert.Contains("/bin/sh \"$d/$1\"", script);
        }
    }
}
