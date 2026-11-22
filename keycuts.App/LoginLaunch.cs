using System;
using System.Globalization;
using System.Linq;

namespace keycuts.GUI
{
    /// <summary>
    /// Whether a macOS start is the login's (issue #294), so keycuts waits in the menu bar instead of opening its window. No Apple
    /// documentation says how, and the obvious signals fail: when the app decides, there is no launch Apple event for any kind of
    /// start (a manual <c>open</c> included, measured), launchd is the parent of every app LaunchServices starts, and the app is
    /// active in both. What differs is when: a login item starts within moments of the login session, a manual launch normally
    /// much later. A manual start in the first minutes after logging in lands in the menu bar too; opening keycuts again shows the window.
    /// </summary>
    public static class LoginLaunch
    {
        /// <summary>How soon after the session started a start still counts as the login's.</summary>
        public static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(3);

        /// <param name="legacyLoginItem">The launch Apple event says it is a login item's launch ('lgit'), as a legacy login item's does.</param>
        /// <param name="registered">keycuts is registered as a login item (<c>SMAppService.mainApp.status</c> enabled).</param>
        /// <param name="parentIsLaunchd">The parent process is launchd (pid 1), so not a shell or an IDE.</param>
        /// <param name="sessionAge">How long the user's login session has run; null when it can't be read, which is never a login.</param>
        public static bool Decide(bool legacyLoginItem, bool registered, bool parentIsLaunchd, TimeSpan? sessionAge)
        {
            if (legacyLoginItem)
            {
                return true;
            }

            return registered && parentIsLaunchd && sessionAge is { } age && age >= TimeSpan.Zero && age < LoginWindow;
        }

        /// <summary>
        /// From <c>ps -c -o etime=,comm=</c> lines, the elapsed time of the first of <paramref name="names"/> that is running (the
        /// oldest, if several); null when none is.
        /// </summary>
        public static TimeSpan? OldestOf(string psOutput, params string[] names)
        {
            foreach (var name in names)
            {
                TimeSpan? oldest = null;

                foreach (var line in (psOutput ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    var space = trimmed.IndexOf(' ');

                    if (space > 0 && trimmed.Substring(space + 1).Trim() == name && ParseElapsed(trimmed.Substring(0, space)) is { } age && (oldest == null || age > oldest))
                    {
                        oldest = age;
                    }
                }

                if (oldest != null)
                {
                    return oldest;
                }
            }

            return null;
        }

        /// <summary>Reads <c>ps</c>'s elapsed time: <c>mm:ss</c>, <c>hh:mm:ss</c> or <c>dd-hh:mm:ss</c>; null when it isn't one of those.</summary>
        public static TimeSpan? ParseElapsed(string text)
        {
            text = (text ?? string.Empty).Trim();
            var days = 0;
            var dash = text.IndexOf('-');

            if (dash >= 0)
            {
                if (!int.TryParse(text.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out days))
                {
                    return null;
                }

                text = text.Substring(dash + 1);
            }

            var parts = text.Split(':');

            if (parts.Length is < 2 or > 3 || parts.Any(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                return null;
            }

            var numbers = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            var hours = numbers.Length == 3 ? numbers[0] : 0;
            return new TimeSpan(days, hours, numbers[^2], numbers[^1]);
        }
    }
}
