using System;
using System.Threading.Tasks;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>Where the edit form should put the keyboard focus after a failed save.</summary>
    public enum EditFocusTarget
    {
        Target,
        Name
    }

    /// <summary>
    /// The Manager's Edit form (#180): the Creator's target, name and description, filled in from a keycut. Save checks them
    /// as the Creator and the rename do (the target is resolved, a name another command uses asks first, a taken name is
    /// refused) and writes the file again. Errors stay beside the field they're about; the form stays open until a save works.
    /// </summary>
    public class EditKeycutViewModel : ObservableObject
    {
        public const string NotEditableMessage = "This keycut can’t be edited here. Edit it in Notepad.";

        public const string MissingMessage = "This keycut no longer exists.";

        private readonly Keycut original;

        private readonly Func<KeycutEdit, Task<Keycut>> saveAsync;

        private readonly Func<string, NameCheck>? checkName;

        private readonly Func<NameCheck, bool>? confirmName;

        private string target;

        private string name;

        private string description;

        private string targetError = string.Empty;

        private string nameError = string.Empty;

        private string saveError = string.Empty;

        private bool isSaving;

        /// <param name="keycut">The keycut being edited; its values fill the form.</param>
        /// <param name="saveAsync">Writes the edit and returns the keycut as saved (<see cref="KeycutRepository.UpdateAsync"/>
        /// through <see cref="IKeycutActions"/>). It throws <see cref="KeycutRepositoryException"/> for what the form can explain.</param>
        /// <param name="checkName">What else answers to a new name (#153); null checks nothing.</param>
        /// <param name="confirmName">Asks to go ahead with a name another command uses; true goes ahead. Without one such a
        /// name stays in the form, so nothing is renamed unasked.</param>
        public EditKeycutViewModel(
            Keycut keycut,
            Func<KeycutEdit, Task<Keycut>> saveAsync,
            Func<string, NameCheck>? checkName = null,
            Func<NameCheck, bool>? confirmName = null)
        {
            original = keycut ?? throw new ArgumentNullException(nameof(keycut));
            this.saveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
            this.checkName = checkName;
            this.confirmName = confirmName;

            target = keycut.Target ?? string.Empty;
            name = keycut.Name ?? string.Empty;
            description = keycut.Description ?? string.Empty;

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised to close the dialog: true after a save (or when there was nothing to save), false for Cancel.</summary>
        public event EventHandler<bool>? CloseRequested;

        /// <summary>Raised after a failed save so the view can put the focus on the field to fix.</summary>
        public event EventHandler<EditFocusTarget>? FocusRequested;

        public AsyncRelayCommand SaveCommand { get; }

        public ICommand CancelCommand { get; }

        /// <summary>The window title.</summary>
        public string Title
        {
            get { return "Edit keycut"; }
        }

        /// <summary>The keycut as it was loaded.</summary>
        public Keycut Original
        {
            get { return original; }
        }

        /// <summary>The keycut as saved; null until a save has written the file (and after a close with no changes).</summary>
        public Keycut? Saved { get; private set; }

        public string Target
        {
            get { return target; }
            set
            {
                if (SetProperty(ref target, value ?? string.Empty))
                {
                    ClearErrors(target: true);
                }
            }
        }

        public string Name
        {
            get { return name; }
            set
            {
                if (SetProperty(ref name, value ?? string.Empty))
                {
                    ClearErrors(name: true);
                }
            }
        }

        public string Description
        {
            get { return description; }
            set
            {
                if (SetProperty(ref description, value ?? string.Empty))
                {
                    ClearErrors();
                }
            }
        }

        /// <summary>Why the target was refused, beside the target field; empty when there is no problem.</summary>
        public string TargetError
        {
            get { return targetError; }
        }

        public bool HasTargetError
        {
            get { return targetError.Length > 0; }
        }

        /// <summary>Why the name was refused, beside the name field; empty when there is no problem.</summary>
        public string NameError
        {
            get { return nameError; }
        }

        public bool HasNameError
        {
            get { return nameError.Length > 0; }
        }

        /// <summary>A problem that isn't about one field (the file is gone, can't be edited here, couldn't be written).</summary>
        public string SaveError
        {
            get { return saveError; }
        }

        public bool HasSaveError
        {
            get { return saveError.Length > 0; }
        }

        public bool IsSaving
        {
            get { return isSaving; }
            private set
            {
                if (SetProperty(ref isSaving, value))
                {
                    OnPropertyChanged(nameof(IsInputEnabled));
                    OnPropertyChanged(nameof(CanSave));
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>The fields are off while saving.</summary>
        public bool IsInputEnabled
        {
            get { return !isSaving; }
        }

        /// <summary>Save needs a target and a name, as Create does.</summary>
        public bool CanSave
        {
            get { return !isSaving && !string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(name); }
        }

        /// <summary>Whether any field differs from the keycut as loaded (whitespace around the values doesn't count).</summary>
        public bool HasChanges
        {
            get
            {
                return !string.Equals(target.Trim(), original.Target ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(name.Trim(), original.Name ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(description.Trim(), original.Description ?? string.Empty, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Saves the form. With nothing changed it just closes, and the file isn't touched (its modified date stays). A name
        /// that is invalid, a target that is refused, a taken name, or a name another command uses that isn't confirmed
        /// keeps the form open at the field to fix.
        /// </summary>
        public async Task SaveAsync()
        {
            var newTarget = target.Trim();
            var newName = name.Trim();
            var newDescription = description.Trim();

            SetErrors(string.Empty, string.Empty, string.Empty);

            if (!HasChanges)
            {
                CloseRequested?.Invoke(this, true);
                return;
            }

            if (!KeycutCreator.IsValidName(newName))
            {
                SetErrors(string.Empty, CreatorViewModel.GetInvalidNameMessage(newName), string.Empty);
                FocusRequested?.Invoke(this, EditFocusTarget.Name);
                return;
            }

            IsSaving = true;

            try
            {
                if (!await ConfirmNameAsync(newName))
                {
                    FocusRequested?.Invoke(this, EditFocusTarget.Name);
                    return;
                }

                Saved = await saveAsync(new KeycutEdit(newTarget, newName, newDescription));
            }
            catch (KeycutRepositoryException ex)
            {
                Explain(ex, newName);
                return;
            }
            catch (Exception ex)
            {
                SetErrors(string.Empty, string.Empty, "Couldn’t save “" + original.Name + "”: " + ex.Message);
                return;
            }
            finally
            {
                IsSaving = false;
            }

            CloseRequested?.Invoke(this, true);
        }

        /// <summary>
        /// A new name another command uses asks first (#153), as a rename does: false when it wasn't confirmed (or nobody
        /// could be asked). A case-only change, or the same name, checks nothing.
        /// </summary>
        private async Task<bool> ConfirmNameAsync(string newName)
        {
            if (checkName == null || string.Equals(newName, original.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            NameCheck check;

            try
            {
                check = await Task.Run(() => checkName(newName)) ?? NameCheck.None(newName);
            }
            catch (Exception)
            {
                // A check that fails warns about nothing
                check = NameCheck.None(newName);
            }

            if (check.Conflicts.Count == 0)
            {
                return true;
            }

            // The confirmation is modal over this form, so nothing here can be changed while it's open
            return confirmName?.Invoke(check) == true;
        }

        private void Explain(KeycutRepositoryException ex, string newName)
        {
            switch (ex.Error)
            {
                case KeycutRepositoryError.InvalidName:
                    SetErrors(string.Empty, CreatorViewModel.GetInvalidNameMessage(newName), string.Empty);
                    FocusRequested?.Invoke(this, EditFocusTarget.Name);
                    break;
                case KeycutRepositoryError.AlreadyExists:
                    SetErrors(string.Empty, "“" + newName + "” already exists. Choose another name.", string.Empty);
                    FocusRequested?.Invoke(this, EditFocusTarget.Name);
                    break;
                case KeycutRepositoryError.InvalidTarget:
                    SetErrors(CreatorViewModel.GetTargetErrorMessage(ex.TargetError), string.Empty, string.Empty);
                    FocusRequested?.Invoke(this, EditFocusTarget.Target);
                    break;
                case KeycutRepositoryError.NotEditable:
                    SetErrors(string.Empty, string.Empty, NotEditableMessage);
                    break;
                case KeycutRepositoryError.NotFound:
                    SetErrors(string.Empty, string.Empty, MissingMessage);
                    break;
                default:
                    var reason = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    SetErrors(string.Empty, string.Empty, "Couldn’t save “" + original.Name + "”: " + reason);
                    break;
            }
        }

        // Typing in a field clears what was said about it, and any general error
        private void ClearErrors(bool target = false, bool name = false)
        {
            SetErrors(target ? string.Empty : targetError, name ? string.Empty : nameError, string.Empty);
            OnPropertyChanged(nameof(CanSave));
            SaveCommand.RaiseCanExecuteChanged();
        }

        private void SetErrors(string forTarget, string forName, string forSave)
        {
            var targetChanged = targetError != forTarget;
            var nameChanged = nameError != forName;
            var saveChanged = saveError != forSave;

            targetError = forTarget;
            nameError = forName;
            saveError = forSave;

            if (targetChanged)
            {
                OnPropertyChanged(nameof(TargetError));
                OnPropertyChanged(nameof(HasTargetError));
            }

            if (nameChanged)
            {
                OnPropertyChanged(nameof(NameError));
                OnPropertyChanged(nameof(HasNameError));
            }

            if (saveChanged)
            {
                OnPropertyChanged(nameof(SaveError));
                OnPropertyChanged(nameof(HasSaveError));
            }
        }
    }
}
