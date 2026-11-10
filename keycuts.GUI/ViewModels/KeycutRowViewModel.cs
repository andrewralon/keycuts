using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// One row of the manager table. Wraps a <see cref="Keycut"/> and exposes display-ready values.
    /// </summary>
    public class KeycutRowViewModel : ObservableObject
    {
        private bool isMenuOpen;

        private bool isRenaming;

        private string editName = string.Empty;

        public KeycutRowViewModel(Keycut keycut, ManagerViewModel? owner = null)
        {
            Keycut = keycut ?? throw new System.ArgumentNullException(nameof(keycut));

            OpenCommand = new RelayCommand(() => owner?.OpenKeycut(this));
            EditCommand = new RelayCommand(() => owner?.EditKeycut(this));
            OpenFileLocationCommand = new RelayCommand(() => owner?.OpenFileLocation(this));
            CopyDestinationCommand = new RelayCommand(() => owner?.CopyDestination(this));
            DeleteCommand = new RelayCommand(() => _ = owner?.DeleteKeycutAsync(this));
            RenameCommand = new RelayCommand(() => owner?.BeginRename(this));
        }

        public ICommand RenameCommand { get; }

        /// <summary>True while the name cell shows a text box (see <see cref="ManagerViewModel.BeginRename"/>).</summary>
        public bool IsRenaming
        {
            get { return isRenaming; }
            private set
            {
                if (SetProperty(ref isRenaming, value))
                {
                    OnPropertyChanged(nameof(ShowMissingIndicator));
                }
            }
        }

        /// <summary>The Missing marker under the name; hidden while renaming, since the name box alone fills the row.</summary>
        public bool ShowMissingIndicator
        {
            get { return IsDestinationMissing && !isRenaming; }
        }

        /// <summary>The name being typed while renaming.</summary>
        public string EditName
        {
            get { return editName; }
            set { SetProperty(ref editName, value ?? string.Empty); }
        }

        /// <summary>Accessible name of the rename text box.</summary>
        public string RenameLabel
        {
            get { return "New name for " + Name; }
        }

        internal void StartRename()
        {
            EditName = Name;
            IsRenaming = true;
        }

        internal void EndRename()
        {
            IsRenaming = false;
        }

        public Keycut Keycut { get; }

        public ICommand OpenCommand { get; }

        public ICommand EditCommand { get; }

        public ICommand OpenFileLocationCommand { get; }

        public ICommand CopyDestinationCommand { get; }

        public ICommand DeleteCommand { get; }

        /// <summary>Accessible name of the row's three-dot menu button.</summary>
        public string ActionsLabel
        {
            get { return "Actions for " + Name; }
        }

        public string Name
        {
            get { return Keycut.Name ?? string.Empty; }
        }

        public KeycutKind Kind
        {
            get { return Keycut.Kind; }
        }

        /// <summary>The label shown in the type badge; searched and sorted by.</summary>
        public string TypeLabel
        {
            get { return TypeBadge.GetLabel(Kind); }
        }

        public string Description
        {
            get { return Keycut.Description ?? string.Empty; }
        }

        public string Destination
        {
            get { return Keycut.Destination ?? string.Empty; }
        }

        public bool IsDestinationMissing
        {
            get { return Keycut.DestinationStatus == DestinationStatus.Missing; }
        }

        public string MissingTooltip
        {
            get { return "Destination not found: " + Destination; }
        }

        /// <summary>True while this row's actions menu is open; the row keeps its hover tint.</summary>
        public bool IsMenuOpen
        {
            get { return isMenuOpen; }
            set { SetProperty(ref isMenuOpen, value); }
        }

        /// <summary>
        /// Case-insensitive substring match over name, type label, description and destination.
        /// Blank search text matches everything.
        /// </summary>
        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            var term = search.Trim();
            return Contains(Name, term) || Contains(TypeLabel, term) || Contains(Description, term) || Contains(Destination, term);
        }

        private static bool Contains(string value, string term)
        {
            return value.Contains(term, System.StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>Call after the background destination check has updated the keycut.</summary>
        public void RefreshDestinationStatus()
        {
            OnPropertyChanged(nameof(IsDestinationMissing));
            OnPropertyChanged(nameof(ShowMissingIndicator));
            OnPropertyChanged(nameof(MissingTooltip));
        }
    }
}
