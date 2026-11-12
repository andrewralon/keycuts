using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace keycuts.Common
{
    public class Shortcut
    {
        #region Properties

        public string Target { get; private set; }

        /// <summary>Replaces the classified target with an equivalent spelling (environment variables, #31).</summary>
        internal void SetTarget(string target)
        {
            Target = target;
        }

        public string Extension { get; private set; } = ".bat";

        public string Filename { get; private set; }

        public string FilenameWithExtension { get; private set; }

        public string Folder { get; private set; }

        public string FullPath { get; private set; }

        public string OpenWithAppPath { get; private set; }

        public bool OpenWithApp { get; private set; }

        public ShortcutType Type { get; private set; } = ShortcutType.Unknown;

        public string ShortcutsFolder { get; private set; }

        #endregion Properties

        #region Constructors

        public Shortcut(string target, string shortcut, string defaultFolder, string openWithAppPath = null)
        {
            Type = GetShortcutTypeFromTarget(target, out string newTarget);
            Target = newTarget;
            Extension = ".bat";
            Filename = GetFilename(shortcut);
            FilenameWithExtension = $"{Filename}{Extension}";
            Folder = GetFolder(shortcut, defaultFolder);
            FullPath = GetFullPath(shortcut, defaultFolder);
            OpenWithAppPath = openWithAppPath;
            OpenWithApp = openWithAppPath != null && File.Exists(openWithAppPath);
        }

        private Shortcut()
        {
        }

        /// <summary>The keycut's name (no .bat) for a shortcut argument, as the constructor reads it. Touches no files.</summary>
        public static string GetFilename(string shortcut)
        {
            return shortcut.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ?
                shortcut :
                Path.GetFileNameWithoutExtension(shortcut);
        }

        /// <summary>
        /// The keycut's bare name for a shortcut argument, without any folder. <see cref="GetFilename"/> keeps a name
        /// ending in .exe as given, folder included, so a full path like <c>C:\Shortcuts\tool.exe</c> needs this to be
        /// looked up as <c>tool.exe</c> (#153).
        /// </summary>
        public static string GetName(string shortcut)
        {
            return Path.GetFileName(GetFilename(shortcut));
        }

        /// <summary>
        /// The .bat a shortcut argument is written to, exactly as the constructor builds it. A name ending in .exe keeps
        /// any folder it was given, so <c>nested\tool.exe</c> lands in a subfolder and a rooted one replaces the folder.
        /// </summary>
        public static string GetFullPath(string shortcut, string defaultFolder)
        {
            return Path.Combine(GetFolder(shortcut, defaultFolder), GetFilename(shortcut) + ".bat");
        }

        /// <summary>The folder a shortcut argument is written to: its own if it's a full path, else <paramref name="defaultFolder"/>.</summary>
        public static string GetFolder(string shortcut, string defaultFolder)
        {
            return IsNotFullPath(shortcut) ?
                defaultFolder :
                Path.GetDirectoryName(shortcut);
        }

        /// <summary>
        /// A shortcut whose type and target are already known, so nothing on disk is checked. Lets tests emit
        /// any type, including ones the classifier never returns.
        /// </summary>
        public static Shortcut FromParts(ShortcutType type, string target, string fullPath, string openWithAppPath = null)
        {
            var folder = Path.GetDirectoryName(fullPath);
            var filename = Path.GetFileNameWithoutExtension(fullPath);

            return new Shortcut
            {
                Type = type,
                Target = target,
                Filename = filename,
                FilenameWithExtension = $"{filename}.bat",
                Folder = folder,
                FullPath = fullPath,
                OpenWithAppPath = openWithAppPath,
                OpenWithApp = openWithAppPath != null,
            };
        }

        #endregion Constructors

        #region Private Methods

        #endregion Private Methods

        #region Public Methods

        public static ShortcutType GetShortcutTypeFromTarget(string target, out string newTarget)
        {
            newTarget = target;
            var type = new ShortcutType();

            if (IsCLSIDKey(target))
            {
                type = ShortcutType.CLSIDKey;
            }
            else if (IsValidUrl(target, out string url1))
            {
                newTarget = url1;
                type = ShortcutType.Url;
            }
            else if (IsValidUrlFile(target, out string url2))
            {
                newTarget = url2;
                type = ShortcutType.Url;
            }
            else
            {
                target = Path.GetFullPath(target);
                newTarget = target;

                if (!TryGetAttributes(target, out var attributes))
                {
                    // Missing, or not readable by this user: report Unknown rather than crash (#68)
                    type = ShortcutType.Unknown;
                }
                else if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    type = ShortcutType.Folder;
                }
                else if (File.Exists(target))
                {
                    if (IsHostsFile(target))
                    {
                        type = ShortcutType.HostsFile;
                    }
                    else
                    {
                        IsLink(target, out newTarget);
                        type = ShortcutType.File;
                    }
                }
                else
                {
                    type = ShortcutType.Unknown;
                }
            }

            return type;
        }

        /// <summary>
        /// The hosts file has no default handler, so it is opened with Notepad. Compares the full path against this
        /// machine's System32 (Windows isn't always on C:) as well as the usual C: path.
        /// </summary>
        public static bool IsHostsFile(string fullPath)
        {
            return IsHostsFile(fullPath, Environment.SystemDirectory);
        }

        /// <summary>The testable part of <see cref="IsHostsFile(string)"/>.</summary>
        public static bool IsHostsFile(string fullPath, string systemDirectory)
        {
            var hosts = new[]
            {
                @"C:\Windows\System32\drivers\etc\hosts",
                Path.Combine(systemDirectory, @"drivers\etc\hosts")
            };

            return hosts.Any(h => string.Equals(h, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetAttributes(string path, out FileAttributes attributes)
        {
            try
            {
                attributes = File.GetAttributes(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                attributes = default;
                return false;
            }
        }

        public static bool IsValidUrl(string target, out string url)
        {
            url = target;
            var result = false;

            try
            {
                result = Uri.TryCreate(target, UriKind.Absolute, out Uri uriResult)
                    && !uriResult.IsFile;

                // Check for an incomplete Uri without the scheme
                //  Complete:   http://www.amazon.com
                //  Incomplete: amazon.com
                if (!result)
                {
                    var newUri = new UriBuilder(target);
                    if (uriResult != null && !uriResult.IsFile)
                    {
                        // Fix the incomplete Uri
                        url = newUri.Uri.AbsoluteUri;
                        result = true;
                    }
                }
            }
            catch (UriFormatException)
            {
                // Not a URL; the caller tries the other types
            }

            return result;
        }

        public static bool IsValidUrlFile(string target, out string url)
        {
            var result = false;
            url = target;

            if (Path.GetExtension(target).EndsWith(".url", StringComparison.InvariantCultureIgnoreCase))
            {
                // Check for a line starting with "URL="
                var line = File.ReadAllLines(target)
                    .ToList()
                    .FirstOrDefault(a => a.StartsWith("URL=", StringComparison.InvariantCultureIgnoreCase));

                // Update the target to use the URL from the link
                if (line != null)
                {
                    url = line.Substring(4);
                    if (url != null)
                    {
                        target = url;
                        result = true;
                    }
                }
            }

            return result;
        }

        public static bool IsNotFullPath(string path)
        {
            var result = false;
            if (path != Path.GetFullPath(path))
            {
                result = true;
            }
            return result;
        }

        public static bool IsLink(string target, out string link)
        {
            link = target;

            try
            {
                target = Path.GetFullPath(Environment.ExpandEnvironmentVariables(target));
                link = target;

                if (!string.Equals(Path.GetExtension(target), ".lnk", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(target))
                {
                    return false;
                }

                // Late-bound WScript.Shell avoids a COM interop reference
                var shellType = System.Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return false;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(target);
                string linkTarget = shortcut.TargetPath;
                if (string.IsNullOrEmpty(linkTarget))
                {
                    return false;
                }

                link = linkTarget;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Escapes a URL for the unquoted <c>START &lt;url&gt;</c> line so cmd passes every character through literally:
        /// each % is doubled (a .bat reads % as a variable, even inside quotes), and outside double quotes ^ &amp; | &lt; &gt;
        /// each get a ^ (cmd's escape character, so ^ itself must be escaped too). Inside quotes cmd takes those literally,
        /// including ^, so they are left alone there; each " toggles that state, as it does for cmd. The result is exactly
        /// reversible: an existing %% or ^&amp; in the URL is literal text and is escaped again.
        /// </summary>
        public static string SanitizeBatEscapeCharacters(string command)
        {
            var escaped = new StringBuilder(command.Length);
            var inQuotes = false;

            foreach (var c in command)
            {
                switch (c)
                {
                    case '%':
                        escaped.Append("%%");
                        break;
                    case '"':
                        inQuotes = !inQuotes;
                        escaped.Append(c);
                        break;
                    case '^':
                    case '&':
                    case '|':
                    case '<':
                    case '>':
                        if (!inQuotes)
                        {
                            escaped.Append('^');
                        }

                        escaped.Append(c);
                        break;
                    default:
                        escaped.Append(c);
                        break;
                }
            }

            return escaped.ToString();
        }

        public static bool IsCLSIDKey(string guid)
        {
            var result = false;

            if ((guid.StartsWith("{") || guid.StartsWith("::{") || guid.StartsWith("shell:::{")) &&
                guid.EndsWith("}"))
            {
                result = true;
            }

            return result;
        }

        public static string GetCLSIDKeyFullString(string guid)
        {
            var result = guid;

            if (guid.StartsWith("{"))
            {
                result = $"shell:::{guid}";
            }
            else if (guid.StartsWith("::{"))
            {
                result = $"shell:{guid}";
            }

            return result;
        }

        #endregion Public Methods
    }
}
