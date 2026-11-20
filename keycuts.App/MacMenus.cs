using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// macOS's menu bar (Phase 5.2, docs/styleguide.md, Decisions): the app menu (About and Settings…; macOS adds Hide and Quit),
    /// then View (the three screens), Window (Minimize, Close) and Help. Items with a key take it before the window sees it, so they
    /// do what the keys do there (<see cref="MainWindow.PickTab"/>). Nothing off macOS.
    /// </summary>
    public static class MacMenus
    {
        public const string HelpUrl = "https://github.com/andrewralon/keycuts#readme";

        public static void Attach(Application app, MainWindow window)
        {
            if (!OperatingSystem.IsMacOS())
            {
                return;
            }

            var about = () => NoticeWindow.Show(window, "keycuts " + new Version(Runner.Version).ToString(3));

            // Avalonia has already built the app menu (About Avalonia, Services, Hide, Quit) and doesn't look at a menu set now:
            // change that one in place so macOS redraws it
            var appMenu = NativeMenu.GetMenu(app);
            if (appMenu == null)
            {
                appMenu = new NativeMenu();
                NativeMenu.SetMenu(app, appMenu);
            }

            FillAppMenu(appMenu, about, () => Show(window, 3));

            var menuBar = new NativeMenu();
            menuBar.Add(Submenu("View",
                Item("Creator", new KeyGesture(Key.D1, KeyModifiers.Meta), () => Show(window, 1)),
                Item("Manager", new KeyGesture(Key.D2, KeyModifiers.Meta), () => Show(window, 2)),
                Item("Settings", new KeyGesture(Key.D3, KeyModifiers.Meta), () => Show(window, 3))));
            menuBar.Add(Submenu("Window",
                Item("Minimize", new KeyGesture(Key.M, KeyModifiers.Meta), () => window.WindowState = WindowState.Minimized),
                Item("Close", new KeyGesture(Key.W, KeyModifiers.Meta), window.Close)));
            menuBar.Add(Submenu("Help",
                Item("keycuts Help", null, () => Desktops.Current.OpenTarget(HelpUrl))));
            NativeMenu.SetMenu(window, menuBar);
        }

        /// <summary>About keycuts and Settings… (⌘,) at the top of the app menu, in place of Avalonia's About; the items macOS adds stay.</summary>
        public static void FillAppMenu(NativeMenu appMenu, Action about, Action settings)
        {
            for (var i = appMenu.Items.Count - 1; i >= 0; i--)
            {
                if (appMenu.Items[i] is NativeMenuItem { Header: "About Avalonia" })
                {
                    appMenu.Items.RemoveAt(i);
                }
            }

            appMenu.Items.Insert(0, Item("About keycuts", null, about));
            appMenu.Items.Insert(1, new NativeMenuItemSeparator());
            appMenu.Items.Insert(2, Item("Settings…", new KeyGesture(Key.OemComma, KeyModifiers.Meta), settings));
        }

        private static void Show(MainWindow window, int tab)
        {
            window.PickTab(tab);
            window.Activate();
        }

        private static NativeMenuItem Item(string header, KeyGesture? gesture, Action action)
        {
            var item = new NativeMenuItem(header) { Gesture = gesture };
            item.Click += (sender, e) => action();
            return item;
        }

        private static NativeMenuItem Submenu(string header, params NativeMenuItem[] items)
        {
            var menu = new NativeMenu();
            foreach (var item in items)
            {
                menu.Add(item);
            }

            return new NativeMenuItem(header) { Menu = menu };
        }
    }
}
