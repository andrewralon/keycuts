using System.Collections.Generic;
using System.Linq;
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
        Classic,

        /// <summary>Onyx's white keycap alone, on a transparent background.</summary>
        KeyWhite,

        /// <summary>Porcelain's charcoal keycap alone.</summary>
        KeyCharcoal,

        /// <summary>Cobalt's and Twilight's blue keycap alone.</summary>
        KeyBlue,

        /// <summary>Classic's monogram without its white square.</summary>
        ClassicTransparent
    }

    /// <summary>
    /// A choice in the Settings icon picker: its name, what it shows, and which drawing it uses in light and dark mode. Shared by
    /// both apps (docs/macos-port.md, correction 15); the images are each app's own (AppIcons.Images.cs).
    /// </summary>
    public sealed partial class AppIconOption
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
    }

    /// <summary>
    /// The app icons: the choices Settings offers and which drawing each one shows. Shared by both apps; the images themselves
    /// are each app's own (AppIcons.Images.cs).
    /// </summary>
    public static partial class AppIcons
    {
        public const string FollowsThemeGroup = "FOLLOWS THE THEME";

        public const string AlwaysTheSameGroup = "ALWAYS THE SAME";

        /// <summary>The folder, beside the exe, that the .ico files are copied to, for the shortcuts that point at them.</summary>
        public const string FolderName = "icons";

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
        /// The drawing without its background: the keycap alone, or the monogram without its square. The tab bar always shows this
        /// one; the windows and the shortcuts do when the icon is set to transparent. A transparent drawing is its own.
        /// </summary>
        public static AppIconArt GetTransparent(AppIconArt art)
        {
            return art switch
            {
                AppIconArt.Porcelain => AppIconArt.KeyCharcoal,
                AppIconArt.Onyx => AppIconArt.KeyWhite,
                AppIconArt.Cobalt or AppIconArt.Twilight => AppIconArt.KeyBlue,
                AppIconArt.Classic => AppIconArt.ClassicTransparent,
                _ => art
            };
        }

        /// <summary>The .ico's name, as in assets/icons: porcelain.ico, key-white.ico, classic-transparent.ico.</summary>
        public static string GetFileName(AppIconArt art)
        {
            var name = art switch
            {
                AppIconArt.KeyWhite => "key-white",
                AppIconArt.KeyCharcoal => "key-charcoal",
                AppIconArt.KeyBlue => "key-blue",
                AppIconArt.ClassicTransparent => "classic-transparent",
                _ => art.ToString().ToLowerInvariant()
            };
            return name + ".ico";
        }
    }
}
