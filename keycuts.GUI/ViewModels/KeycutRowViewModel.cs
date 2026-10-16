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

        public KeycutRowViewModel(Keycut keycut, ManagerViewModel? owner = null)
        {
            Keycut = keycut ?? throw new System.ArgumentNullException(nameof(keycut));

            OpenCommand = new RelayCommand(() => owner?.OpenKeycut(this));
            EditCommand = new RelayCommand(() => owner?.EditKeycut(this));
            CopyDestinationCommand = new RelayCommand(() => owner?.CopyDestination(this));
            DeleteCommand = new RelayCommand(() => _ = owner?.DeleteKeycutAsync(this));
        }

        public Keycut Keycut { get; }

        public ICommand OpenCommand { get; }

        public ICommand EditCommand { get; }

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
            OnPropertyChanged(nameof(MissingTooltip));
        }
    }
}
