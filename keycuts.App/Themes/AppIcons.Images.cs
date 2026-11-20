using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace keycuts.GUI.Themes
{
    /// <summary>The Avalonia app's icon images: the window icons and the previews, from the images built in.</summary>
    public static partial class AppIcons
    {
        private static readonly Dictionary<AppIconArt, Bitmap> previews = new();

        private static readonly object gate = new();

        /// <summary>For <c>Window.Icon</c>: the .ico, from which the system picks the frames for the taskbar and Alt+Tab.</summary>
        public static Avalonia.Controls.WindowIcon GetWindowIcon(AppIconArt art)
        {
            var assembly = typeof(AppIcons).Assembly.GetName().Name;
            using var stream = AssetLoader.Open(new Uri($"avares://{assembly}/Images/AppIcons/{GetFileName(art)}"));
            return new Avalonia.Controls.WindowIcon(stream);
        }

        /// <summary>The 256 px image (assets/icons), for showing the icon in the app at any size and DPI.</summary>
        public static Bitmap GetPreview(AppIconArt art)
        {
            lock (gate)
            {
                if (!previews.TryGetValue(art, out var preview))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(GetFileName(art));
                    var assembly = typeof(AppIcons).Assembly.GetName().Name;
                    using var stream = AssetLoader.Open(new Uri($"avares://{assembly}/Images/AppIcons/{name}-256.png"));
                    preview = new Bitmap(stream);
                    previews[art] = preview;
                }

                return preview;
            }
        }
    }
}
