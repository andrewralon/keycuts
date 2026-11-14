using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using keycuts.GUI.Controls;

namespace keycuts.GUI
{
    /// <summary>
    /// A small window procedure for the tab bar's maximize button (#218). Windows 11 shows its snap layouts when the pointer rests over
    /// a window's maximize button, and it knows the button only when the window answers <c>WM_NCHITTEST</c> with <c>HTMAXBUTTON</c> there.
    /// <c>WindowChrome</c> doesn't do that for a button of our own. Once it does, the mouse over the button is a non-client matter:
    /// WPF sees no hover and no click, so this hook sets <see cref="CaptionButtonState"/> for the look, and a press and release on
    /// the button maximizes or restores the window (which a standard window's frame would do through its default handling).
    /// </summary>
    public sealed class WindowCaptionHook
    {
        public const int HtClient = 1;

        public const int HtCaption = 2;

        public const int HtMaxButton = 9;

        private const int WmNcHitTest = 0x0084;
        private const int WmNcMouseMove = 0x00A0;
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmNcLButtonUp = 0x00A2;
        private const int WmNcMouseLeave = 0x02A2;
        private const uint TmeLeave = 0x00000002;
        private const uint TmeNonClient = 0x00000010;

        private readonly Window window;

        private readonly FrameworkElement maximizeButton;

        private bool tracking;

        public WindowCaptionHook(Window window, FrameworkElement maximizeButton)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            this.maximizeButton = maximizeButton ?? throw new ArgumentNullException(nameof(maximizeButton));

            var source = PresentationSource.FromVisual(window) as HwndSource;
            if (source != null)
            {
                source.AddHook(WndProc);
            }
            else
            {
                window.SourceInitialized += (sender, e) => (PresentationSource.FromVisual(window) as HwndSource)?.AddHook(WndProc);
            }
        }

        /// <summary>Whether a point in the button's own coordinates (DIPs) is on the button.</summary>
        public static bool IsOver(Point point, double width, double height)
        {
            return point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;
        }

        /// <summary>The screen position (pixels) a mouse message carries in its <c>lParam</c>: two signed 16-bit halves.</summary>
        public static Point ScreenPoint(IntPtr lParam)
        {
            var value = lParam.ToInt64();
            return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmNcHitTest:
                    if (IsOverMaximizeButton(lParam))
                    {
                        handled = true;
                        return new IntPtr(HtMaxButton);
                    }

                    break;

                case WmNcMouseMove:
                    var over = wParam.ToInt32() == HtMaxButton;
                    CaptionButtonState.SetIsHot(maximizeButton, over);
                    if (!over)
                    {
                        CaptionButtonState.SetIsPressed(maximizeButton, false);
                    }
                    else if (!tracking)
                    {
                        tracking = true;
                        var request = new TrackMouseEvent { Size = (uint)Marshal.SizeOf<TrackMouseEvent>(), Flags = TmeLeave | TmeNonClient, Window = hwnd };
                        TrackMouseEventNative(ref request);
                    }

                    break;

                case WmNcMouseLeave:
                    tracking = false;
                    CaptionButtonState.SetIsHot(maximizeButton, false);
                    CaptionButtonState.SetIsPressed(maximizeButton, false);
                    break;

                case WmNcLButtonDown:
                    if (wParam.ToInt32() == HtMaxButton)
                    {
                        CaptionButtonState.SetIsPressed(maximizeButton, true);
                        handled = true;
                    }

                    break;

                case WmNcLButtonUp:
                    if (wParam.ToInt32() == HtMaxButton)
                    {
                        var pressed = CaptionButtonState.GetIsPressed(maximizeButton);
                        CaptionButtonState.SetIsPressed(maximizeButton, false);
                        handled = true;
                        if (pressed)
                        {
                            ToggleMaximized();
                        }
                    }

                    break;
            }

            return IntPtr.Zero;
        }

        private bool IsOverMaximizeButton(IntPtr lParam)
        {
            // A disabled button (the fixed-size screens) must not claim the point: the bar is a plain caption there
            if (!maximizeButton.IsEnabled || !maximizeButton.IsVisible || maximizeButton.ActualWidth <= 0)
            {
                return false;
            }

            var point = maximizeButton.PointFromScreen(ScreenPoint(lParam));
            return IsOver(point, maximizeButton.ActualWidth, maximizeButton.ActualHeight);
        }

        private void ToggleMaximized()
        {
            if (window.WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(window);
            }
            else
            {
                SystemCommands.MaximizeWindow(window);
            }
        }

        #region External Methods

        [StructLayout(LayoutKind.Sequential)]
        private struct TrackMouseEvent
        {
            public uint Size;
            public uint Flags;
            public IntPtr Window;
            public uint HoverTime;
        }

        [DllImport("user32.dll", EntryPoint = "TrackMouseEvent")]
        private static extern bool TrackMouseEventNative(ref TrackMouseEvent request);

        #endregion External Methods
    }
}
