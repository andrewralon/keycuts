using System;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The macOS torn corner (issue #292): whether the window shows it. Off by default, so macOS keeps its native window; Settings
    /// turns it on and the main window follows <see cref="Changed"/>. The Windows window always has the corner and ignores this.
    /// </summary>
    public sealed class TornCornerSetting : ITornCorner
    {
        /// <summary>What the main window reads; the app sets it from the settings file at startup.</summary>
        public static bool On { get; private set; }

        public static event EventHandler? Changed;

        public static void Initialize(bool on)
        {
            On = on;
        }

        public bool IsOn
        {
            get { return On; }
        }

        public string? Set(bool on)
        {
            try
            {
                RegistryStuff.SetTornCorner(on);
            }
            catch (Exception ex)
            {
                return "Couldn’t save the torn corner setting: " + ex.Message;
            }

            Show(on);
            return null;
        }

        /// <summary>Shows or hides the corner now, without storing anything (for <see cref="Set"/> and for tests).</summary>
        public static void Show(bool on)
        {
            On = on;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
