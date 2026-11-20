using System;
using System.Runtime.Versioning;
using Avalonia.Controls;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// macOS (Phase 5.1, docs/styleguide.md, Tabbed window): the traffic lights centered in the 48 tab bar, the close button 20
    /// from the edge. Avalonia keeps them where AppKit puts them in a plain title bar (near the top, 8 from the edge), so this makes
    /// the title bar's container as tall as the tab bar and moves the three standard buttons, keeping their spacing. AppKit lays
    /// them out again on a resize or a change of focus, so it's applied again then. Does nothing off macOS.
    /// </summary>
    public static class MacTrafficLights
    {
        /// <summary>The close button's distance from the left edge.</summary>
        public const double Left = 20;

        public static void Attach(Window window, double barHeight)
        {
            if (OperatingSystem.IsMacOS())
            {
                Follow(window, barHeight);
            }
        }

        [SupportedOSPlatform("macos")]
        private static void Follow(Window window, double barHeight)
        {
            void Apply() => Place(window, barHeight);
            window.Opened += (sender, e) => Apply();
            window.SizeChanged += (sender, e) => Apply();
            window.Activated += (sender, e) => Apply();
            window.Deactivated += (sender, e) => Apply();
        }

        [SupportedOSPlatform("macos")]
        private static void Place(Window window, double barHeight)
        {
            var handle = window.TryGetPlatformHandle();
            if (handle == null || handle.Handle == IntPtr.Zero || handle.HandleDescriptor != "NSWindow")
            {
                return;
            }

            using (ObjC.AutoreleasePool())
            {
                var buttons = new IntPtr[3];
                for (var i = 0; i < buttons.Length; i++)
                {
                    // NSWindowCloseButton, NSWindowMiniaturizeButton, NSWindowZoomButton
                    buttons[i] = ObjC.Send(handle.Handle, ObjC.Selector("standardWindowButton:"), (long)i);
                    if (buttons[i] == IntPtr.Zero)
                    {
                        return;
                    }
                }

                // NSTitlebarView inside NSTitlebarContainerView, inside the frame view; AppKit's coordinates start at the bottom
                var titlebar = ObjC.Send(buttons[0], ObjC.Selector("superview"));
                var container = ObjC.Send(titlebar, ObjC.Selector("superview"));
                var frameView = ObjC.Send(container, ObjC.Selector("superview"));
                if (titlebar == IntPtr.Zero || container == IntPtr.Zero || frameView == IntPtr.Zero)
                {
                    return;
                }

                var outer = ObjC.SendRect(frameView, ObjC.Selector("bounds"));
                ObjC.Send(container, ObjC.Selector("setFrame:"), new CGRect { X = 0, Y = outer.Height - barHeight, Width = outer.Width, Height = barHeight });
                ObjC.Send(titlebar, ObjC.Selector("setFrame:"), new CGRect { X = 0, Y = 0, Width = outer.Width, Height = barHeight });

                var first = ObjC.SendRect(buttons[0], ObjC.Selector("frame"));
                foreach (var button in buttons)
                {
                    var frame = ObjC.SendRect(button, ObjC.Selector("frame"));
                    ObjC.Send(button, ObjC.Selector("setFrameOrigin:"), new CGPoint
                    {
                        X = Left + frame.X - first.X,
                        Y = Math.Round((barHeight - frame.Height) / 2)
                    });
                }
            }
        }
    }
}
