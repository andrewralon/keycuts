using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace keycuts.GUI.Themes
{
    /// <summary>The WPF app's images of an icon choice, for the Settings picker.</summary>
    public sealed partial class AppIconOption
    {
        public ImageSource LightImage => AppIcons.GetPreview(LightArt);

        public ImageSource DarkImage => AppIcons.GetPreview(DarkArt);

        /// <summary>The drawings without their background, which the picker shows while the icon is transparent.</summary>
        public ImageSource LightTransparentImage => AppIcons.GetPreview(AppIcons.GetTransparent(LightArt));

        public ImageSource DarkTransparentImage => AppIcons.GetPreview(AppIcons.GetTransparent(DarkArt));
    }

    /// <summary>The WPF app's icon images: the window icons and the picker's previews, from the .ico files built in.</summary>
    public static partial class AppIcons
    {
        private static readonly Dictionary<AppIconArt, BitmapFrame> windowIcons = new();

        private static readonly Dictionary<AppIconArt, BitmapSource> previews = new();

        private static readonly object gate = new();

        /// <summary>
        /// Whether the app is in a light theme, for the options that follow it. High contrast has no light or dark of its own, so
        /// its window background decides: a black one (High Contrast Black, Aquatic) counts as dark.
        /// </summary>
        public static bool IsLight(ThemeVariant variant, Color highContrastWindow)
        {
            return variant switch
            {
                ThemeVariant.Light => true,
                ThemeVariant.Dark => false,
                _ => (0.2126 * highContrastWindow.R + 0.7152 * highContrastWindow.G + 0.0722 * highContrastWindow.B) / 255 >= 0.5
            };
        }

        public static Uri GetPackUri(AppIconArt art)
        {
            return new Uri($"pack://application:,,,/keycuts.GUI;component/Images/AppIcons/{GetFileName(art)}", UriKind.Absolute);
        }

        /// <summary>
        /// For <c>Window.Icon</c>: a frame of the .ico's decoder, from which WPF picks the frames nearest the title bar's small
        /// size and the taskbar and Alt+Tab's large one at the current DPI.
        /// </summary>
        public static BitmapFrame GetWindowIcon(AppIconArt art)
        {
            lock (gate)
            {
                if (!windowIcons.TryGetValue(art, out var icon))
                {
                    icon = BitmapFrame.Create(GetPackUri(art), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    icon.Freeze();
                    windowIcons[art] = icon;
                }

                return icon;
            }
        }

        /// <summary>The largest frame (256 px), for showing the icon in the app at any size and DPI.</summary>
        public static BitmapSource GetPreview(AppIconArt art)
        {
            lock (gate)
            {
                if (!previews.TryGetValue(art, out var preview))
                {
                    var decoder = BitmapDecoder.Create(GetPackUri(art), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    preview = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                    preview.Freeze();
                    previews[art] = preview;
                }

                return preview;
            }
        }
    }
}
