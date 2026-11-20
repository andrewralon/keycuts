using System;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// macOS (Phase 6.1, docs/macos-port.md, The Run box): keycuts stays running in the menu bar. Closing the window hides it, and
    /// the Dock icon shows only while the window is open. The menu bar icon (a template image, so it follows the menu bar's color)
    /// opens the creator, the Manager and Settings, and quits. Clicking keycuts again (Finder, <c>open -b</c>) shows the window.
    /// </summary>
    public static class MacMenuBar
    {
        /// <summary>The keycap outline from the mockup, on an 18 × 16 box.</summary>
        public const string KeycapPath = "M3,2.5 H15 L16.5,12 A1.5,1.5 0 0 1 15,13.5 H3 A1.5,1.5 0 0 1 1.5,12 Z M5,4.5 H13 L13.8,10 H4.2 Z";

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
            MacOSProperties.SetIsTemplateIcon(tray, true);
            TrayIcon.SetIcons(app, new TrayIcons { tray });

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

        /// <summary>The Dock icon while a window is open: <c>setActivationPolicy:</c> regular (0) or accessory (1), at runtime (Phase 0.2).</summary>
        [SupportedOSPlatform("macos")]
        private static void ShowInDock(bool show)
        {
            var nsApp = ObjC.Send(ObjC.GetClass("NSApplication"), ObjC.Selector("sharedApplication"));
            ObjC.SendBool(nsApp, ObjC.Selector("setActivationPolicy:"), show ? 0L : 1L);
        }

        private static NativeMenuItem Item(string header, Action action)
        {
            var item = new NativeMenuItem(header);
            item.Click += (sender, e) => action();
            return item;
        }

        /// <summary>The keycap outline at 32 px, black on clear: a template image, which macOS colors for the menu bar.</summary>
        public static WindowIcon KeycapIcon()
        {
            var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
            using (var context = bitmap.CreateDrawingContext(true))
            using (context.PushTransform(Matrix.CreateTranslation(0, 1) * Matrix.CreateScale(32.0 / 18, 32.0 / 18)))
            {
                context.DrawGeometry(null, new Pen(Brushes.Black, 1.4, lineJoin: PenLineJoin.Round), Geometry.Parse(KeycapPath));
            }

            return new WindowIcon(bitmap);
        }
    }
}
