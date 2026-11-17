using System;
using System.Windows;
using keycuts.Common;

namespace keycuts.GUI.Themes
{
    /// <summary>
    /// Shows the icon picked in Settings on every window (title bar, taskbar button, Alt+Tab), and switches it when the theme
    /// changes for the options that follow the theme. With <see cref="Transparent"/> on, that is the drawing without its
    /// background. Changing the shortcuts (Start menu, pinned taskbar) is the caller's <c>updateShortcuts</c>, so tests and Debug
    /// runs can leave them alone.
    /// </summary>
    public static class AppIconManager
    {
        private static Application? application;

        private static Action<AppIconArt>? updateShortcuts;

        public static AppIconChoice Choice { get; private set; }

        /// <summary>Whether the windows and the shortcuts show the icon without its background (the keycap alone).</summary>
        public static bool Transparent { get; private set; }

        /// <summary>The drawing shown now.</summary>
        public static AppIconArt Current { get; private set; }

        /// <summary>The tab bar's drawing: always the one without a background, whatever <see cref="Transparent"/> says.</summary>
        public static AppIconArt TabBar
        {
            get { return AppIcons.GetTransparent(Current); }
        }

        /// <summary>After <see cref="ThemeManager.Initialize"/>, which it follows, and before the first window.</summary>
        public static void Initialize(Application app, AppIconChoice choice, bool transparent, Action<AppIconArt>? shortcuts)
        {
            // Once per process: the class handler and the theme subscription can't be added twice
            if (application != null)
            {
                return;
            }

            application = app;
            updateShortcuts = shortcuts;
            Choice = choice;
            Transparent = transparent;
            Current = Resolve(choice, transparent);
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
            ThemeManager.Applied += (_, _) => Refresh();

            // Also brings back the icon on shortcuts an upgrade or a repair recreated
            updateShortcuts?.Invoke(Current);
        }

        /// <summary>Shows the choice on every open window now. Saving it is the caller's job.</summary>
        public static void SetChoice(AppIconChoice choice)
        {
            Choice = choice;
            Refresh();
        }

        /// <summary>Shows the icon with or without its background on every open window now. Saving it is the caller's job.</summary>
        public static void SetTransparent(bool transparent)
        {
            Transparent = transparent;
            Refresh();
        }

        /// <summary>Gives a window the current icon; every window gets it when it loads.</summary>
        public static void ApplyTo(Window window)
        {
            window.Icon = AppIcons.GetWindowIcon(Current);
        }

        private static AppIconArt Resolve(AppIconChoice choice, bool transparent)
        {
            var art = AppIcons.Resolve(choice, AppIcons.IsLight(ThemeManager.Current, SystemColors.WindowColor));
            return transparent ? AppIcons.GetTransparent(art) : art;
        }

        private static void Refresh()
        {
            var art = Resolve(Choice, Transparent);
            if (application == null || art == Current)
            {
                return;
            }

            Current = art;
            foreach (Window window in application.Windows)
            {
                ApplyTo(window);
            }

            updateShortcuts?.Invoke(art);
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (application != null && sender is Window window)
            {
                ApplyTo(window);
            }
        }
    }
}
