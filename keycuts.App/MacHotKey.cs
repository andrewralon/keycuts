using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Threading;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// The Run box's global shortcut on macOS (Phase 6.2): Carbon's <c>RegisterEventHotKey</c>, which needs no Accessibility
    /// permission (Phase 0.2). One shortcut at a time; registering another replaces it. <see cref="Status"/> says whether the
    /// last one took: 0, or the OSStatus (<see cref="TakenError"/> when another app registered the same keys).
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class MacHotKey
    {
        private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

        /// <summary><c>eventHotKeyExistsErr</c>: another app has the shortcut.</summary>
        public const int TakenError = -9878;

        [StructLayout(LayoutKind.Sequential)]
        private struct EventHotKeyID
        {
            public uint Signature;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EventTypeSpec
        {
            public uint EventClass;
            public uint EventKind;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EventHandlerProc(IntPtr callRef, IntPtr eventRef, IntPtr userData);

        [DllImport(Carbon)]
        private static extern IntPtr GetApplicationEventTarget();

        [DllImport(Carbon)]
        private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyID id, IntPtr target, uint options, out IntPtr hotKeyRef);

        [DllImport(Carbon)]
        private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);

        [DllImport(Carbon)]
        private static extern int InstallEventHandler(IntPtr target, EventHandlerProc handler, uint numTypes, EventTypeSpec[] list, IntPtr userData, out IntPtr handlerRef);

        // Kept alive for as long as Carbon may call it
        private static EventHandlerProc? handler;

        private static IntPtr handlerRef;

        private static IntPtr hotKeyRef;

        private static Action? pressed;

        /// <summary>0 when the last shortcut registered, or none was asked for; otherwise Carbon's error.</summary>
        public static int Status { get; private set; }

        /// <summary>The shortcut asked for last, registered or not.</summary>
        public static RunBoxHotkey? Current { get; private set; }

        /// <summary>Raised on the UI thread after <see cref="Register"/>, so Settings can say whether it took.</summary>
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

            if (hotKeyRef != IntPtr.Zero)
            {
                UnregisterEventHotKey(hotKeyRef);
                hotKeyRef = IntPtr.Zero;
            }

            Status = hotkey == null ? 0 : Install();
            if (Status == 0 && hotkey != null)
            {
                // 'kcts', 1
                Status = RegisterEventHotKey(hotkey.KeyCode, hotkey.CarbonModifiers, new EventHotKeyID { Signature = 0x6B637473, Id = 1 },
                    GetApplicationEventTarget(), 0, out hotKeyRef);
            }

            Changed?.Invoke(null, EventArgs.Empty);
            return Status;
        }

        /// <summary>Lets go of the shortcut while Settings records a new one, keeping <see cref="Current"/>; false takes it back.</summary>
        public static void Pause(bool paused)
        {
            if (paused && hotKeyRef != IntPtr.Zero)
            {
                UnregisterEventHotKey(hotKeyRef);
                hotKeyRef = IntPtr.Zero;
            }
            else if (!paused && hotKeyRef == IntPtr.Zero)
            {
                Register(Current);
            }
        }

        private static int Install()
        {
            if (handlerRef != IntPtr.Zero)
            {
                return 0;
            }

            handler = OnEvent;
            // kEventClassKeyboard ('keyb'), kEventHotKeyPressed
            var types = new[] { new EventTypeSpec { EventClass = 0x6B657962, EventKind = 5 } };
            return InstallEventHandler(GetApplicationEventTarget(), handler, 1, types, IntPtr.Zero, out handlerRef);
        }

        private static int OnEvent(IntPtr callRef, IntPtr eventRef, IntPtr userData)
        {
            Dispatcher.UIThread.Post(() => pressed?.Invoke());
            return 0;
        }
    }
}
