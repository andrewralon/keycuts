using System;
using System.IO;
using System.Runtime.Versioning;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI
{
    /// <summary>
    /// The Dock icon while keycuts runs: the icon chosen in Settings. <c>Window.Icon</c> doesn't reach the Dock on macOS (it showed
    /// the generic icon whatever was picked), so the choice is set on <c>NSApplication</c>. The bundle's own icon stays fixed, since
    /// changing it would break the signature. The .ico files beside the app are the ones Windows shortcuts use; AppKit reads them.
    /// </summary>
    public static class MacDockIcon
    {
        /// <summary>Shows the current icon now and after each change of the choice, the transparent setting or the theme.</summary>
        [SupportedOSPlatform("macos")]
        public static void Follow()
        {
            Apply(AppIconManager.Current);
            AppIconManager.Changed += (sender, e) => Apply(AppIconManager.Current);
        }

        [SupportedOSPlatform("macos")]
        private static void Apply(AppIconArt art)
        {
            var file = Path.Combine(AppContext.BaseDirectory, AppIcons.FolderName, AppIcons.GetFileName(art));

            if (!File.Exists(file))
            {
                return;
            }

            using (ObjC.AutoreleasePool())
            {
                var image = ObjC.Send(ObjC.Send(ObjC.GetClass("NSImage"), ObjC.Selector("alloc")), ObjC.Selector("initWithContentsOfFile:"), ObjC.String(file));

                if (image != IntPtr.Zero)
                {
                    var nsApp = ObjC.Send(ObjC.GetClass("NSApplication"), ObjC.Selector("sharedApplication"));
                    ObjC.Send(nsApp, ObjC.Selector("setApplicationIconImage:"), image);
                }
            }
        }
    }
}
