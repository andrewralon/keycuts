using System;
using System.Collections.Generic;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>How a keycut matched what was typed in the Run box, best first.</summary>
    public enum RunBoxMatchKind
    {
        /// <summary>The name, in any case.</summary>
        Exact,

        /// <summary>The name starts with it.</summary>
        Prefix,

        /// <summary>The name, description or target holds it.</summary>
        Contains
    }

    public sealed record RunBoxMatch(Keycut Keycut, RunBoxMatchKind Kind);

    /// <summary>
    /// The Run box's suggestions (docs/macos-port.md, Phase 6.3): the exact name, then names that start with the text, then keycuts
    /// whose name, description or target holds it, all in any case. Within each, more runs first, then the latest run, then the
    /// name. Pure: the keycuts are what the Manager loads, run counts included.
    /// </summary>
    public static class RunBoxMatcher
    {
        /// <summary>The matches for <paramref name="text"/> (trimmed), at most <paramref name="limit"/>. Nothing for empty text.</summary>
        public static IReadOnlyList<RunBoxMatch> Find(IEnumerable<Keycut> keycuts, string text, int limit = int.MaxValue)
        {
            text = text?.Trim() ?? "";

            if (text.Length == 0 || keycuts == null || limit <= 0)
            {
                return Array.Empty<RunBoxMatch>();
            }

            return keycuts
                .Select(keycut => (Keycut: keycut, Kind: Match(keycut, text)))
                .Where(item => item.Kind != null)
                .OrderBy(item => item.Kind.Value)
                .ThenByDescending(item => item.Keycut.Runs ?? 0)
                .ThenByDescending(item => item.Keycut.LastRun)
                .ThenBy(item => item.Keycut.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(item => new RunBoxMatch(item.Keycut, item.Kind.Value))
                .ToList();
        }

        private static RunBoxMatchKind? Match(Keycut keycut, string text)
        {
            var name = keycut.Name ?? "";

            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                return RunBoxMatchKind.Exact;
            }

            if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                return RunBoxMatchKind.Prefix;
            }

            return new[] { name, keycut.Description, keycut.Target }.Any(field => field?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                ? RunBoxMatchKind.Contains
                : null;
        }
    }
}
