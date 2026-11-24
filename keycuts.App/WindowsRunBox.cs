using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The Run box on Windows (issue #306): opened by the shortcut or the tray menu's Run…, centered on the screen with the mouse,
    /// a third of the way down its work area, as on macOS. The box is the same window (<see cref="RunBoxWindow"/>); this is its host.
    /// Closing it gives the focus back to the window that had it, unless the main window is open or the user clicked another one.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class WindowsRunBox
    {
        private static RunBoxWindow? box;

        private static MainWindow? main;

        private static ShellViewModel? shell;

        private static IntPtr previous;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        public static void Attach(MainWindow mainWindow, ShellViewModel shellViewModel)
        {
            main = mainWindow;
            shell = shellViewModel;
        }

        public static void Show()
        {
            // The window to give the focus back to; the box itself being up already isn't one
            var foreground = GetForegroundWindow();
            if (box == null || foreground != Handle(box))
            {
                previous = foreground;
            }

            box ??= new RunBoxWindow(new RunBoxViewModel(RunBoxActions.Run, Create, Hide));
            box.ViewModel.Reset();
            _ = RunBoxActions.LoadAsync(box.ViewModel);

            box.Show();
            Place(box);
            box.Activate();
            box.FocusText();
        }

        private static void Create(string name)
        {
            if (main == null || shell == null)
            {
                return;
            }

            WindowsTray.Show(main, 1);
            shell.Creator.ShortcutName = name;
        }

        private static void Hide()
        {
            if (box == null)
            {
                return;
            }

            // Only when the box still has the focus: a click on another window already moved it there
            var hadFocus = GetForegroundWindow() == Handle(box);
            box.Hide();
            if (hadFocus && main is not { IsVisible: true } && previous != IntPtr.Zero && IsWindow(previous))
            {
                SetForegroundWindow(previous);
            }
        }

        private static IntPtr Handle(RunBoxWindow window)
        {
            return window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        }

        /// <summary>Centered across the screen with the mouse, its top a third of the way down that screen's work area.</summary>
        private static void Place(RunBoxWindow window)
        {
            var screens = window.Screens;
            var screen = (GetCursorPos(out var cursor) ? screens.ScreenFromPoint(new PixelPoint(cursor.X, cursor.Y)) : null) ?? screens.Primary;
            if (screen == null)
            {
                return;
            }

            var area = screen.WorkingArea;
            var width = window.Bounds.Width * screen.Scaling;
            window.Position = new PixelPoint((int)(area.X + (area.Width - width) / 2), (int)(area.Y + area.Height / 3));
        }
    }
}
