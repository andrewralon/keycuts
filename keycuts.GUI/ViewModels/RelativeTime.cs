using System;

namespace keycuts.GUI.ViewModels
{
    /// <summary>"3 days ago" for the Modified column (#167). Pure, so tests pass the clock.</summary>
    public static class RelativeTime
    {
        /// <summary>
        /// How long ago <paramref name="time"/> was, in one unit: "just now", "5 minutes ago", "2 hours ago", "yesterday",
        /// "3 days ago", "2 months ago", "1 year ago". A time in the future (the clock moved) reads "just now". Empty for
        /// <c>default</c>, which means the time isn't known.
        /// </summary>
        public static string Format(DateTime time, DateTime now)
        {
            if (time == default)
            {
                return string.Empty;
            }

            var age = now - time;

            if (age < TimeSpan.FromMinutes(1))
            {
                return "just now";
            }

            if (age < TimeSpan.FromHours(1))
            {
                return Plural((int)age.TotalMinutes, "minute");
            }

            if (age < TimeSpan.FromDays(1))
            {
                return Plural((int)age.TotalHours, "hour");
            }

            var days = (int)age.TotalDays;

            if (days < 2)
            {
                return "yesterday";
            }

            if (days < 30)
            {
                return Plural(days, "day");
            }

            if (days < 365)
            {
                return Plural(days / 30, "month");
            }

            return Plural(days / 365, "year");
        }

        /// <summary>The full date and time, for the tooltip. Empty for <c>default</c>.</summary>
        public static string FormatFull(DateTime time)
        {
            return time == default ? string.Empty : time.ToString("f");
        }

        private static string Plural(int count, string unit)
        {
            return count + " " + unit + (count == 1 ? "" : "s") + " ago";
        }
    }
}
