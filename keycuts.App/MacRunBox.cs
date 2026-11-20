using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The Run box on macOS (Phase 6.4): opened by the shortcut or the menu bar's Run…, a third of the way down the screen with
    /// the mouse. A keycut runs through <see cref="Desktops.Current"/> (<c>/bin/sh</c>, detached, so its own lines count the run).
    /// Closing it gives the focus back to the app that had it ([NSApp hide:]) unless the main window is open.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class MacRunBox
    {
        private static RunBoxWindow? box;

        private static MainWindow? main;

        private static ShellViewModel? shell;

        public static void Attach(MainWindow mainWindow, ShellViewModel shellViewModel)
        {
            main = mainWindow;
            shell = shellViewModel;
        }

        public static void Show()
        {
            box ??= new RunBoxWindow(new RunBoxViewModel(Run, Create, Hide));
            box.ViewModel.Reset(Array.Empty<Keycut>());
            _ = LoadAsync(box.ViewModel);

            box.Show();
            Place(box);
            ObjC.SendBool(NSApp(), ObjC.Selector("activateIgnoringOtherApps:"), 1L);
            box.Activate();
            box.FocusText();
        }

        private static async Task LoadAsync(RunBoxViewModel viewModel)
        {
            try
            {
                var keycuts = await Task.Run(() => KeycutRepository.CreateDefault().LoadAllAsync());
                Dispatcher.UIThread.Post(() => viewModel.Keycuts = keycuts);
            }
            catch (Exception ex) when (ex is KeycutRepositoryException || ex is System.IO.IOException)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: the Run box couldn't load the keycuts: " + ex.Message);
            }
        }

        private static void Run(Keycut keycut)
        {
            try
            {
                Desktops.Current.RunKeycut(keycut.FilePath);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException || ex is InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't run \"" + keycut.FilePath + "\": " + ex.Message);
            }
        }

        private static void Create(string name)
        {
            if (main == null || shell == null)
            {
                return;
            }

            MacMenuBar.Show(main, 1);
            shell.Creator.ShortcutName = name;
        }

        private static void Hide()
        {
            box?.Hide();
            if (main is not { IsVisible: true })
            {
                ObjC.Send(NSApp(), ObjC.Selector("hide:"), IntPtr.Zero);
            }
        }

        private static IntPtr NSApp()
        {
            return ObjC.Send(ObjC.GetClass("NSApplication"), ObjC.Selector("sharedApplication"));
        }

        /// <summary>Centered across the screen with the mouse, its top a third of the way down that screen's work area.</summary>
        private static void Place(RunBoxWindow window)
        {
            var screens = window.Screens;
            if (screens.Primary is not { } primary)
            {
                return;
            }

            // AppKit's mouse position: points from the bottom left of the main screen; Avalonia's screens: pixels from the top left
            var mouse = ObjC.SendPoint(ObjC.GetClass("NSEvent"), ObjC.Selector("mouseLocation"));
            var top = primary.Bounds.Height / primary.Scaling - mouse.Y;
            var screen = screens.All.FirstOrDefault(s =>
                new Rect(s.Bounds.X / s.Scaling, s.Bounds.Y / s.Scaling, s.Bounds.Width / s.Scaling, s.Bounds.Height / s.Scaling)
                    .Contains(new Point(mouse.X, top))) ?? primary;

            var area = screen.WorkingArea;
            var width = window.Bounds.Width * screen.Scaling;
            window.Position = new PixelPoint((int)(area.X + (area.Width - width) / 2), (int)(area.Y + area.Height / 3));
        }
    }
}
