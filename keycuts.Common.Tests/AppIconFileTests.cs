#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The app icon files in assets/icons, the installer's icon, and repointing the app's shortcuts (Common's ShortcutIcons).
    /// Moved from keycuts.Tests at the Avalonia cutover; the app's own icon tests are keycuts.App.Tests' AppIconTests.
    /// </summary>
    public class AppIconFileTests : IDisposable
    {
        public static readonly int[] IcoSizes = { 16, 20, 24, 32, 40, 48, 64, 96, 256 };

        private readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-icon-tests-" + Guid.NewGuid().ToString("N"));

        public AppIconFileTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        // The icons the app ships: every size the title bar, the taskbar and Explorer ask for at 100-200 percent scaling, 32-bit,
        // with 256 as PNG and the rest as bitmaps (tools/New-AppIcons.ps1)
        [Theory]
        [InlineData("porcelain")]
        [InlineData("onyx")]
        [InlineData("cobalt")]
        [InlineData("twilight")]
        [InlineData("classic")]
        [InlineData("key-white")]
        [InlineData("key-charcoal")]
        [InlineData("key-blue")]
        [InlineData("classic-transparent")]
        public void IconFiles_HaveEverySizeAsA32BitFrame_AndAPngPerSize(string name)
        {
            var folder = Path.Combine(FindRepoRoot(), "assets", "icons", name);
            var frames = ReadIcoDirectory(Path.Combine(folder, name + ".ico"));

            Assert.Equal(IcoSizes, frames.Select(f => f.Side));
            Assert.All(frames, f => Assert.Equal(32, f.Bits));
            Assert.All(frames, f => Assert.Equal(f.Side == 256, f.IsPng));

            foreach (var side in IcoSizes)
            {
                Assert.True(File.Exists(Path.Combine(folder, $"{name}-{side}.png")), $"{name}-{side}.png is missing");
            }

            // The original icon has no big version; the keycaps have one
            Assert.Equal(!name.StartsWith("classic", StringComparison.Ordinal), File.Exists(Path.Combine(folder, $"{name}-1024.png")));
        }

        // The installer's icon (Start menu shortcut, Installed apps) is the default's light drawing, as the exe's is
        [Fact]
        public void TheInstaller_UsesPorcelain()
        {
            var repo = FindRepoRoot();
            var expected = Path.GetFullPath(Path.Combine(repo, "assets", "icons", "porcelain", "porcelain.ico"));
            var package = XDocument.Load(Path.Combine(repo, "installer", "Package.wxs"));
            var icon = package.Descendants().Single(e => e.Name.LocalName == "Icon" && (string?)e.Attribute("Id") == "keycuts.ico");
            Assert.Equal(expected, Path.GetFullPath(Path.Combine(repo, "installer", ((string)icon.Attribute("SourceFile")!).Replace('\\', Path.DirectorySeparatorChar))));
        }

        #region Shortcuts

        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void PointAt_ChangesOnlyShortcutsToTheExe_AndOnlyOnce()
        {
            var exe = Path.Combine(root, "app", "keycuts.GUI.exe");
            var other = Path.Combine(root, "app", "other.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
            File.WriteAllText(exe, string.Empty);
            File.WriteAllText(other, string.Empty);
            var icon = Path.Combine(root, "app", "icons", "cobalt.ico");

            var startMenu = Path.Combine(root, "Start Menu");
            var taskbar = Path.Combine(root, "TaskBar");
            var ours = CreateShortcut(Path.Combine(startMenu, "keycuts.lnk"), exe);
            var pinned = CreateShortcut(Path.Combine(taskbar, "keycuts.lnk"), exe);
            var theirs = CreateShortcut(Path.Combine(startMenu, "other.lnk"), other);
            var before = ShortcutIcons.GetIconLocation(theirs);
            var folders = new[] { startMenu, taskbar, Path.Combine(root, "missing"), string.Empty };

            var changed = ShortcutIcons.PointAt(folders, exe, icon);

            Assert.Equal(new[] { ours, pinned }.OrderBy(p => p), changed.OrderBy(p => p));
            Assert.Equal(icon + ",0", ShortcutIcons.GetIconLocation(ours));
            Assert.Equal(icon + ",0", ShortcutIcons.GetIconLocation(pinned));
            Assert.Equal(before, ShortcutIcons.GetIconLocation(theirs));

            // Already showing it: nothing is written again
            Assert.Empty(ShortcutIcons.PointAt(folders, exe, icon));
        }

        [WindowsFact]
        [SupportedOSPlatform("windows")]
        public void PointAt_SkipsABrokenShortcut_AndStillChangesTheOthers()
        {
            var exe = Path.Combine(root, "keycuts.GUI.exe");
            File.WriteAllText(exe, string.Empty);
            var folder = Path.Combine(root, "Start Menu");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "broken.lnk"), "not a shortcut");
            var good = CreateShortcut(Path.Combine(folder, "keycuts.lnk"), exe);

            var changed = ShortcutIcons.PointAt(new[] { folder }, exe, Path.Combine(root, "porcelain.ico"));

            Assert.Equal(new[] { good }, changed);
        }

        [SupportedOSPlatform("windows")]
        private static string CreateShortcut(string path, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = target;
            shortcut.Save();
            return path;
        }

        #endregion

        internal static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("keycuts.sln not found above " + AppContext.BaseDirectory);
        }

        private record IcoFrame(int Side, int Bits, bool IsPng);

        private static List<IcoFrame> ReadIcoDirectory(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
            Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
            var frames = new List<IcoFrame>();
            for (var i = 0; i < BitConverter.ToUInt16(bytes, 4); i++)
            {
                var at = 6 + 16 * i;
                var offset = BitConverter.ToInt32(bytes, at + 12);
                frames.Add(new IcoFrame(bytes[at] == 0 ? 256 : bytes[at], BitConverter.ToUInt16(bytes, at + 6), bytes[offset] == 0x89 && bytes[offset + 1] == (byte)'P'));
            }

            return frames;
        }
    }
}
