using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace keycuts.GUI
{
    /// <summary>
    /// Reads a destination from drag-and-drop or clipboard data. Priority: files (first item wins), then browser links, then text.
    /// </summary>
    public static class DragDrop
    {
        public const string UrlFormatUnicode = "UniformResourceLocatorW";

        public const string UrlFormatAnsi = "UniformResourceLocator";

        public static bool HasSupportedData(IDataObject? data)
        {
            return TryGetDestination(data, out _);
        }

        /// <summary>Gets the raw destination text from the data, or false if none of the supported formats holds usable text.</summary>
        public static bool TryGetDestination(IDataObject? data, out string destination)
        {
            destination = string.Empty;
            if (data == null)
            {
                return false;
            }

            var candidates = new Func<string?>[]
            {
                () => GetFirstFile(data),
                () => GetUrl(data, UrlFormatUnicode, Encoding.Unicode),
                () => GetUrl(data, UrlFormatAnsi, Encoding.UTF8),
                () => GetText(data, DataFormats.UnicodeText),
                () => GetText(data, DataFormats.Text)
            };

            foreach (var candidate in candidates)
            {
                string? value;
                try
                {
                    value = candidate();
                }
                catch (Exception ex) when (ex is InvalidCastException || ex is IOException || ex is System.Runtime.InteropServices.COMException)
                {
                    value = null;
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    destination = value!;
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the data carries a file list (used by paste, where a plain text box can't take files).</summary>
        public static bool TryGetFirstFile(IDataObject? data, out string path)
        {
            path = data == null ? string.Empty : GetFirstFile(data) ?? string.Empty;
            return path.Length > 0;
        }

        /// <summary>Decodes a Windows URL clipboard format: a null-terminated string in a stream, byte array, or string.</summary>
        public static string? DecodeUrlData(object? raw, Encoding encoding)
        {
            byte[]? bytes;
            switch (raw)
            {
                case string text:
                    return TrimAtNull(text);
                case MemoryStream memory:
                    bytes = memory.ToArray();
                    break;
                case Stream stream:
                    using (var copy = new MemoryStream())
                    {
                        stream.CopyTo(copy);
                        bytes = copy.ToArray();
                    }

                    break;
                case byte[] array:
                    bytes = array;
                    break;
                default:
                    return null;
            }

            return TrimAtNull(encoding.GetString(bytes));
        }

        private static string? GetFirstFile(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop, false))
            {
                return null;
            }

            return (data.GetData(DataFormats.FileDrop, false) as string[])?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
        }

        private static string? GetUrl(IDataObject data, string format, Encoding encoding)
        {
            return data.GetDataPresent(format, false) ? DecodeUrlData(data.GetData(format, false), encoding) : null;
        }

        private static string? GetText(IDataObject data, string format)
        {
            return data.GetDataPresent(format, false) ? data.GetData(format, false) as string : null;
        }

        private static string TrimAtNull(string text)
        {
            var end = text.IndexOf('\0');
            return end >= 0 ? text.Substring(0, end) : text;
        }
    }
}
