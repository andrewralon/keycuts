using CommandLine;

namespace keycuts.CLI
{
    /// <summary><c>keycuts install-shell-integration</c> (Phase 7): the Finder Quick Action on macOS, the Explorer verbs on Windows.</summary>
    [Verb("install-shell-integration", HelpText = "Add \"keycut this!\" to Finder's Quick Actions (macOS) or File Explorer's menus (Windows).")]
    public class InstallShellIntegrationOptions
    {
    }

    /// <summary><c>keycuts uninstall-shell-integration</c>: takes it out again.</summary>
    [Verb("uninstall-shell-integration", HelpText = "Take \"keycut this!\" out of Finder's Quick Actions (macOS) or File Explorer's menus (Windows).")]
    public class UninstallShellIntegrationOptions
    {
    }
}
