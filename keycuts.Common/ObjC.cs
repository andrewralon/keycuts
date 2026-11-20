using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace keycuts.Common
{
    /// <summary>
    /// The few Objective-C runtime calls keycuts makes on macOS (the Trash; the app adds its own), through <c>objc_msgSend</c>
    /// with fixed argument lists. Objects returned by class convenience methods are autoreleased, so callers wrap their
    /// work in <see cref="AutoreleasePool"/>.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class ObjC
    {
        private const string Lib = "/usr/lib/libobjc.A.dylib";

        [DllImport(Lib, EntryPoint = "objc_getClass")]
        public static extern IntPtr GetClass(string name);

        [DllImport(Lib, EntryPoint = "sel_registerName")]
        public static extern IntPtr Selector(string name);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, long arg);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SendBool(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SendBool(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SendBool(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, out IntPtr error);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern long SendLong(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_autoreleasePoolPush")]
        private static extern IntPtr PoolPush();

        [DllImport(Lib, EntryPoint = "objc_autoreleasePoolPop")]
        private static extern void PoolPop(IntPtr pool);

        /// <summary><c>[NSString stringWithUTF8String:]</c>, autoreleased.</summary>
        public static IntPtr String(string value)
        {
            var utf8 = Marshal.StringToCoTaskMemUTF8(value);

            try
            {
                return Send(GetClass("NSString"), Selector("stringWithUTF8String:"), utf8);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }

        /// <summary>The text of an <c>NSString</c>, or null for nil.</summary>
        public static string ToManaged(IntPtr nsString)
        {
            return nsString == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(nsString, Selector("UTF8String")));
        }

        /// <summary>An <c>NSError</c>'s <c>localizedDescription</c>, or null for nil.</summary>
        public static string ErrorText(IntPtr error)
        {
            return error == IntPtr.Zero ? null : ToManaged(Send(error, Selector("localizedDescription")));
        }

        /// <summary>An autorelease pool for the length of a <c>using</c>.</summary>
        public static IDisposable AutoreleasePool()
        {
            return new Pool(PoolPush());
        }

        private sealed class Pool : IDisposable
        {
            private IntPtr _pool;

            public Pool(IntPtr pool)
            {
                _pool = pool;
            }

            public void Dispose()
            {
                if (_pool != IntPtr.Zero)
                {
                    PoolPop(_pool);
                    _pool = IntPtr.Zero;
                }
            }
        }
    }
}
