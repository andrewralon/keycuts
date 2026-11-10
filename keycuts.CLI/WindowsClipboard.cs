using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace keycuts.CLI
{
    /// <summary>
    /// Puts text on the Windows clipboard through the Win32 API (#97), so the CLI needs no WinForms or WPF. The text
    /// stays on the clipboard after the process exits.
    /// </summary>
    internal static class WindowsClipboard
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        // Another program can hold the clipboard open for a moment
        private const int OpenAttempts = 10;
        private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(50);

        /// <exception cref="Win32Exception">The clipboard couldn't be opened or set.</exception>
        public static void SetText(string text)
        {
            text ??= string.Empty;

            // EmptyClipboard makes the window that opened the clipboard its owner, and SetClipboardData is documented to
            // fail without one, so open it with a hidden message-only window that lives until the clipboard is closed.
            // Without that window, fail before touching the clipboard rather than empty it and copy nothing.
            var owner = CreateWindowEx(0, "STATIC", null, 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (owner == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                SetText(text, owner);
            }
            finally
            {
                DestroyWindow(owner);
            }
        }

        private static void SetText(string text, IntPtr owner)
        {
            OpenWithRetry(owner);

            try
            {
                if (!EmptyClipboard())
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var bytes = (text.Length + 1) * sizeof(char);
                var memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);

                if (memory == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    var target = GlobalLock(memory);

                    if (target == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                        Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
                    }
                    finally
                    {
                        GlobalUnlock(memory);
                    }

                    if (SetClipboardData(CF_UNICODETEXT, memory) == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    // The clipboard owns the memory now
                    memory = IntPtr.Zero;
                }
                finally
                {
                    if (memory != IntPtr.Zero)
                    {
                        GlobalFree(memory);
                    }
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static void OpenWithRetry(IntPtr owner)
        {
            for (var attempt = 1; ; attempt++)
            {
                if (OpenClipboard(owner))
                {
                    return;
                }

                var error = Marshal.GetLastWin32Error();

                if (attempt == OpenAttempts)
                {
                    throw new Win32Exception(error);
                }

                Thread.Sleep(OpenRetryDelay);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);
    }
}
