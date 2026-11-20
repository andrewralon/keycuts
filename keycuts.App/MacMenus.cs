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

            var appMenu = new NativeMenu();
            appMenu.Add(Item("About keycuts", null, () => NoticeWindow.Show(window, "keycuts " + new Version(Runner.Version).ToString(3))));
            appMenu.Add(new NativeMenuItemSeparator());
            appMenu.Add(Item("Settings…", new KeyGesture(Key.OemComma, KeyModifiers.Meta), () => Show(window, 3)));
            NativeMenu.SetMenu(app, appMenu);

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
