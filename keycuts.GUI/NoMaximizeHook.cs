using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace keycuts.GUI
{
    /// <summary>
    /// Keeps the main window from being maximized, on every screen. The Manager is resizable (<c>ResizeMode.CanResize</c>), and WPF gives
    /// a resizable window the maximize box, which is what lets Windows maximize it: a double click on the caption, Win+Up, a drag to the
    /// top of the screen and the window menu's Maximize. This takes the box off whenever WPF puts it back (<c>WM_STYLECHANGING</c>),
    /// refuses the maximize command for anything that asks anyway, and restores the window if something sets the state directly.
    /// Resizing from the edges and snapping to the sides of the screen are untouched.
    /// </summary>
    public sealed class NoMaximizeHook
    {
        public const int WsMaximizeBox = 0x00010000;

        public const int WmSysCommand = 0x0112;

        public const int ScMaximize = 0xF030;

        private const int WmStyleChanging = 0x007C;
        private const int GwlStyle = -16;

        private readonly Window window;

        public NoMaximizeHook(Window window)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));

            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                Attach(source);
            }
            else
            {
                window.SourceInitialized += (sender, e) =>
                {
                    if (PresentationSource.FromVisual(window) is HwndSource initialized)
                    {
                        Attach(initialized);
                    }
                };
            }

            window.StateChanged += Window_StateChanged;
        }

        /// <summary>The style a window should have: the one it asked for, without the maximize box.</summary>
        public static int WithoutMaximizeBox(int style)
        {
            return style & ~WsMaximizeBox;
        }

        /// <summary>Whether a <c>WM_SYSCOMMAND</c> asks to maximize; the low four bits of its command are the system's own.</summary>
        public static bool IsMaximizeCommand(IntPtr wParam)
        {
            return (wParam.ToInt64() & 0xFFF0) == ScMaximize;
        }

        private void Attach(HwndSource source)
        {
            source.AddHook(WndProc);

            var style = GetWindowLong(source.Handle, GwlStyle);
            if ((style & WsMaximizeBox) != 0)
            {
                SetWindowLong(source.Handle, GwlStyle, WithoutMaximizeBox(style));
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmStyleChanging:
                    if (wParam.ToInt64() == GwlStyle)
                    {
                        // STYLESTRUCT: the old style, then the new one, which the window gets once this returns
                        var styleNew = Marshal.ReadInt32(lParam, sizeof(int));
                        Marshal.WriteInt32(lParam, sizeof(int), WithoutMaximizeBox(styleNew));
                    }

                    break;

                case WmSysCommand:
                    if (IsMaximizeCommand(wParam))
                    {
                        handled = true;
                    }

                    break;
            }

            return IntPtr.Zero;
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            // Only something setting WindowState itself gets here; restore once that change has finished
            if (window.WindowState == WindowState.Maximized)
            {
                window.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                {
                    if (window.WindowState == WindowState.Maximized)
                    {
                        window.WindowState = WindowState.Normal;
                    }
                }));
            }
        }

        #region External Methods

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        #endregion External Methods
    }
}
