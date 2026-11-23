using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Threading;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// The Run box's global shortcut on Windows (issue #306): <c>RegisterHotKey</c> on a message-only window of its own, on the UI
    /// thread, so WM_HOTKEY arrives with the rest of the app's messages and the press runs on that thread. Only one app can have a
    /// shortcut: <see cref="Status"/> is 0 when it is registered, otherwise the Win32 error (1409: another app has it). A start
    /// with the shortcut taken still runs; Settings says so.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class WindowsHotKey
    {
        private const int WM_HOTKEY = 0x0312;

        private const int HotKeyId = 1;

        private static readonly IntPtr MessageOnlyParent = new(-3);

        private static IntPtr window;

        private static WindowProc? windowProc;

        private static bool registered;

        private static Action? pressed;

        private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            public uint Size;
            public uint Style;
            public IntPtr WindowProc;
            public int ClassExtra;
            public int WindowExtra;
            public IntPtr Instance;
            public IntPtr Icon;
            public IntPtr Cursor;
            public IntPtr Background;
            public string? MenuName;
            public string ClassName;
            public IntPtr SmallIcon;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassExW(ref WindowClass windowClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? name);

        /// <summary>The shortcut chosen (null: none), whether or not Windows let us have it.</summary>
        public static RunBoxHotkey? Current { get; private set; }

        /// <summary>0 when <see cref="Current"/> is registered (or there is none); otherwise the Win32 error.</summary>
        public static int Status { get; private set; }

        /// <summary>Raised when the shortcut changes, so the tray menu shows the new one.</summary>
        public static event EventHandler? Changed;

        /// <summary>Registers <paramref name="hotkey"/> in place of the one before (null: none), calling <paramref name="onPressed"/> on the UI thread.</summary>
        public static int Register(RunBoxHotkey? hotkey, Action onPressed)
        {
            pressed = onPressed;
            return Register(hotkey);
        }

        /// <summary>The same, for what the shortcut already does.</summary>
        public static int Register(RunBoxHotkey? hotkey)
        {
            Current = hotkey;
            Unregister();

            Status = hotkey == null ? 0 : Install();
            if (Status == 0 && hotkey != null)
            {
                if (hotkey.IsAllowedOnWindows && RegisterHotKey(window, HotKeyId, hotkey.WindowsModifiers, hotkey.WindowsKeyCode))
                {
                    registered = true;
                }
                else
                {
                    Status = Marshal.GetLastWin32Error();
                    if (Status == 0)
                    {
                        Status = -1;
                    }
                }
            }

            Changed?.Invoke(null, EventArgs.Empty);
            return Status;
        }

        /// <summary>Lets go of the shortcut while Settings records a new one, keeping <see cref="Current"/>; false takes it back.</summary>
        public static void Pause(bool paused)
        {
            if (paused)
            {
                Unregister();
            }
            else if (!registered)
            {
                Register(Current);
            }
        }

        private static void Unregister()
        {
            if (registered)
            {
                UnregisterHotKey(window, HotKeyId);
                registered = false;
            }
        }

        /// <summary>Makes the window the first time it is needed; 0, or the error.</summary>
        private static int Install()
        {
            if (window != IntPtr.Zero)
            {
                return 0;
            }

            windowProc = OnMessage;
            var instance = GetModuleHandleW(null);
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(),
                WindowProc = Marshal.GetFunctionPointerForDelegate(windowProc),
                Instance = instance,
                ClassName = "keycuts.RunBoxHotKey"
            };

            if (RegisterClassExW(ref windowClass) == 0)
            {
                return Marshal.GetLastWin32Error();
            }

            window = CreateWindowExW(0, windowClass.ClassName, "keycuts", 0, 0, 0, 0, 0, MessageOnlyParent, IntPtr.Zero, instance, IntPtr.Zero);
            return window == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        }

        private static IntPtr OnMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WM_HOTKEY && wParam.ToInt32() == HotKeyId)
            {
                Dispatcher.UIThread.Post(() => pressed?.Invoke());
                return IntPtr.Zero;
            }

            return DefWindowProcW(hwnd, message, wParam, lParam);
        }
    }

    /// <summary>Settings' Run box shortcut on Windows (issue #306): stored in the registry and registered with <see cref="WindowsHotKey"/>.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsRunBoxShortcut : ViewModels.IRunBoxShortcut
    {
        public RunBoxHotkey? Current
        {
            get { return WindowsHotKey.Current; }
        }

        public int Status
        {
            get { return WindowsHotKey.Status; }
        }

        public bool UsesWindowsKeys
        {
            get { return true; }
        }

        public int Apply(RunBoxHotkey? hotkey)
        {
            RegistryStuff.SetRunBoxHotkey(hotkey);
            return WindowsHotKey.Register(hotkey);
        }

        public void Pause(bool paused)
        {
            WindowsHotKey.Pause(paused);
        }
    }
}
