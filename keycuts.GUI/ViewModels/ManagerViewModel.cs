using System.Windows.Input;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// State of the manager. The table, search filtering and row menus are added by KC-12 to KC-14.
    /// </summary>
    public class ManagerViewModel : ObservableObject
    {
        private string searchText = string.Empty;

        private bool isMenuOpen;

        private int keycutCount;

        public ManagerViewModel(ICommand showCreatorCommand, ICommand openSettingsCommand)
        {
            ShowCreatorCommand = showCreatorCommand;
            OpenSettingsCommand = openSettingsCommand;
        }

        public ICommand ShowCreatorCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public string SearchText
        {
            get { return searchText; }
            set { SetProperty(ref searchText, value ?? string.Empty); }
        }

        /// <summary>True while a row actions menu is open. Escape closes it before anything else.</summary>
        public bool IsMenuOpen
        {
            get { return isMenuOpen; }
            set { SetProperty(ref isMenuOpen, value); }
        }

        public int KeycutCount
        {
            get { return keycutCount; }
            set { SetProperty(ref keycutCount, value); }
        }
    }
}
