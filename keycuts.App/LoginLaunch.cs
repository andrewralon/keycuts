namespace keycuts.GUI
{
    /// <summary>
    /// Whether a macOS start is the login's (issue #294). No Apple documentation says how; this follows what another app that hit
    /// the same bug does (TokenTracker #989/#1005) plus a check of the parent process of its own. A start by Finder, the Dock or
    /// <c>open</c> carries a launch Apple event; a login item registered with <c>SMAppService.mainApp</c> carries none.
    /// </summary>
    public static class LoginLaunch
    {
        /// <param name="eventPresent">There is a current launch Apple event.</param>
        /// <param name="legacyLoginItem">That event says it is a login item's launch ('lgit').</param>
        /// <param name="registered">keycuts is registered as a login item (<c>SMAppService.mainApp.status</c> enabled).</param>
        /// <param name="parentIsLaunchd">The parent process is launchd (pid 1), so not a shell or an IDE.</param>
        public static bool Decide(bool eventPresent, bool legacyLoginItem, bool registered, bool parentIsLaunchd)
        {
            if (legacyLoginItem)
            {
                return true;
            }

            if (eventPresent)
            {
                return false;
            }

            return registered && parentIsLaunchd;
        }
    }
}
