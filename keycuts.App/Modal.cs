using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace keycuts.GUI
{
    /// <summary>
    /// The dialogs as WPF had them: the view models ask synchronously (a confirmation returns a bool), so a dialog runs in a
    /// nested dispatcher frame until it closes. Also the few Windows calls the views make.
    /// </summary>
    public static class Modal
    {
        /// <summary>
        /// Tests answer the dialogs here instead of showing them: given the dialog window, the result it closes with. Null
        /// shows the real dialog.
        /// </summary>
        public static Func<Window, object?>? Answer { get; set; }

        /// <summary>Shows <paramref name="dialog"/> over <paramref name="owner"/> and waits for its result.</summary>
        public static T? Show<T>(Window dialog, Window? owner)
        {
            if (Answer is { } answer)
            {
                return (T?)answer(dialog);
            }

            // A dialog needs an owner in Avalonia: the main window, when the caller's isn't showing
            if (owner == null || !owner.IsVisible)
            {
                owner = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (owner == null || !owner.IsVisible)
            {
                return default;
            }

            var result = default(T);
            var frame = new DispatcherFrame();
            _ = Wait();
            Dispatcher.UIThread.PushFrame(frame);
            return result;

            async System.Threading.Tasks.Task Wait()
            {
                try
                {
                    result = await dialog.ShowDialog<T>(owner);
                }
                finally
                {
                    frame.Continue = false;
                }
            }
        }

        /// <summary>A dialog's height capped to the screen so its footer stays reachable, and the dialog kept on it.</summary>
        public static void FitToWorkArea(Window window)
        {
            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            if (screen == null)
            {
                return;
            }

            var scale = screen.Scaling;
            var area = screen.WorkingArea;
            var workArea = new Avalonia.Rect(area.X / scale, area.Y / scale, area.Width / scale, area.Height / scale);
            window.MaxHeight = workArea.Height;
            var bounds = WindowLayout.ClampToWorkArea(
                new Avalonia.Rect(window.Position.X / scale, window.Position.Y / scale, window.Bounds.Width, window.Bounds.Height), workArea);
            window.Position = new Avalonia.PixelPoint((int)Math.Round(bounds.Left * scale), (int)Math.Round(bounds.Top * scale));
        }

        /// <summary>The system's default sound, as WPF's <c>SystemSounds.Beep</c>. Nothing off Windows for now.</summary>
        public static void Beep()
        {
            if (OperatingSystem.IsWindows())
            {
                MessageBeep(0);
            }
        }

        /// <summary>Whether a Shift key is down now, read from the system (Avalonia keeps no keyboard state of its own).</summary>
        public static bool IsShiftDown()
        {
            return OperatingSystem.IsWindows() && (GetKeyState(VkShift) & 0x8000) != 0;
        }

        private const int VkShift = 0x10;

        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint type);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int key);
    }
}
