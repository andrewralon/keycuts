using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;

// This app is Windows-only; the CA1416 platform analyzer doesn't recognise the Windows TFM here.
#pragma warning disable CA1416

namespace keycuts.GUI.Themes
{
    public enum ThemeVariant
    {
        Light,
        Dark,
        HighContrast
    }

    /// <summary>
    /// Follows the Windows app theme (light/dark, no in-app toggle) by swapping the palette dictionary at runtime.
    /// </summary>
    public static class ThemeManager
    {
        const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string AppsUseLightThemeValue = "AppsUseLightTheme";
        const int DwmwaUseImmersiveDarkMode = 20;

        static Application? application;

        public static ThemeVariant Current { get; private set; } = ThemeVariant.Light;

        /// <summary>
        /// High contrast wins; otherwise AppsUseLightTheme = 0 means dark. A missing value means light.
        /// </summary>
        public static ThemeVariant Resolve(int? appsUseLightTheme, bool highContrast)
        {
            if (highContrast)
            {
                return ThemeVariant.HighContrast;
            }

            return appsUseLightTheme == 0 ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public static string GetPaletteFileName(ThemeVariant variant)
        {
            return variant switch
            {
                ThemeVariant.Dark => "Dark.xaml",
                ThemeVariant.HighContrast => "HighContrast.xaml",
                _ => "Light.xaml"
            };
        }

        public static void Initialize(Application app)
        {
            application = app;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            app.Exit += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            Apply(DetectVariant());
        }

        public static ThemeVariant DetectVariant()
        {
            return Resolve(ReadAppsUseLightTheme(), SystemParameters.HighContrast);
        }

        static int? ReadAppsUseLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
                return key?.GetValue(AppsUseLightThemeValue) as int?;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Accessibility))
            {
                return;
            }

            application?.Dispatcher.BeginInvoke(new Action(() => Apply(DetectVariant())));
        }

        static void Apply(ThemeVariant variant)
        {
            if (application == null)
            {
                return;
            }

            var dictionaries = application.Resources.MergedDictionaries;
            var palette = dictionaries.FirstOrDefault(d => IsPalette(d));
            var fileName = GetPaletteFileName(variant);
            var replacement = new ResourceDictionary { Source = new Uri($"pack://application:,,,/keycuts.GUI;component/Themes/{fileName}", UriKind.Absolute) };

            if (palette == null)
            {
                dictionaries.Insert(0, replacement);
            }
            else
            {
                dictionaries[dictionaries.IndexOf(palette)] = replacement;
            }

            Current = variant;

            foreach (Window window in application.Windows)
            {
                ApplyWindowChrome(window);
            }
        }

        static bool IsPalette(ResourceDictionary dictionary)
        {
            var source = dictionary.Source?.OriginalString;
            return source != null && (source.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Themes/HighContrast.xaml", StringComparison.OrdinalIgnoreCase));
        }

        static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                ApplyWindowChrome(window);
            }
        }

        // Derived Window types don't pick up an implicit Window style, so set the base look here unless the window already chose one.
        static void ApplyWindowChrome(Window window)
        {
            if (window.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
            {
                window.SetResourceReference(Control.BackgroundProperty, "CanvasBrush");
            }

            if (window.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
            {
                window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            }

            if (window.ReadLocalValue(Control.FontFamilyProperty) == DependencyProperty.UnsetValue)
            {
                window.SetResourceReference(Control.FontFamilyProperty, "AppFontFamily");
            }

            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                var useDark = Current == ThemeVariant.Dark ? 1 : 0;
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
            }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
