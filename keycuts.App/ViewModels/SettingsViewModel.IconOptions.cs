using System.Collections;
using System.Linq;
using keycuts.GUI.Themes;

namespace keycuts.GUI.ViewModels
{
    /// <summary>The Avalonia app's half of <see cref="SettingsViewModel"/> (docs/macos-port.md, correction 15).</summary>
    public partial class SettingsViewModel
    {
        /// <summary>
        /// The picker's choices in order, grouped by <see cref="AppIconOption.Group"/>. Avalonia has no grouped collection view, so
        /// the groups are the list's items; the icon picker (Phase 3.8) lays them out.
        /// </summary>
        private static partial IEnumerable CreateIconOptionsView()
        {
            return AppIcons.Options.GroupBy(option => option.Group).ToList();
        }
    }
}
