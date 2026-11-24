using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>
    /// The "Run keycut" Shortcut behind Settings' Spotlight row on macOS (issue #328): <c>k gmail</c> in Spotlight runs the keycut.
    /// Packaging signs <c>installer/macos/Run keycut.plist</c> into the bundle's Resources (<c>tools/mac/sign-spotlight-shortcut.sh</c>);
    /// the app opens that file, Shortcuts imports it, and the user adds the quick key in Spotlight themselves, which the file can't hold.
    /// </summary>
    public static class SpotlightShortcut
    {
        /// <summary>The shortcut's name, which is the signed file's name too.</summary>
        public const string Name = "Run keycut";

        /// <summary>The quick key the row asks the user to add.</summary>
        public const string QuickKey = "k";

        public const string FileName = Name + ".shortcut";

        /// <summary>The folder of the bundle's Resources it ships in.</summary>
        public const string FolderName = "Spotlight";

        /// <summary>The signed file if this Mac can use it and the build has it; otherwise null, which hides the row.</summary>
        public static string FindFile()
        {
            return OperatingSystem.IsMacOS()
                ? Find(BundleResources.Folder(FolderName), OperatingSystem.IsMacOSVersionAtLeast(26), File.Exists)
                : null;
        }

        /// <param name="folder">Where the file would be.</param>
        /// <param name="supportsQuickKeys">Spotlight's quick keys came with macOS 26.</param>
        /// <param name="fileExists">Tests pass a fake.</param>
        public static string Find(string folder, bool supportsQuickKeys, Func<string, bool> fileExists)
        {
            var file = Path.Combine(folder, FileName);

            return supportsQuickKeys && fileExists(file) ? file : null;
        }
    }
}
