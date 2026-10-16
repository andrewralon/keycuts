using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// One row of the manager table. Wraps a <see cref="Keycut"/> and exposes display-ready values.
    /// </summary>
    public class KeycutRowViewModel : ObservableObject
    {
        private bool isMenuOpen;

        public KeycutRowViewModel(Keycut keycut)
        {
            Keycut = keycut ?? throw new System.ArgumentNullException(nameof(keycut));
        }

        public Keycut Keycut { get; }

        public string Name
        {
            get { return Keycut.Name ?? string.Empty; }
        }

        public KeycutKind Kind
        {
            get { return Keycut.Kind; }
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

        /// <summary>Call after the background destination check has updated the keycut.</summary>
        public void RefreshDestinationStatus()
        {
            OnPropertyChanged(nameof(IsDestinationMissing));
            OnPropertyChanged(nameof(MissingTooltip));
        }
    }
}
