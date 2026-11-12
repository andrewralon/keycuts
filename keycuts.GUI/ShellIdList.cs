using System;
using System.Runtime.InteropServices;

namespace keycuts.GUI
{
    /// <summary>
    /// Reads the "Shell IDList Array" drag format (CFSTR_SHELLIDLIST, a CIDA), which Explorer puts on a drag of items
    /// that have no file path, like the Recycle Bin or This PC (#9). The first item's parsing name is a CLSID key such
    /// as <c>::{645FF040-5081-101B-9F08-00AA002F954E}</c>, which keycuts already accepts as a target.
    /// </summary>
    public static class ShellIdList
    {
        public const string Format = "Shell IDList Array";

        // SIGDN_DESKTOPABSOLUTEPARSING: the name a shell:::{...} path or Explorer's address bar accepts
        private const uint DesktopAbsoluteParsing = 0x80028000;

        /// <summary>
        /// The first item of a CIDA as one absolute item ID list: the parent folder's list followed by the item's. A CIDA
        /// is a count, then count + 1 offsets (the parent first, then each item), then the lists, each a run of
        /// length-prefixed IDs ending in a 2-byte zero. Null if the data is too short or an offset points outside it.
        /// </summary>
        public static byte[]? GetFirstItem(byte[]? cida)
        {
            if (cida == null || cida.Length < 12)
            {
                return null;
            }

            var count = BitConverter.ToUInt32(cida, 0);

            if (count < 1 || 4 + (count + 1L) * 4 > cida.Length)
            {
                return null;
            }

            var parentLength = GetListLength(cida, BitConverter.ToUInt32(cida, 4), out var parentOffset);
            var itemLength = GetListLength(cida, BitConverter.ToUInt32(cida, 8), out var itemOffset);

            if (parentLength < 0 || itemLength < 0)
            {
                return null;
            }

            // The parent's IDs without its terminator, then the item's IDs with theirs (what ILCombine does)
            var combined = new byte[parentLength - 2 + itemLength];
            Array.Copy(cida, parentOffset, combined, 0, parentLength - 2);
            Array.Copy(cida, itemOffset, combined, parentLength - 2, itemLength);
            return combined;
        }

        /// <summary>
        /// The desktop-absolute parsing name of an absolute item ID list, from the shell: <c>::{...}</c> for a virtual
        /// item, a path for a file. Null if the shell can't name it.
        /// </summary>
        public static string? GetParsingName(byte[] idList)
        {
            var pidl = Marshal.AllocCoTaskMem(idList.Length);

            try
            {
                Marshal.Copy(idList, 0, pidl, idList.Length);

                if (SHGetNameFromIDList(pidl, DesktopAbsoluteParsing, out var name) != 0 || name == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(name);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(name);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        /// <summary>The length of the ID list at <paramref name="offset"/>, including its terminator, or -1 if it overruns the data.</summary>
        private static int GetListLength(byte[] data, uint offset, out int start)
        {
            start = (int)Math.Min(offset, int.MaxValue);
            var position = (long)offset;

            while (position + 2 <= data.Length)
            {
                var size = BitConverter.ToUInt16(data, (int)position);

                if (size == 0)
                {
                    return (int)(position + 2 - offset);
                }

                if (size < 2)
                {
                    return -1;
                }

                position += size;
            }

            return -1;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdnName, out IntPtr ppszName);
    }
}
