using System.Runtime.Versioning;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// macOS's accessibility settings as the app reads them. Avalonia 12 reports no contrast preference on macOS (checked with
    /// Increase contrast on: <c>ContrastPreference</c> stays <c>NoPreference</c>, also at startup), so <see cref="ThemeManager"/>
    /// reads <c>NSWorkspace.accessibilityDisplayShouldIncreaseContrast</c> itself.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class MacAccessibility
    {
        public static bool IncreaseContrast()
        {
            using (ObjC.AutoreleasePool())
            {
                var workspace = ObjC.Send(ObjC.GetClass("NSWorkspace"), ObjC.Selector("sharedWorkspace"));
                return ObjC.SendBool(workspace, ObjC.Selector("accessibilityDisplayShouldIncreaseContrast"));
            }
        }
    }
}
