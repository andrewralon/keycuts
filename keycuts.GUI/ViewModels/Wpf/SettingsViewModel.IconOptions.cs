using System.Collections;
using System.Collections.Generic;
using System.Windows.Data;
using keycuts.GUI.Themes;

namespace keycuts.GUI.ViewModels
{
    /// <summary>The WPF app's half of <see cref="SettingsViewModel"/> (docs/macos-port.md, correction 15).</summary>
    public partial class SettingsViewModel
    {
        /// <summary>
        /// A view of this window's own, grouped by <see cref="AppIconOption.Group"/> for the picker's GroupStyle: the default view
        /// of the shared list would collect a group description per window.
        /// </summary>
        private static partial IEnumerable CreateIconOptionsView()
        {
            var view = new ListCollectionView(new List<AppIconOption>(AppIcons.Options));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppIconOption.Group)));
            return view;
        }
    }
}
