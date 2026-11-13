using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>
    /// Whether two targets are the same thing, for telling someone a keycut already exists "one to one" (#213). Looser than
    /// text equality: environment variables are expanded, case and a trailing slash don't count. A description or the version
    /// that wrote a keycut never matter, since only targets are compared.
    /// </summary>
    public static class TargetComparison
    {
        public static bool IsSame(string first, string second)
        {
            var left = Normalize(first);

            return left.Length > 0 && string.Equals(left, Normalize(second), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string target)
        {
            var text = TargetResolver.Clean(target);

            if (text.Length == 0)
            {
                return string.Empty;
            }

            text = Environment.ExpandEnvironmentVariables(text);

            if (text.Contains("://"))
            {
                return text.TrimEnd('/');
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
            return text.Length > 3 ? text.TrimEnd('\\', '/') : text;
        }
    }
}
