using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>How alike two targets are.</summary>
    public enum TargetMatch
    {
        Different,

        /// <summary>Two URLs that differ only in capitals or a trailing slash, which a server may or may not treat as the same page.</summary>
        Similar,

        Same
    }

    /// <summary>
    /// Whether two targets are the same thing, for telling someone a keycut already exists "one to one" (#213). Looser than
    /// text equality: environment variables are expanded, and a description or the version that wrote a keycut never matter,
    /// since only targets are compared. Paths ignore case and a trailing slash, as Windows does. URLs don't: only their scheme
    /// and host are case-insensitive, and a bare <c>/</c> after the host is nothing; capitals or a trailing slash anywhere in
    /// the rest can make another page, so a URL that differs only that way is <see cref="TargetMatch.Similar"/> and left to
    /// the user to judge.
    /// </summary>
    public static class TargetComparison
    {
        public static bool IsSame(string first, string second)
        {
            return Compare(first, second) == TargetMatch.Same;
        }

        public static TargetMatch Compare(string first, string second)
        {
            var left = Normalize(first);
            var right = Normalize(second);

            if (left.Key.Length == 0 || left.IsUrl != right.IsUrl)
            {
                return TargetMatch.Different;
            }

            if (!left.IsUrl)
            {
                return string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase) ? TargetMatch.Same : TargetMatch.Different;
            }

            if (string.Equals(left.Key, right.Key, StringComparison.Ordinal))
            {
                return TargetMatch.Same;
            }

            return string.Equals(left.Key.TrimEnd('/'), right.Key.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                ? TargetMatch.Similar
                : TargetMatch.Different;
        }

        private static (string Key, bool IsUrl) Normalize(string target)
        {
            var text = TargetResolver.Clean(target);

            if (text.Length == 0)
            {
                return (string.Empty, false);
            }

            text = Environment.ExpandEnvironmentVariables(text);

            if (text.Contains("://"))
            {
                // Uri lowercases the scheme and host and drops a default port; the root path is always "/"
                return Uri.TryCreate(text, UriKind.Absolute, out var uri)
                    ? (uri.GetLeftPart(UriPartial.Authority) + uri.PathAndQuery + uri.Fragment, true)
                    : (text, true);
            }

            try
            {
                text = Path.GetFullPath(text);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                // Compare it as written
            }

            // Keep the slash of a drive root (C:\), which is part of its name
            return (text.Length > 3 ? text.TrimEnd('\\', '/') : text, false);
        }
    }
}
