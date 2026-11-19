using System;
using System.Runtime.Versioning;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The desktop seam (Phase 2.6): the process starts <see cref="WindowsDesktop"/> makes, without starting any.</summary>
    public class DesktopTests
    {
        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void OnWindows_TheDesktopIsWindowsDesktop()
        {
            Assert.IsType<WindowsDesktop>(Desktops.Current);
        }

        [MacFact]
        public void OnTheMac_TheDesktopIsUnavailableForNow()
        {
            Assert.IsType<UnavailableDesktop>(Desktops.Current);
            Assert.Throws<PlatformNotSupportedException>(() => Desktops.Current.RunKeycut("/tmp/x"));
        }

        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void RunKeycut_ShellExecutesTheFile_WithTheArgumentsAsTyped()
        {
            var start = WindowsDesktop.RunKeycutStart(@"C:\Shortcuts\db.bat", "\"a b\" c");

            Assert.Equal(@"C:\Shortcuts\db.bat", start.FileName);
            Assert.Equal("\"a b\" c", start.Arguments);
            Assert.True(start.UseShellExecute);
            Assert.Equal("", WindowsDesktop.RunKeycutStart(@"C:\Shortcuts\db.bat", null).Arguments);
        }

        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void RevealFile_OpensExplorerWithTheFileSelected()
        {
            var start = WindowsDesktop.RevealFileStart(@"C:\Shortcuts\my db.bat");

            Assert.Equal("explorer.exe", start.FileName);
            Assert.Equal("/select,\"C:\\Shortcuts\\my db.bat\"", start.Arguments);
            Assert.True(start.UseShellExecute);
        }

        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void EditFile_OpensNotepadOnTheQuotedFile()
        {
            var start = WindowsDesktop.EditFileStart(@"C:\Shortcuts\my db.bat");

            Assert.Equal("notepad.exe", start.FileName);
            Assert.Equal("\"C:\\Shortcuts\\my db.bat\"", start.Arguments);
            Assert.True(start.UseShellExecute);
        }

        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void OpenTarget_ShellExecutesIt()
        {
            var start = WindowsDesktop.OpenTargetStart("https://github.com/andrewralon/keycuts/releases");

            Assert.Equal("https://github.com/andrewralon/keycuts/releases", start.FileName);
            Assert.Equal("", start.Arguments);
            Assert.True(start.UseShellExecute);
        }
    }
}
