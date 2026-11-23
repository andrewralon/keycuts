using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI
{
    /// <summary>
    /// The Dock icon while keycuts runs: the icon chosen in Settings. <c>Window.Icon</c> doesn't reach the Dock on macOS (it showed
    /// the generic icon whatever was picked), so the choice is set on <c>NSApplication</c>. The bundle's own icon (what the Dock shows after a quit) is
    /// set to the same image as its custom icon (<see cref="KeepAfterQuit"/>). The art is drawn on Apple's icon grid first (<see cref="DockIconImage"/>, issue #310): the Dock draws a runtime image as given, so a full-bleed tile looked a fifth too big.
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

        /// <summary>
        /// Sets the icon again. The Dock ignores an icon set while the app was hidden from it (a start at login), so this runs each time
        /// the app comes back to the Dock.
        /// </summary>
        [SupportedOSPlatform("macos")]
        public static void Refresh()
        {
            Apply(AppIconManager.Current);
        }

        [SupportedOSPlatform("macos")]
        private static void Apply(AppIconArt art)
        {
            byte[] png;
            try
            {
                png = DockIconImage.Render(Load(art));
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't draw the Dock icon: " + ex.Message);
                return;
            }

            using (ObjC.AutoreleasePool())
            {
                var handle = GCHandle.Alloc(png, GCHandleType.Pinned);
                try
                {
                    var data = ObjC.Send(ObjC.GetClass("NSData"), ObjC.Selector("dataWithBytes:length:"), handle.AddrOfPinnedObject(), (nuint)png.Length);
                    var image = ObjC.Send(ObjC.Send(ObjC.GetClass("NSImage"), ObjC.Selector("alloc")), ObjC.Selector("initWithData:"), data);

                    if (image != IntPtr.Zero)
                    {
                        var nsApp = ObjC.Send(ObjC.GetClass("NSApplication"), ObjC.Selector("sharedApplication"));
                        ObjC.Send(nsApp, ObjC.Selector("setApplicationIconImage:"), image);
                        KeepAfterQuit(art, image);
                    }
                }
                finally
                {
                    handle.Free();
                }
            }
        }

        /// <summary>
        /// The Dock goes back to the app's own icon when it quits (a pinned tile, Finder), which would be Porcelain whatever was
        /// chosen. So the same image is also set as the custom icon of keycuts.app (<c>NSWorkspace setIcon:forFile:</c>), only
        /// when the drawing changed. It needs the app to be writable by the user (a drag install; a .pkg's is root's, where it
        /// does nothing) and adds an <c>Icon</c> file to the bundle, which the strict signature check doesn't seal. Never fails.
        /// </summary>
        [SupportedOSPlatform("macos")]
        private static void KeepAfterQuit(AppIconArt art, IntPtr image)
        {
            if (art == keptArt || BundlePath() is not { } bundle)
            {
                return;
            }

            var workspace = ObjC.Send(ObjC.GetClass("NSWorkspace"), ObjC.Selector("sharedWorkspace"));
            if (ObjC.SendBool(workspace, ObjC.Selector("setIcon:forFile:options:"), image, ObjC.String(bundle), 0))
            {
                keptArt = art;
            }
        }

        private static AppIconArt? keptArt;

        /// <summary>The <c>.app</c> this runs from, or null for a run from the build folder.</summary>
        private static string? BundlePath()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                if (folder.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                {
                    return folder.FullName;
                }
            }

            return null;
        }

        /// <summary>The 1024 px art beside the app, or the 256 px one built in (the monogram has no larger original).</summary>
        private static Bitmap Load(AppIconArt art)
        {
            var name = Path.GetFileNameWithoutExtension(AppIcons.GetFileName(art));
            var file = Path.Combine(AppContext.BaseDirectory, AppIcons.FolderName, name + "-1024.png");
            return File.Exists(file) ? new Bitmap(file) : AppIcons.GetPreview(art);
        }
    }
}
