using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace keycuts.GUI
{
    /// <summary>
    /// Reads a target from drag-and-drop or clipboard data (WPF's DragDrop). Priority: files (first item wins), then virtual shell
    /// items (as a <c>::{CLSID}</c> key), then browser links, then text.
    /// </summary>
    public static class TargetData
    {
        public static readonly DataFormat<byte[]> ShellIdListFormat = DataFormat.CreateBytesPlatformFormat(ShellIdList.Format);

        public static readonly DataFormat<byte[]> UrlFormatUnicode = DataFormat.CreateBytesPlatformFormat("UniformResourceLocatorW");

        public static readonly DataFormat<byte[]> UrlFormatAnsi = DataFormat.CreateBytesPlatformFormat("UniformResourceLocator");

        /// <summary>What a drop or the clipboard holds, in each format keycuts reads.</summary>
        public sealed record Contents(string? File, byte[]? ShellIdList, byte[]? UrlUnicode, byte[]? UrlAnsi, string? Text);

        public static bool HasSupportedData(IDataTransfer? data)
        {
            return TryGetTarget(data, out _);
        }

        /// <summary>Gets the raw target text from a drop, or false if none of the supported formats holds usable text.</summary>
        public static bool TryGetTarget(IDataTransfer? data, out string target)
        {
            target = data == null ? string.Empty : Pick(Read(data), GetParsingName) ?? string.Empty;
            return target.Length > 0;
        }

        /// <summary>The clipboard's target, or null.</summary>
        public static async Task<string?> GetTargetAsync(IAsyncDataTransfer? data)
        {
            if (data == null)
            {
                return null;
            }

            var contents = new Contents(
                await Safe(async () => LocalPath((await data.TryGetFilesAsync())?.FirstOrDefault())),
                await Safe(() => TryGet(data, ShellIdListFormat)),
                await Safe(() => TryGet(data, UrlFormatUnicode)),
                await Safe(() => TryGet(data, UrlFormatAnsi)),
                await Safe(() => data.TryGetTextAsync()));
            return Pick(contents, GetParsingName);
        }

        /// <summary>The clipboard's first file, or null: a plain text box can't paste files.</summary>
        public static async Task<string?> GetFirstFileAsync(IAsyncDataTransfer? data)
        {
            return data == null ? null : await Safe(async () => LocalPath((await data.TryGetFilesAsync())?.FirstOrDefault()));
        }

        public static Contents Read(IDataTransfer data)
        {
            return new Contents(
                SafeSync(() => LocalPath(data.TryGetFiles()?.FirstOrDefault())),
                SafeSync(() => data.Contains(ShellIdListFormat) ? data.TryGetValue(ShellIdListFormat) : null),
                SafeSync(() => data.Contains(UrlFormatUnicode) ? data.TryGetValue(UrlFormatUnicode) : null),
                SafeSync(() => data.Contains(UrlFormatAnsi) ? data.TryGetValue(UrlFormatAnsi) : null),
                SafeSync(() => data.TryGetText()));
        }

        /// <summary>The target in the order keycuts prefers, with the shell's parsing-name lookup passed in for tests.</summary>
        public static string? Pick(Contents contents, Func<byte[], string?> getParsingName)
        {
            var candidates = new Func<string?>[]
            {
                () => contents.File,
                () => GetShellItem(contents.ShellIdList, getParsingName),
                () => contents.UrlUnicode == null ? null : DecodeUrlData(contents.UrlUnicode, Encoding.Unicode),
                () => contents.UrlAnsi == null ? null : DecodeUrlData(contents.UrlAnsi, Encoding.UTF8),
                () => contents.Text
            };

            foreach (var candidate in candidates)
            {
                var value = SafeSync(candidate);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>Decodes a Windows URL clipboard format: a null-terminated string.</summary>
        public static string DecodeUrlData(byte[] bytes, Encoding encoding)
        {
            var text = encoding.GetString(bytes);
            var end = text.IndexOf('\0');
            return end >= 0 ? text.Substring(0, end) : text;
        }

        /// <summary>
        /// A virtual shell item with no file path, like the Recycle Bin (#9), as its <c>::{CLSID}</c> key. Any other parsing name
        /// (a library, a path) is ignored.
        /// </summary>
        private static string? GetShellItem(byte[]? cida, Func<byte[], string?> getParsingName)
        {
            var item = ShellIdList.GetFirstItem(cida);
            var name = item == null ? null : getParsingName(item);

            // Only a bare key: "::" and one braced GUID, not a nested path like ::{...}\::{...}
            return name != null && name.StartsWith("::{", StringComparison.Ordinal) && Guid.TryParseExact(name.Substring(2), "B", out _) ? name : null;
        }

        private static string? GetParsingName(byte[] idList)
        {
            return OperatingSystem.IsWindows() ? ShellIdList.GetParsingName(idList) : null;
        }

        private static string? LocalPath(IStorageItem? item)
        {
            return TrimFolderSlash(item?.TryGetLocalPath());
        }

        /// <summary>
        /// macOS hands over a folder with a trailing <c>/</c> (Phase 0.4): taken off, so a dropped folder is the same target as a
        /// typed one (the root, <c>/</c>, stays). Windows paths are left as they are.
        /// </summary>
        public static string? TrimFolderSlash(string? path)
        {
            return path != null && path.Length > 1 && path.EndsWith('/') && !path.Contains('\\') ? path.TrimEnd('/') : path;
        }

        private static async Task<byte[]?> TryGet(IAsyncDataTransfer data, DataFormat<byte[]> format)
        {
            return data.Contains(format) ? await data.TryGetValueAsync(format) : null;
        }

        private static async Task<T?> Safe<T>(Func<Task<T?>> read)
        {
            try
            {
                return await read();
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is IOException || ex is System.Runtime.InteropServices.COMException)
            {
                return default;
            }
        }

        private static T? SafeSync<T>(Func<T?> read)
        {
            try
            {
                return read();
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is IOException || ex is System.Runtime.InteropServices.COMException)
            {
                return default;
            }
        }
    }
}
