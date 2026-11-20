using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace keycuts.GUI.Themes
{
    /// <summary>The Avalonia app's icon images. The window icons and the picker's come with Settings (Phase 3.8).</summary>
    public static partial class AppIcons
    {
        private static readonly Dictionary<AppIconArt, Bitmap> previews = new();

        private static readonly object gate = new();

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
