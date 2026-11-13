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

        private bool isEditingDescription;

        private string editDescription = string.Empty;

        public KeycutRowViewModel(Keycut keycut, ManagerViewModel? owner = null)
        {
            Keycut = keycut ?? throw new System.ArgumentNullException(nameof(keycut));

            OpenCommand = new RelayCommand(() => owner?.OpenKeycut(this));
            // Greyed out in the menu for a keycut the form can't save (#202); the flag is read when the keycuts load, so it
            // never changes for a row. Ctrl+E still goes to EditKeycutAsync, which says why.
            EditCommand = new RelayCommand(() => _ = owner?.EditKeycutAsync(this), () => Keycut.CanEditInForm);
            EditInNotepadCommand = new RelayCommand(() => owner?.EditInNotepad(this));
            OpenFileLocationCommand = new RelayCommand(() => owner?.OpenFileLocation(this));
            CopyTargetCommand = new RelayCommand(() => owner?.CopyTarget(this));
            DeleteCommand = new RelayCommand(() => _ = owner?.DeleteKeycutAsync(this));
            RenameCommand = new RelayCommand(() => owner?.BeginRename(this));
            EditDescriptionCommand = new RelayCommand(() => owner?.BeginEditDescription(this));
        }

        public ICommand RenameCommand { get; }

        /// <summary>"Edit description" (Shift+F2): edits the description in place, like <see cref="RenameCommand"/> does the name (#202).</summary>
        public ICommand EditDescriptionCommand { get; }

        /// <summary>True while the description cell shows a text box (see <see cref="ManagerViewModel.BeginEditDescription"/>).</summary>
        public bool IsEditingDescription
        {
            get { return isEditingDescription; }
            private set { SetProperty(ref isEditingDescription, value); }
        }

        /// <summary>The description being typed while it is edited.</summary>
        public string EditDescription
        {
            get { return editDescription; }
            set { SetProperty(ref editDescription, value ?? string.Empty); }
        }

        /// <summary>Accessible name of the description text box.</summary>
        public string DescriptionLabel
        {
            get { return "New description for " + Name; }
        }

        internal void StartEditDescription()
        {
            EditDescription = Description;
            IsEditingDescription = true;
        }

        internal void EndEditDescription()
        {
            IsEditingDescription = false;
        }

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

        /// <summary>False when the Edit form can't save this keycut without losing something: <see cref="EditCommand"/> can't run.</summary>
        public bool CanEditInForm
        {
            get { return Keycut.CanEditInForm; }
        }

        /// <summary>Why Edit is greyed out, for its tooltip, and where to go instead. Empty when Edit works.</summary>
        public string EditTooltip
        {
            get { return Keycut.CanEditInForm ? string.Empty : EditKeycutViewModel.NotEditableMessage; }
        }

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

        /// <summary>
        /// The tooltip of the Target cell (#203): the program with its arguments, as they'd be typed, where the cell itself
        /// shows the program (<see cref="Target"/>) so it stays a path to copy. A launcher such as Chrome with a profile shows
        /// which one here. The same as the target when there are no arguments.
        /// </summary>
        public string TargetTooltip
        {
            get
            {
                var program = string.IsNullOrEmpty(Keycut.Program) ? Target : Keycut.Program;

                return string.IsNullOrEmpty(Keycut.Arguments) ? Target : program + " " + Keycut.Arguments;
            }
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

                // What was searched is the check's to say: a bare name, or a program with a start-in folder, isn't one place
                return "Not found: " + lookedFor + (string.IsNullOrEmpty(Keycut.CheckedDetail) ? string.Empty : " (" + Keycut.CheckedDetail + ")");
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
