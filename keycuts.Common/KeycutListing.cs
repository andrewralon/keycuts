using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace keycuts.Common
{
    /// <summary>How <c>keycuts list</c> orders the keycuts (#237): the Manager's orders.</summary>
    public enum KeycutListSort
    {
        /// <summary>By name, A to Z.</summary>
        Name,

        /// <summary>The most runs first; a keycut with no record after one that never ran. Ties by name.</summary>
        Runs,

        /// <summary>The latest run first; one that never ran (or has no record) last. Ties by name.</summary>
        LastRun
    }

    /// <summary>
    /// What <c>keycuts list</c> prints (#173, #237): one line per keycut under a header, columns lined up. Name, type, target and
    /// description; <c>--usage</c> adds Runs and Last run (ISO 8601 local time, for scripts), with a dash where there is no record.
    /// Pure: the CLI loads the keycuts (through <see cref="KeycutRepository"/> with the user's <see cref="UsageLog"/>, as the GUI
    /// does) and writes the lines.
    /// </summary>
    public static class KeycutListing
    {
        public const string NoRecord = "-";

        /// <summary>The <c>--sort</c> values, as typed.</summary>
        public static readonly IReadOnlyList<string> SortNames = new[] { "name", "runs", "last-run" };

        /// <summary>Reads a <c>--sort</c> value (any case); null or empty is <see cref="KeycutListSort.Name"/>.</summary>
        public static bool TryParseSort(string text, out KeycutListSort sort)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "name":
                    sort = KeycutListSort.Name;
                    return true;
                case "runs":
                    sort = KeycutListSort.Runs;
                    return true;
                case "last-run":
                case "lastrun":
                    sort = KeycutListSort.LastRun;
                    return true;
                default:
                    sort = KeycutListSort.Name;
                    return false;
            }
        }

        /// <summary>
        /// The keycuts whose name, type, target or description holds <paramref name="search"/> (any case), as the Manager's
        /// search box matches; all of them for a blank search.
        /// </summary>
        public static IReadOnlyList<Keycut> Filter(IEnumerable<Keycut> keycuts, string search)
        {
            var term = (search ?? "").Trim();

            if (term.Length == 0)
            {
                return keycuts.ToList();
            }

            return keycuts.Where(keycut =>
                Contains(keycut.Name, term) ||
                Contains(TypeText(keycut), term) ||
                Contains(keycut.Target, term) ||
                Contains(keycut.Description, term)).ToList();
        }

        public static IReadOnlyList<Keycut> Sort(IEnumerable<Keycut> keycuts, KeycutListSort sort)
        {
            var byName = StringComparer.OrdinalIgnoreCase;

            switch (sort)
            {
                case KeycutListSort.Runs:
                    return keycuts.OrderByDescending(keycut => keycut.Runs ?? -1).ThenBy(keycut => keycut.Name, byName).ToList();
                case KeycutListSort.LastRun:
                    return keycuts.OrderByDescending(keycut => keycut.LastRun).ThenBy(keycut => keycut.Name, byName).ToList();
                default:
                    return keycuts.OrderBy(keycut => keycut.Name, byName).ToList();
            }
        }

        /// <summary>The header and one line per keycut, columns two spaces apart. The last column (Description) isn't padded.</summary>
        public static IReadOnlyList<string> Format(IReadOnlyList<Keycut> keycuts, bool usage)
        {
            var header = usage
                ? new[] { "Name", "Type", "Runs", "Last run", "Target", "Description" }
                : new[] { "Name", "Type", "Target", "Description" };
            var rows = new List<string[]> { header };

            foreach (var keycut in keycuts)
            {
                rows.Add(usage
                    ? new[] { keycut.Name ?? "", TypeText(keycut), RunsText(keycut), LastRunText(keycut), OneLine(keycut.Target), OneLine(keycut.Description) }
                    : new[] { keycut.Name ?? "", TypeText(keycut), OneLine(keycut.Target), OneLine(keycut.Description) });
            }

            var widths = Enumerable.Range(0, header.Length).Select(column => rows.Max(row => row[column].Length)).ToArray();

            return rows.Select(row =>
            {
                var line = new StringBuilder();

                for (var column = 0; column < row.Length; column++)
                {
                    var last = column == row.Length - 1;
                    line.Append(last ? row[column] : row[column].PadRight(widths[column] + 2));
                }

                return line.ToString().TrimEnd();
            }).ToList();
        }

        /// <summary>
        /// <c>keycuts list --json</c> (#173): an array with one object per keycut, for scripts. Every field is always there: <c>runs</c>
        /// and <c>lastRun</c> (ISO 8601 local time) are null without a record, <c>lastRun</c> too when it never ran. <c>target</c> is what the
        /// file says; <c>file</c> is the .bat's path.
        /// </summary>
        public static string ToJson(IReadOnlyList<Keycut> keycuts)
        {
            using var stream = new System.IO.MemoryStream();
            using (var json = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions
            {
                Indented = true,
                // Readable URLs (& and non-ASCII as themselves); this is a console's output, not HTML
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
            {
                json.WriteStartArray();

                foreach (var keycut in keycuts)
                {
                    json.WriteStartObject();
                    json.WriteString("name", keycut.Name ?? "");
                    json.WriteString("type", TypeText(keycut));
                    json.WriteString("target", (keycut.Target ?? "").Trim());
                    json.WriteString("description", keycut.Description ?? "");

                    if (keycut.Runs == null)
                    {
                        json.WriteNull("runs");
                    }
                    else
                    {
                        json.WriteNumber("runs", keycut.Runs.Value);
                    }

                    var lastRun = LastRunText(keycut);
                    if (lastRun == NoRecord)
                    {
                        json.WriteNull("lastRun");
                    }
                    else
                    {
                        json.WriteString("lastRun", lastRun);
                    }

                    json.WriteString("file", keycut.FilePath ?? "");
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>The type as the Manager's badge names it, in lower case: url, file, folder, command.</summary>
        public static string TypeText(Keycut keycut)
        {
            return keycut.Kind.ToString().ToLowerInvariant();
        }

        /// <summary>The count, or a dash for no record (the keycut doesn't count its runs).</summary>
        public static string RunsText(Keycut keycut)
        {
            return keycut.Runs?.ToString(CultureInfo.InvariantCulture) ?? NoRecord;
        }

        /// <summary>When it last ran, as <c>2026-11-15T09:30:00</c> (local time); a dash if it never ran or has no record.</summary>
        public static string LastRunText(Keycut keycut)
        {
            return keycut.Runs == null || keycut.LastRun == default
                ? NoRecord
                : keycut.LastRun.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string OneLine(string text)
        {
            return (text ?? "").Replace("\r", " ").Replace("\n", " ");
        }

        private static bool Contains(string value, string term)
        {
            return value != null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
