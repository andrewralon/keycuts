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
    /// macOS (Phase 6.1, docs/macos-port.md, The Run box): keycuts stays running in the menu bar. Closing the window hides it, and
    /// the Dock icon shows only while the window is open. The menu bar icon (the app icon's keycap, the one chosen in Settings)
    /// opens the creator, the Manager and Settings, and quits. Clicking keycuts again (Finder, <c>open -b</c>) shows the window.
    /// </summary>
    public static class MacMenuBar
    {
        private static bool quitting;

        public static void Attach(Application app, IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
        {
            if (OperatingSystem.IsMacOS())
            {
                Follow(app, desktop, window);
            }
        }

        [SupportedOSPlatform("macos")]
        private static void Follow(Application app, IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (sender, e) => quitting = true;

            // The red light, ⌘W and Escape hide the window; Quit and logging out close it
            window.Closing += (sender, e) =>
            {
                if (!quitting && e.CloseReason == WindowCloseReason.WindowClosing)
                {
                    e.Cancel = true;
                    window.Hide();
                    ShowInDock(false);
                }
            };

            var menu = new NativeMenu();
            // Run… shows the Run box's shortcut, and follows it when Settings changes it
            var run = Item("Run…", MacRunBox.Show);
            run.Gesture = RunBoxGesture.From(MacHotKey.Current);
            MacHotKey.Changed += (sender, e) => run.Gesture = RunBoxGesture.From(MacHotKey.Current);
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
            TrayIcon.SetIcons(app, new TrayIcons { tray });
            AppIconManager.Changed += (sender, e) => tray.Icon = KeycapIcon();

            // Opened again while it runs (the Dock, Finder, Spotlight, open -b): the window comes back
            if (app.TryGetFeature<IActivatableLifetime>() is { } activation)
            {
                activation.Activated += (sender, e) =>
                {
                    if (e.Kind == ActivationKind.Reopen && !window.IsVisible)
                    {
                        Show(window, null);
                    }
                };
            }
        }

        /// <summary>Shows the window, on a tab if one is given, in front of every other app.</summary>
        public static void Show(MainWindow window, int? tab)
        {
            if (OperatingSystem.IsMacOS())
            {
                ShowInDock(true);
                ObjC.SendBool(ObjC.Send(ObjC.GetClass("NSApplication"), ObjC.Selector("sharedApplication")), ObjC.Selector("activateIgnoringOtherApps:"), 1L);
            }

            window.Show();
            window.Activate();
            if (tab is int number)
            {
                window.PickTab(number);
            }
        }

        /// <summary>No Dock icon: a start at login, with no window.</summary>
        public static void HideFromDock()
        {
            if (OperatingSystem.IsMacOS())
            {
                ShowInDock(false);
            }
        }

        /// <summary>The Dock icon while a window is open: <c>setActivationPolicy:</c> regular (0) or accessory (1), at runtime (Phase 0.2).</summary>
        [SupportedOSPlatform("macos")]
        private static void ShowInDock(bool show)
        {
            var nsApp = ObjC.Send(ObjC.GetClass("NSApplication"), ObjC.Selector("sharedApplication"));
            ObjC.SendBool(nsApp, ObjC.Selector("setActivationPolicy:"), show ? 0L : 1L);

            if (show)
            {
                MacDockIcon.Refresh();
            }
        }

        private static NativeMenuItem Item(string header, Action action)
        {
            var item = new NativeMenuItem(header);
            item.Click += (sender, e) => action();
            return item;
        }

        /// <summary>The keycap alone (no tile) of the app icon now chosen, in full color. macOS scales it to the menu bar's height.</summary>
        public static WindowIcon KeycapIcon()
        {
            return new WindowIcon(AppIcons.GetPreview(AppIconManager.TabBar));
        }
    }
}
