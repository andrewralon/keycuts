using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using keycuts.Common;

namespace keycuts.GUI.Themes
{
    /// <summary>One drawing of the app icon: a .ico in Images/AppIcons (from assets/icons, made by tools/New-AppIcons.ps1).</summary>
    public enum AppIconArt
    {
        Porcelain,
        Onyx,
        Cobalt,
        Twilight,
        Classic
    }

    /// <summary>A choice in the Settings icon picker: its name, what it shows, and which drawing it uses in light and dark mode.</summary>
    public sealed class AppIconOption
    {
        public AppIconOption(AppIconChoice choice, string name, string description, AppIconArt lightArt, AppIconArt darkArt)
        {
            Choice = choice;
            Name = name;
            Description = description;
            LightArt = lightArt;
            DarkArt = darkArt;
        }

        public AppIconChoice Choice { get; }

        public string Name { get; }

        public string Description { get; }

        public AppIconArt LightArt { get; }

        public AppIconArt DarkArt { get; }

        /// <summary>True for the options that switch with the theme; the picker shows both of their drawings.</summary>
        public bool FollowsTheme => LightArt != DarkArt;

        /// <summary>The picker's heading for this option's group.</summary>
        public string Group => FollowsTheme ? AppIcons.FollowsThemeGroup : AppIcons.AlwaysTheSameGroup;

        /// <summary>Classic sits alone on the picker's last row.</summary>
        public bool IsWide => Choice == AppIconChoice.Classic;

        public ImageSource LightImage => AppIcons.GetPreview(LightArt);

        public ImageSource DarkImage => AppIcons.GetPreview(DarkArt);
    }

    /// <summary>The app icons: the choices Settings offers, which drawing each one shows, and the images themselves.</summary>
    public static class AppIcons
    {
        public const string FollowsThemeGroup = "FOLLOWS THE THEME";

        public const string AlwaysTheSameGroup = "ALWAYS THE SAME";

        /// <summary>The folder, beside the exe, that the .ico files are copied to, for the shortcuts that point at them.</summary>
        public const string FolderName = "icons";

        private static readonly Dictionary<AppIconArt, BitmapFrame> windowIcons = new();

        private static readonly Dictionary<AppIconArt, BitmapSource> previews = new();

        private static readonly object gate = new();

        /// <summary>In the picker's order.</summary>
        public static IReadOnlyList<AppIconOption> Options { get; } = new[]
        {
            new AppIconOption(AppIconChoice.BlackAndWhite, "Black & White", "Porcelain in light mode, Onyx in dark mode.", AppIconArt.Porcelain, AppIconArt.Onyx),
            new AppIconOption(AppIconChoice.Accent, "Accent", "Cobalt in light mode, Twilight in dark mode.", AppIconArt.Cobalt, AppIconArt.Twilight),
            new AppIconOption(AppIconChoice.Porcelain, "Porcelain", "Charcoal keycap on a light background.", AppIconArt.Porcelain, AppIconArt.Porcelain),
            new AppIconOption(AppIconChoice.Onyx, "Onyx", "White keycap on a dark background.", AppIconArt.Onyx, AppIconArt.Onyx),
            new AppIconOption(AppIconChoice.Cobalt, "Cobalt", "Blue keycap on a light background.", AppIconArt.Cobalt, AppIconArt.Cobalt),
            new AppIconOption(AppIconChoice.Twilight, "Twilight", "Blue keycap on a dark background.", AppIconArt.Twilight, AppIconArt.Twilight),
            new AppIconOption(AppIconChoice.Classic, "Classic", "Team Ralon FTW!", AppIconArt.Classic, AppIconArt.Classic),
        };

        public static AppIconOption Get(AppIconChoice choice)
        {
            return Options.FirstOrDefault(o => o.Choice == choice) ?? Options[0];
        }

        /// <summary>The drawing a choice shows in a light or a dark theme.</summary>
        public static AppIconArt Resolve(AppIconChoice choice, bool light)
        {
            var option = Get(choice);
            return light ? option.LightArt : option.DarkArt;
        }

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

        public static string GetFileName(AppIconArt art)
        {
            return art.ToString().ToLowerInvariant() + ".ico";
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
