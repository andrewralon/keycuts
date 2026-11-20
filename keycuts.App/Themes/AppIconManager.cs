using System;
using keycuts.Common;

namespace keycuts.GUI.Themes
{
    /// <summary>
    /// The app icon the user chose, and the drawing it shows now (WPF's Themes/AppIconManager, its state only). The Settings view
    /// model reads and changes it through <c>AppIconService</c>. Setting the windows' icons and repointing the shortcuts comes with
    /// the icon picker in Phase 3.8.
    /// </summary>
    public static class AppIconManager
    {
        public static AppIconChoice Choice { get; private set; }

        public static bool Transparent { get; private set; }

        /// <summary>The drawing the windows and shortcuts show.</summary>
        public static AppIconArt Current { get; private set; } = Resolve(AppIconChoice.BlackAndWhite, false);

        /// <summary>The tab bar always shows the keycap alone.</summary>
        public static AppIconArt TabBar
        {
            get { return AppIcons.GetTransparent(Current); }
        }

        /// <summary>Raised when <see cref="Current"/> changes: a new choice, the transparent setting, or a theme change.</summary>
        public static event EventHandler? Changed;

        public static void Initialize(AppIconChoice choice, bool transparent)
        {
            Choice = choice;
            Transparent = transparent;
            Current = Resolve(choice, transparent);
            ThemeManager.Applied -= OnThemeApplied;
            ThemeManager.Applied += OnThemeApplied;
        }

        public static void SetChoice(AppIconChoice choice)
        {
            Choice = choice;
            Refresh();
        }

        public static void SetTransparent(bool transparent)
        {
            Transparent = transparent;
            Refresh();
        }

        /// <summary>
        /// Whether the app is in a light theme, for the options that follow it. High contrast has no light or dark of its own, so
        /// its window background decides: a black one (High Contrast Black, Aquatic) counts as dark.
        /// </summary>
        public static bool IsLight(Palette palette, Avalonia.Media.Color highContrastWindow)
        {
            return palette switch
            {
                Palette.Light => true,
                Palette.Dark => false,
                _ => (0.2126 * highContrastWindow.R + 0.7152 * highContrastWindow.G + 0.0722 * highContrastWindow.B) / 255 >= 0.5
            };
        }

        static AppIconArt Resolve(AppIconChoice choice, bool transparent)
        {
            var light = IsLight(ThemeManager.Current, ThemeManager.ReadSystemColor(SystemColorName.Window));
            var art = AppIcons.Resolve(choice, light);
            return transparent ? AppIcons.GetTransparent(art) : art;
        }

        static void OnThemeApplied(object? sender, EventArgs e)
        {
            Refresh();
        }

        static void Refresh()
        {
            var art = Resolve(Choice, Transparent);
            if (art == Current)
            {
                return;
            }

            Current = art;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
