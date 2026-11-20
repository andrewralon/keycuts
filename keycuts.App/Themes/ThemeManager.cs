using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using keycuts.Common;

namespace keycuts.GUI.Themes
{
    /// <summary>keycuts' three palettes (<c>Themes/Light.axaml</c>, <c>Dark.axaml</c>, <c>HighContrast.axaml</c>).</summary>
    public enum Palette
    {
        Light,
        Dark,
        HighContrast
    }

    /// <summary>
    /// Applies the user's theme choice by switching the app's theme variant, which picks the palette from
    /// <c>App.axaml</c>'s theme dictionaries. The System choice follows the platform's theme live, and Windows high contrast
    /// always wins (WPF's ThemeManager, ported for Phase 3.2).
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>The high-contrast palette's variant. Avalonia's own resources (none yet) fall back to Light's.</summary>
        public static readonly ThemeVariant HighContrastVariant = new ThemeVariant("HighContrast", ThemeVariant.Light);

        static Application? application;

        public static Palette Current { get; private set; } = Palette.Light;

        public static ThemePreference Preference { get; private set; }

        /// <summary>Reads a system color for the high-contrast palette: Windows', or macOS's fixed ones. Tests replace it.</summary>
        public static Func<SystemColorName, Color> ReadSystemColor { get; set; } =
            OperatingSystem.IsWindows() ? SystemColor.ReadWindows : name => SystemColor.ReadMac(name, IsDarkAppearance());

        /// <summary>macOS's high contrast follows the theme choice, or the system's appearance with System.</summary>
        private static bool IsDarkAppearance()
        {
            return Preference switch
            {
                ThemePreference.Light => false,
                ThemePreference.Dark => true,
                _ => application?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark
            };
        }

        /// <summary>
        /// Raised after a palette is applied: a new theme, a Settings preview, or a change in the platform's colors (high
        /// contrast included, whose colors can change while the palette doesn't). The app icon follows it.
        /// </summary>
        public static event EventHandler? Applied;

        /// <summary>
        /// High contrast always wins, because it is an accessibility need. Then the user's choice; with System, the platform's
        /// dark theme means Dark and anything else (light, or not known) means Light.
        /// </summary>
        public static Palette Resolve(PlatformThemeVariant? platformTheme, bool highContrast, ThemePreference preference = ThemePreference.System)
        {
            if (highContrast)
            {
                return Palette.HighContrast;
            }

            return preference switch
            {
                ThemePreference.Light => Palette.Light,
                ThemePreference.Dark => Palette.Dark,
                _ => platformTheme == PlatformThemeVariant.Dark ? Palette.Dark : Palette.Light
            };
        }

        /// <summary>
        /// Whether the platform asks for keycuts' high-contrast palette: Windows high contrast, or macOS's Increase contrast
        /// (Phase 5.4), on a platform whose setting keycuts follows.
        /// </summary>
        public static bool IsHighContrast(PlatformColorValues? values, bool followsPlatformContrast)
        {
            return followsPlatformContrast && values?.ContrastPreference == ColorContrastPreference.High;
        }

        public static ThemeVariant GetThemeVariant(Palette palette)
        {
            return palette switch
            {
                Palette.Dark => ThemeVariant.Dark,
                Palette.HighContrast => HighContrastVariant,
                _ => ThemeVariant.Light
            };
        }

        public static void Initialize(Application app, ThemePreference preference = ThemePreference.System)
        {
            application = app;
            Preference = preference;

            if (app.PlatformSettings != null)
            {
                app.PlatformSettings.ColorValuesChanged += (_, _) => Dispatcher.UIThread.Post(() => Apply(DetectPalette()));
            }

            Apply(DetectPalette());
        }

        public static Palette DetectPalette()
        {
            var values = application?.PlatformSettings?.GetColorValues();
            var highContrast = IsHighContrast(values, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) || ReadMacIncreaseContrast();
            return Resolve(values?.ThemeVariant, highContrast, Preference);
        }

        /// <summary>
        /// macOS's Increase contrast, which Avalonia doesn't report (<see cref="MacAccessibility"/>). Off until the app starts
        /// following it, so tests never read the machine's setting.
        /// </summary>
        public static Func<bool> ReadMacIncreaseContrast { get; set; } = () => false;

        /// <summary>
        /// Follows Increase contrast on macOS: reads it now and again every two seconds (no event reaches the app), and applies
        /// the palette when it changes.
        /// </summary>
        public static void FollowMacIncreaseContrast(Func<bool> read)
        {
            ReadMacIncreaseContrast = read;
            var last = read();

            if (last)
            {
                Apply(DetectPalette());
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                var now = ReadMacIncreaseContrast();

                if (now != last)
                {
                    last = now;
                    Apply(DetectPalette());
                }
            };
            timer.Start();
        }

        /// <summary>Re-themes every open window now. Saving the choice is the caller's job.</summary>
        public static void SetPreference(ThemePreference preference)
        {
            Preference = preference;

            // High contrast on macOS takes its colors from the theme choice, so a new choice fills them again
            var palette = DetectPalette();
            if (palette != Current || palette == Palette.HighContrast)
            {
                Apply(palette);
            }
        }

        /// <summary>Shows <paramref name="palette"/> now, whatever the platform says. For the app's own previews and for tests.</summary>
        public static void Apply(Palette palette)
        {
            if (application == null)
            {
                return;
            }

            if (palette == Palette.HighContrast && HighContrastPalette(application) is { } highContrast)
            {
                SystemColor.Fill(highContrast, ReadSystemColor);
            }

            Current = palette;
            application.RequestedThemeVariant = GetThemeVariant(palette);
            Applied?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>The high-contrast palette as loaded from <c>App.axaml</c>, whose brushes <see cref="SystemColor.Fill"/> sets.</summary>
        public static IResourceDictionary? HighContrastPalette(Application app)
        {
            if (!app.Resources.ThemeDictionaries.TryGetValue(HighContrastVariant, out var provider))
            {
                return null;
            }

            return provider switch
            {
                ResourceInclude include => include.Loaded as IResourceDictionary,
                IResourceDictionary dictionary => dictionary,
                _ => null
            };
        }
    }
}
