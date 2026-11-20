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

            if (!OperatingSystem.IsMacOS() && Shortcut.IsCLSIDKey(text))
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
            var expanded = Environment.ExpandEnvironmentVariables(OperatingSystem.IsMacOS() ? MacTargets.ExpandHome(text) : text);

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(expanded);

                if (OperatingSystem.IsMacOS())
                {
                    fullPath = MacTargets.ResolveLinks(fullPath);
                }
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

            if (OperatingSystem.IsMacOS())
            {
                // An app is a folder that open launches; an alias needs the Finder to read it
                if (MacTargets.IsAppBundle(fullPath))
                {
                    return TargetResult.Valid(ShortcutType.File, fullPath);
                }

                if (MacTargets.IsFinderAlias(fullPath))
                {
                    return TargetResult.Invalid(TargetError.Unsupported, text);
                }

                if (MacTargets.ReadWebloc(fullPath) is string webloc)
                {
                    return TargetResult.Valid(ShortcutType.Url, webloc);
                }
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

    /// <summary>
    /// What <see cref="TargetResolver"/> does differently on macOS (docs/macos-port.md, Phase 4.4). Plain file reads, so
    /// they're tested on every platform. None of them throws.
    /// </summary>
    public static class MacTargets
    {
        // A Finder alias file holds bookmark data, which starts like this
        private static readonly byte[] BookmarkMagic = System.Text.Encoding.ASCII.GetBytes("book\0\0\0\0mark\0\0\0\0");

        private static readonly Regex WeblocUrl = new Regex(@"<key>\s*URL\s*</key>\s*<string>(?<url>[^<]*)</string>", RegexOptions.Compiled);

        /// <summary><c>~</c> and <c>~/…</c> as the home folder, as a shell reads them.</summary>
        public static string ExpandHome(string text, string home = null)
        {
            home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return text == "~" ? home : text.StartsWith("~/", StringComparison.Ordinal) ? home + text.Substring(1) : text;
        }

        /// <summary>A symbolic link's final target, so the keycut opens it even if the link goes; anything else as it is.</summary>
        public static string ResolveLinks(string path)
        {
            try
            {
                return File.ResolveLinkTarget(path, true)?.FullName ?? path;
            }
            catch (Exception)
            {
                return path;
            }
        }

        /// <summary>A folder named <c>*.app</c>: a bundle, which is opened as one file.</summary>
        public static bool IsAppBundle(string fullPath)
        {
            return fullPath.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath);
        }

        /// <summary>A Finder alias: a file of bookmark data, which only the Finder's APIs resolve.</summary>
        public static bool IsFinderAlias(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                using var stream = File.OpenRead(path);
                var start = new byte[BookmarkMagic.Length];

                return stream.Read(start, 0, start.Length) == start.Length && start.AsSpan().SequenceEqual(BookmarkMagic);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>The URL in a <c>.webloc</c> written as an XML property list; null for anything else (a binary one is left a File, which <c>open</c> opens).</summary>
        public static string ReadWebloc(string path)
        {
            try
            {
                if (!string.Equals(Path.GetExtension(path), ".webloc", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                {
                    return null;
                }

                var match = WeblocUrl.Match(File.ReadAllText(path));

                return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups["url"].Value.Trim()) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
