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
            EditCommand = new RelayCommand(() => _ = owner?.EditKeycutAsync(this));
            EditInNotepadCommand = new RelayCommand(() => owner?.EditInNotepad(this));
            OpenFileLocationCommand = new RelayCommand(() => owner?.OpenFileLocation(this));
            CopyTargetCommand = new RelayCommand(() => owner?.CopyTarget(this));
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
            get { return IsTargetMissing && !isRenaming; }
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

        /// <summary>Edit: the form with target, name and description (#180).</summary>
        public ICommand EditCommand { get; }

        /// <summary>Edit in Notepad: the raw .bat, for hand edits.</summary>
        public ICommand EditInNotepadCommand { get; }

        public ICommand OpenFileLocationCommand { get; }

        public ICommand CopyTargetCommand { get; }

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

        public string Target
        {
            get { return Keycut.Target ?? string.Empty; }
        }

        /// <summary>When the .bat was last written; sorted by. <c>default</c> when unknown.</summary>
        public System.DateTime Modified
        {
            get { return Keycut.Modified; }
        }

        /// <summary>"3 days ago", as of now; empty when the time isn't known. See <see cref="RefreshModified"/>.</summary>
        public string ModifiedText
        {
            get { return RelativeTime.Format(Keycut.Modified, System.DateTime.Now); }
        }

        /// <summary>The full date and time, shown in a tooltip over <see cref="ModifiedText"/>.</summary>
        public string ModifiedTooltip
        {
            get { return RelativeTime.FormatFull(Keycut.Modified); }
        }

        public bool IsTargetMissing
        {
            get { return Keycut.TargetStatus == TargetStatus.Missing; }
        }

        /// <summary>
        /// Says what the check looked for (#203): the path with its environment variables expanded, so a stale
        /// <c>Program Files (x86)</c> is plain to see, or the bare program name with where it was searched for. Before a
        /// check has said (a keycut built by hand) it falls back to the target as written.
        /// </summary>
        public string MissingTooltip
        {
            get
            {
                var lookedFor = string.IsNullOrEmpty(Keycut.CheckedPath) ? Target : Keycut.CheckedPath;
                var searched = Keycut.RawType == ShortcutType.File && lookedFor.IndexOfAny(new[] { '\\', '/', ':' }) < 0;

                return "Not found: " + lookedFor + (searched ? " (searched the Windows folder, System32, PATH and App Paths)" : string.Empty);
            }
        }

        /// <summary>True while this row's actions menu is open; the row keeps its hover tint.</summary>
        public bool IsMenuOpen
        {
            get { return isMenuOpen; }
            set { SetProperty(ref isMenuOpen, value); }
        }

        /// <summary>
        /// Case-insensitive substring match over name, type label, description and target.
        /// Blank search text matches everything.
        /// </summary>
        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            var term = search.Trim();
            return Contains(Name, term) || Contains(TypeLabel, term) || Contains(Description, term) || Contains(Target, term);
        }

        private static bool Contains(string value, string term)
        {
            return value.Contains(term, System.StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>Call on a reload that kept the row, so "3 days ago" doesn't go stale while the window stays open.</summary>
        public void RefreshModified()
        {
            OnPropertyChanged(nameof(ModifiedText));
        }

        /// <summary>Call after the background target check has updated the keycut.</summary>
        public void RefreshTargetStatus()
        {
            OnPropertyChanged(nameof(IsTargetMissing));
            OnPropertyChanged(nameof(ShowMissingIndicator));
            OnPropertyChanged(nameof(MissingTooltip));
        }
    }
}
