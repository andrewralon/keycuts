using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace keycuts.Common
{
    /// <summary>
    /// Non-throwing target detection for user input (drop, paste, Browse).
    /// </summary>
    public static class TargetResolver
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

        private static readonly Regex SchemelessUrl = new Regex(
            @"^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}(:\d+)?([/?#]\S*)?$",
            RegexOptions.Compiled);

        // Names like "notes.txt" look like hosts but are almost certainly files
        private static readonly HashSet<string> FileLikeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "txt", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "pdf", "exe", "bat", "cmd", "lnk", "url", "dll",
            "msi", "zip", "rar", "png", "jpg", "jpeg", "gif", "bmp", "csv", "log", "ini", "json", "xml", "md", "cs"
        };

        /// <summary>
        /// Synchronous resolution. Never throws for user input, but it touches the filesystem and can
        /// block on unreachable network paths; use <see cref="ResolveAsync"/> from UI code.
        /// </summary>
        public static TargetResult Resolve(string rawInput)
        {
            try
            {
                return ResolveCore(rawInput);
            }
            catch (UnauthorizedAccessException)
            {
                return TargetResult.Invalid(TargetError.AccessDenied, rawInput);
            }
            catch (FileNotFoundException)
            {
                return TargetResult.Invalid(TargetError.NotFound, rawInput);
            }
            catch (DirectoryNotFoundException)
            {
                return TargetResult.Invalid(TargetError.NotFound, rawInput);
            }
            catch (Exception)
            {
                return TargetResult.Invalid(TargetError.Unsupported, rawInput);
            }
        }

        /// <summary>Resolves off the calling thread; returns a Timeout error if it takes longer than the timeout.</summary>
        public static async Task<TargetResult> ResolveAsync(string rawInput, CancellationToken ct = default)
        {
            return await ResolveAsync(rawInput, DefaultTimeout, ct).ConfigureAwait(false);
        }

        public static async Task<TargetResult> ResolveAsync(string rawInput, TimeSpan timeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var work = Task.Run(() => Resolve(rawInput));
            var delay = Task.Delay(timeout, ct);
            var finished = await Task.WhenAny(work, delay).ConfigureAwait(false);

            if (finished == work)
            {
                return await work.ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            return TargetResult.Invalid(TargetError.Timeout, Clean(rawInput));
        }

        public static string Clean(string rawInput)
        {
            var text = (rawInput ?? string.Empty).Trim();

            while (text.Length >= 2 &&
                ((text[0] == '"' && text[text.Length - 1] == '"') ||
                 (text[0] == '\'' && text[text.Length - 1] == '\'')))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            return text;
        }

        private static TargetResult ResolveCore(string rawInput)
        {
            var text = Clean(rawInput);
            if (text.Length == 0)
            {
                return TargetResult.Invalid(TargetError.Empty, text);
            }

            if (Shortcut.IsCLSIDKey(text))
            {
                return TargetResult.Valid(ShortcutType.CLSIDKey, text);
            }

            if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                {
                    // file:///C:/... and file://server/share/...; plain paths such as C:\x also parse as file URIs
                    if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                    {
                        text = uri.LocalPath;
                    }
                }
                else if (uri.Scheme.Length > 1)
                {
                    return TargetResult.Valid(ShortcutType.Url, text);
                }
            }

            return ResolvePath(text);
        }

        private static TargetResult ResolvePath(string text)
        {
            var expanded = Environment.ExpandEnvironmentVariables(text);

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(expanded);
            }
            catch (Exception)
            {
                return TargetResult.Invalid(TargetError.Unsupported, text);
            }

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(fullPath);
            }
            catch (UnauthorizedAccessException)
            {
                return TargetResult.Invalid(TargetError.AccessDenied, text);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return ResolveMissing(text, expanded);
            }
            catch (Exception)
            {
                return TargetResult.Invalid(TargetError.Unsupported, text);
            }

            if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                return TargetResult.Valid(ShortcutType.Folder, fullPath);
            }

            if (Shortcut.IsHostsFile(fullPath))
            {
                return TargetResult.Valid(ShortcutType.HostsFile, fullPath);
            }

            if (string.Equals(Path.GetExtension(fullPath), ".url", StringComparison.OrdinalIgnoreCase))
            {
                var url = ReadUrlFile(fullPath);
                if (url != null)
                {
                    return TargetResult.Valid(ShortcutType.Url, url);
                }
            }

            Shortcut.IsLink(fullPath, out var target);
            return TargetResult.Valid(ShortcutType.File, target);
        }

        private static TargetResult ResolveMissing(string text, string expanded)
        {
            // Nothing on disk: accept "amazon.com" style input as a URL
            if (!Path.IsPathRooted(expanded) &&
                text.IndexOfAny(new[] { '\\', ' ', '%' }) < 0 &&
                SchemelessUrl.IsMatch(text))
            {
                var host = text.Split('/', '?', '#', ':')[0];
                var tld = host.Substring(host.LastIndexOf('.') + 1);
                if (!FileLikeExtensions.Contains(tld) &&
                    Uri.TryCreate("http://" + text, UriKind.Absolute, out var uri))
                {
                    return TargetResult.Valid(ShortcutType.Url, uri.AbsoluteUri);
                }
            }

            return TargetResult.Invalid(TargetError.NotFound, text);
        }

        private static string ReadUrlFile(string path)
        {
            try
            {
                var line = File.ReadAllLines(path)
                    .FirstOrDefault(a => a.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                return line == null ? null : line.Substring(4).Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
