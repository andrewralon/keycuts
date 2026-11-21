using System;
using Avalonia.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// The Windows half of the window's frame (WPF's NoMaximizeHook and NoSystemCaptionHook, docs/macos-port.md corrections 8 and
    /// 9). <c>CanMaximize = false</c> takes the maximize box off, so a double click on the bar, Win+Up and a drag to the top don't
    /// maximize, but the window menu's Maximize (a posted <c>SC_MAXIMIZE</c>) still would: the hook refuses it. It also drops the
    /// classic caption paints Windows would put over the tab bar, and adds <c>WS_SYSMENU</c>, which <c>BorderOnly</c> leaves out,
    /// so Alt+Space opens the window menu. Nothing ever sets <c>WindowState.Maximized</c>.
    /// </summary>
    public static class NoMaximizeHook
    {
        public const int WsMaximizeBox = 0x00010000;

        public const int WsSysMenu = 0x00080000;

        public const int WmSysCommand = 0x0112;

        public const int ScMaximize = 0xF030;

        public const int WmNcUahDrawCaption = 0x00AE;

        public const int WmNcUahDrawFrame = 0x00AF;

        public static void Attach(Window window)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Win32Properties.AddWindowStylesCallback(window, (style, exStyle) => (WithStyles(style), exStyle));
            Win32Properties.AddWndProcHookCallback(window, WndProc);
        }

        /// <summary>The style the window gets: the window menu, and never the maximize box.</summary>
        public static uint WithStyles(uint style)
        {
            return (style | WsSysMenu) & ~(uint)WsMaximizeBox;
        }

        /// <summary>Whether a <c>WM_SYSCOMMAND</c> asks to maximize; the low four bits of its command are the system's own.</summary>
        public static bool IsMaximizeCommand(IntPtr wParam)
        {
            return (wParam.ToInt64() & 0xFFF0) == ScMaximize;
        }

        /// <summary>Whether a message asks the window to paint the system's caption or frame.</summary>
        public static bool IsSystemCaptionPaint(uint msg)
        {
            return msg == WmNcUahDrawCaption || msg == WmNcUahDrawFrame;
        }

        /// <summary>Takes the messages the window refuses: maximize, and the classic caption paints.</summary>
        public static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((msg == WmSysCommand && IsMaximizeCommand(wParam)) || IsSystemCaptionPaint(msg))
            {
                handled = true;
            }

            return IntPtr.Zero;
        }
    }
}
