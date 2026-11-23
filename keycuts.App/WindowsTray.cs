using System;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI
{
    /// <summary>
    /// Windows (issue #307, as macOS's menu bar): keycuts stays running in the system tray. Closing the window hides it to the tray
    /// instead of quitting, so the Run box's shortcut keeps working; Quit keycuts in the tray menu quits, and so does signing out or
    /// shutting down. The tray icon (the keycap of the icon chosen in Settings) opens the Run box, the creator, the Manager and
    /// Settings. A second start (see <see cref="SingleInstance"/>) shows the window again.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class WindowsTray
    {
        private static bool quitting;

        public static void Attach(Application app, IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (sender, e) => quitting = true;

            // The close button and Alt+F4 hide the window; Quit and signing out or shutting down close it
            window.Closing += (sender, e) =>
            {
                if (!quitting && e.CloseReason == WindowCloseReason.WindowClosing)
                {
                    e.Cancel = true;
                    window.Hide();
                }
            };

            var menu = new NativeMenu();
            // Run… shows the Run box's shortcut, and follows it when Settings changes it
            var run = Item("Run…", WindowsRunBox.Show);
            run.Gesture = RunBoxGesture.From(WindowsHotKey.Current);
            WindowsHotKey.Changed += (sender, e) => run.Gesture = RunBoxGesture.From(WindowsHotKey.Current);
            menu.Add(run);
            menu.Add(Item("New keycut…", () => Show(window, 1)));
            menu.Add(Item("Manager", () => Show(window, 2)));
            menu.Add(Item("Settings…", () => Show(window, 3)));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(Item("Quit keycuts", () =>
            {
                quitting = true;
                desktop.Shutdown();
            }));

            var tray = new TrayIcon { Icon = KeycapIcon(), ToolTipText = "keycuts", Menu = menu, IsVisible = true };
            // A click on the icon itself opens the window, as the Dock icon does on macOS
            tray.Clicked += (sender, e) => Show(window, null);
            TrayIcon.SetIcons(app, new TrayIcons { tray });
            AppIconManager.Changed += (sender, e) => tray.Icon = KeycapIcon();
        }

        /// <summary>Shows the window, on a tab if one is given, in front of the other windows.</summary>
        public static void Show(MainWindow window, int? tab)
        {
            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            if (tab is int number)
            {
                window.PickTab(number);
            }
        }

        private static NativeMenuItem Item(string header, Action action)
        {
            var item = new NativeMenuItem(header);
            item.Click += (sender, e) => action();
            return item;
        }

        /// <summary>The keycap alone (no tile) of the app icon now chosen, from its .ico so the tray gets the small frames crisp.</summary>
        private static WindowIcon KeycapIcon()
        {
            return AppIcons.GetWindowIcon(AppIconManager.TabBar);
        }
    }
}
