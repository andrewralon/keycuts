using System;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The mutually exclusive creator states. Drag hover is a separate flag, because it overlays any state.
    /// </summary>
    public enum CreatorState
    {
        Empty,
        DestinationSelected,
        Saving,
        DuplicateName,
        InvalidDestination,
        Success
    }

    /// <summary>
    /// State of the compact creator. The input handling (KC-10) and submit flow (KC-11) set the flags and errors;
    /// the view binds to the derived properties.
    /// </summary>
    public class CreatorViewModel : ObservableObject
    {
        public const string DefaultHelperText = "Press Enter to create it";

        private string destination = string.Empty;

        private string shortcutName = string.Empty;

        private int keycutCount;

        private KeycutKind? destinationKind;

        private DestinationError destinationError;

        private bool isDragHover;

        private bool isSaving;

        private bool isBrowseMenuOpen;

        private string? duplicateName;

        private string? successName;

        public CreatorViewModel(ICommand showManagerCommand, ICommand openSettingsCommand)
        {
            ShowManagerCommand = showManagerCommand;
            OpenSettingsCommand = openSettingsCommand;
            BrowseFileCommand = new RelayCommand(() => BrowseFileRequested?.Invoke(this, EventArgs.Empty), () => IsInputEnabled);
            BrowseFolderCommand = new RelayCommand(() => BrowseFolderRequested?.Invoke(this, EventArgs.Empty), () => IsInputEnabled);
        }

        /// <summary>Raised after a keycut was created so the shell can refresh the count.</summary>
        public event EventHandler? KeycutCreated;

        /// <summary>Raised by <see cref="BrowseFileCommand"/>; the view shows the file dialog and feeds the result back.</summary>
        public event EventHandler? BrowseFileRequested;

        /// <summary>Raised by <see cref="BrowseFolderCommand"/>; the view shows the folder dialog and feeds the result back.</summary>
        public event EventHandler? BrowseFolderRequested;

        public ICommand BrowseFileCommand { get; }

        public ICommand BrowseFolderCommand { get; }

        public ICommand ShowManagerCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        /// <summary>Changing the text invalidates the resolved kind, any destination error, and the success message.</summary>
        public string Destination
        {
            get { return destination; }
            set
            {
                if (SetProperty(ref destination, value ?? string.Empty))
                {
                    destinationKind = null;
                    destinationError = DestinationError.None;
                    successName = null;
                    OnPropertyChanged(nameof(DestinationKind));
                    OnPropertyChanged(nameof(DestinationError));
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>Changing the name clears the duplicate-name error and the success message.</summary>
        public string ShortcutName
        {
            get { return shortcutName; }
            set
            {
                if (SetProperty(ref shortcutName, value ?? string.Empty))
                {
                    duplicateName = null;
                    successName = null;
                    RaiseStateChanged();
                }
            }
        }

        public int KeycutCount
        {
            get { return keycutCount; }
            set { SetProperty(ref keycutCount, value); }
        }

        /// <summary>The kind the destination resolved to. Set it after <see cref="Destination"/>, which clears it.</summary>
        public KeycutKind? DestinationKind
        {
            get { return destinationKind; }
            set
            {
                if (SetProperty(ref destinationKind, value))
                {
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>The reason the destination was rejected. Set it after <see cref="Destination"/>, which clears it.</summary>
        public DestinationError DestinationError
        {
            get { return destinationError; }
            set
            {
                if (SetProperty(ref destinationError, value))
                {
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>True while a drag is over the creator.</summary>
        public bool IsDragHover
        {
            get { return isDragHover; }
            set
            {
                if (SetProperty(ref isDragHover, value))
                {
                    RaiseStateChanged();
                }
            }
        }

        public bool IsSaving
        {
            get { return isSaving; }
            set
            {
                if (SetProperty(ref isSaving, value))
                {
                    RaiseStateChanged();
                }
            }
        }

        public bool IsBrowseMenuOpen
        {
            get { return isBrowseMenuOpen; }
            set { SetProperty(ref isBrowseMenuOpen, value); }
        }

        /// <summary>The name that already exists, while the duplicate-name error is showing.</summary>
        public string? DuplicateName
        {
            get { return duplicateName; }
        }

        /// <summary>The name that was just created, while the success confirmation is showing.</summary>
        public string? SuccessName
        {
            get { return successName; }
        }

        public CreatorState State
        {
            get
            {
                if (isSaving)
                {
                    return CreatorState.Saving;
                }

                if (successName != null)
                {
                    return CreatorState.Success;
                }

                if (HasDestinationError)
                {
                    return CreatorState.InvalidDestination;
                }

                if (duplicateName != null)
                {
                    return CreatorState.DuplicateName;
                }

                return HasDestination ? CreatorState.DestinationSelected : CreatorState.Empty;
            }
        }

        public bool HasDestination
        {
            get { return !string.IsNullOrWhiteSpace(destination); }
        }

        public bool HasDestinationError
        {
            get { return destinationError != DestinationError.None && destinationError != DestinationError.Empty; }
        }

        /// <summary>A destination that has text and was not rejected. KC-10 refines this by resolving the text.</summary>
        public bool HasValidDestination
        {
            get { return HasDestination && !HasDestinationError; }
        }

        public bool IsDuplicateName
        {
            get { return duplicateName != null; }
        }

        public bool IsSuccess
        {
            get { return successName != null && !isSaving; }
        }

        /// <summary>Create needs a valid destination and a non-empty name, and is off while saving.</summary>
        public bool CanCreate
        {
            get { return !isSaving && HasValidDestination && !string.IsNullOrWhiteSpace(shortcutName); }
        }

        /// <summary>The destination field, Browse, and the name field are off while saving; the drop area ignores drops too.</summary>
        public bool IsInputEnabled
        {
            get { return !isSaving; }
        }

        /// <summary>The drag-hover look is hidden while saving, since drops are ignored.</summary>
        public bool ShowDragHover
        {
            get { return isDragHover && !isSaving; }
        }

        /// <summary>Only destination errors turn the drop area red; name errors style just the name field.</summary>
        public bool ShowDestinationError
        {
            get { return HasDestinationError; }
        }

        public IconKind DestinationIcon
        {
            get { return GetDestinationIcon(destinationKind); }
        }

        public string HelperText
        {
            get
            {
                if (HasDestinationError)
                {
                    return GetDestinationErrorMessage(destinationError);
                }

                if (duplicateName != null)
                {
                    return string.Format("\u201C{0}\u201D already exists. Choose another name.", duplicateName);
                }

                if (IsSuccess)
                {
                    return "Created " + successName;
                }

                return DefaultHelperText;
            }
        }

        public bool HelperIsError
        {
            get { return HasDestinationError || duplicateName != null; }
        }

        /// <summary>The destination field is focused unless one is already set, in which case the name field is.</summary>
        public bool FocusNameFieldOnShow
        {
            get { return !string.IsNullOrWhiteSpace(destination); }
        }

        public static IconKind GetDestinationIcon(KeycutKind? kind)
        {
            switch (kind)
            {
                case KeycutKind.Url:
                    return IconKind.Link;
                case KeycutKind.Folder:
                    return IconKind.Folder;
                case KeycutKind.File:
                    return IconKind.File;
                case KeycutKind.Command:
                    return IconKind.Play;
                default:
                    return IconKind.Arrow;
            }
        }

        public static string GetDestinationErrorMessage(DestinationError error)
        {
            switch (error)
            {
                case DestinationError.NotFound:
                    return "That destination wasn\u2019t found.";
                case DestinationError.AccessDenied:
                    return "Access to that destination was denied.";
                case DestinationError.Unsupported:
                    return "That destination type isn\u2019t supported.";
                case DestinationError.Timeout:
                    return "That destination didn\u2019t respond in time.";
                default:
                    return DefaultHelperText;
            }
        }

        /// <summary>
        /// Applies a resolver result. A valid result replaces the text with the normalized destination and sets the kind.
        /// An invalid result is shown as an error only when <paramref name="showError"/> is true (not while the user is typing).
        /// </summary>
        public void ApplyResolved(DestinationResult result, bool showError)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.IsValid)
            {
                Destination = result.Destination;
                DestinationKind = result.Kind;
                DestinationError = DestinationError.None;
            }
            else if (showError && result.Error != DestinationError.Empty)
            {
                DestinationError = result.Error;
            }
        }

        /// <summary>Shows the duplicate-name error on the name field.</summary>
        public void SetDuplicateName(string name)
        {
            duplicateName = name ?? throw new ArgumentNullException(nameof(name));
            successName = null;
            RaiseStateChanged();
        }

        /// <summary>Shows the success confirmation (the caller clears it after a moment).</summary>
        public void SetSuccess(string name)
        {
            successName = name ?? throw new ArgumentNullException(nameof(name));
            duplicateName = null;
            RaiseStateChanged();
        }

        public void Clear()
        {
            destination = string.Empty;
            shortcutName = string.Empty;
            destinationKind = null;
            destinationError = DestinationError.None;
            duplicateName = null;
            successName = null;
            OnPropertyChanged(nameof(Destination));
            OnPropertyChanged(nameof(ShortcutName));
            OnPropertyChanged(nameof(DestinationKind));
            OnPropertyChanged(nameof(DestinationError));
            RaiseStateChanged();
        }

        public void NotifyKeycutCreated()
        {
            KeycutCreated?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseStateChanged()
        {
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(HasDestination));
            OnPropertyChanged(nameof(HasDestinationError));
            OnPropertyChanged(nameof(HasValidDestination));
            OnPropertyChanged(nameof(IsDuplicateName));
            OnPropertyChanged(nameof(DuplicateName));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(SuccessName));
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(IsInputEnabled));
            OnPropertyChanged(nameof(ShowDragHover));
            OnPropertyChanged(nameof(ShowDestinationError));
            OnPropertyChanged(nameof(DestinationIcon));
            OnPropertyChanged(nameof(HelperText));
            OnPropertyChanged(nameof(HelperIsError));
        }
    }
}
