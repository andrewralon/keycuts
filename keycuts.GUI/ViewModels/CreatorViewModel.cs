using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        /// <summary>The name is invalid (characters Windows doesn't allow, or a keycuts command's name).</summary>
        NameError,
        /// <summary>The name would replace a keycut or clash with another command (#153); Create asks first.</summary>
        NameWarning,
        InvalidDestination,
        Success
    }

    /// <summary>Where the view should move keyboard focus after a create attempt.</summary>
    public enum CreatorFocusTarget
    {
        Destination,
        Name,
        NameSelectAll
    }

    /// <summary>
    /// State of the compact creator. The input handling (KC-10) and submit flow (KC-11) set the flags and errors;
    /// the view binds to the derived properties.
    /// </summary>
    public class CreatorViewModel : ObservableObject
    {
        public const string DefaultHelperText = "Press Enter to create it";

        public const string SavingHelperText = "Saving\u2026";

        public const string InvalidNameMessage = "Use letters, numbers, dashes, or dots \u2014 no \\ / : * ? \" < > |";

        /// <summary>A name taken by a keycuts command on PATH (keycuts, keypaste), which a keycut would clash with.</summary>
        public const string CommandNameMessage = "\u201c{0}\u201d is a keycuts command. Choose another name.";

        /// <summary>Why <paramref name="name"/> was refused as invalid: a command's name, or a name that isn't a plain file name.</summary>
        public static string GetInvalidNameMessage(string name)
        {
            return CommandNames.IsCommandName(name) ? string.Format(CommandNameMessage, name.Trim()) : InvalidNameMessage;
        }

        public const string OutputFolderErrorMessage = "Couldn\u2019t save to the output folder. Check Settings.";

        public const string PathUpdateErrorMessage = "Couldn\u2019t update your PATH. Check Settings.";

        public const string UnexpectedErrorMessage = "Something went wrong creating the keycut. Check Settings and try again.";

        /// <summary>An existing keycut the new one would replace, and what it opens now (#153).</summary>
        public const string ReplaceMessage = "\u201c{0}\u201d already exists and opens {1}.";

        /// <summary>An existing keycut whose destination couldn't be read.</summary>
        public const string ReplaceUnknownMessage = "\u201c{0}\u201d already exists.";

        /// <summary>Something found before the keycut, so it would never run from Win+R.</summary>
        public const string HiddenMessage = "\u201c{0}\u201d already opens {1}, so Win+R would never run this keycut.";

        /// <summary>A program found after the keycut, which Win+R would no longer open by this name.</summary>
        public const string ShadowsMessage = "\u201c{0}\u201d already opens {1}. This keycut would replace it in Win+R.";

        /// <summary>One line about a name conflict, as the creator, the manager and the confirmation show it.</summary>
        public static string GetConflictMessage(string name, NameConflict conflict)
        {
            var format = conflict.Effect == NameConflictEffect.Hidden ? HiddenMessage : ShadowsMessage;
            return string.Format(format, name, conflict.Path);
        }

        /// <summary>The inline warning for a name (#153): the keycut it would replace, then the conflict that matters most.</summary>
        public static string GetNameWarningMessage(NameCheck? check)
        {
            if (check == null || !check.HasWarnings)
            {
                return string.Empty;
            }

            var lines = new List<string>();

            if (check.Existing != null)
            {
                lines.Add(string.IsNullOrEmpty(check.Existing.Destination)
                    ? string.Format(ReplaceUnknownMessage, check.Name)
                    : string.Format(ReplaceMessage, check.Name, check.Existing.Destination));
            }

            if (check.MainConflict is NameConflict conflict)
            {
                lines.Add(GetConflictMessage(check.Name, conflict));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string destination = string.Empty;

        private string shortcutName = string.Empty;

        private int keycutCount;

        private KeycutKind? destinationKind;

        private DestinationError destinationError;

        private bool isDragHover;

        private bool isSaving;

        private bool isBrowseMenuOpen;

        // What the typed name runs into (#153), while the warning is showing; null when there's nothing to warn about
        private NameCheck? nameWarning;

        private string? successName;

        private bool isInvalidName;

        private string? submitError;

        private int successVersion;

        private readonly Func<string, string, bool, bool, CancellationToken, Task<CreateResult>> createAsync;

        private readonly Func<string, NameCheck> checkName;

        private bool useEnvironmentVariablesSetting;

        private bool isShiftHeld;

        // Null until the app has read them (in the background, since a custom folder on a network share can be slow)
        private IReadOnlyList<PathVariable>? pathVariables;

        // What the typed destination resolved to (a shortcut's target, a normalized path), while the text keeps what was typed
        private string? resolvedDestination;

        /// <param name="createAsync">Creates the keycut (destination, name, use environment variables, replace an existing
        /// one); defaults to <see cref="KeycutCreator"/>. Replace is true only after the user confirmed it. Tests pass a fake.</param>
        /// <param name="checkName">What a name runs into (#153), checked while typing and before each create; defaults to
        /// <see cref="NameCheck.Run"/> on this machine. Tests pass a fake.</param>
        public CreatorViewModel(
            ICommand showManagerCommand,
            ICommand openSettingsCommand,
            Func<string, string, bool, bool, CancellationToken, Task<CreateResult>>? createAsync = null,
            Func<string, NameCheck>? checkName = null)
        {
            ShowManagerCommand = showManagerCommand;
            OpenSettingsCommand = openSettingsCommand;
            this.createAsync = createAsync ?? DefaultCreateAsync;
            this.checkName = checkName ?? DefaultCheckName;
            CreateCommand = new AsyncRelayCommand(CreateAsync, () => CanCreate);
            BrowseFileCommand = new RelayCommand(() => BrowseFileRequested?.Invoke(this, EventArgs.Empty), () => IsInputEnabled);
            BrowseFolderCommand = new RelayCommand(() => BrowseFolderRequested?.Invoke(this, EventArgs.Empty), () => IsInputEnabled);
        }

        /// <summary>Raised after a keycut was created so the shell can refresh the count.</summary>
        public event EventHandler? KeycutCreated;

        /// <summary>Raised by <see cref="BrowseFileCommand"/>; the view shows the file dialog and feeds the result back.</summary>
        public event EventHandler? BrowseFileRequested;

        /// <summary>Raised by <see cref="BrowseFolderCommand"/>; the view shows the folder dialog and feeds the result back.</summary>
        public event EventHandler? BrowseFolderRequested;

        /// <summary>Raised after a create attempt so the view can move focus (it can't know the outcome).</summary>
        public event EventHandler<CreatorFocusTarget>? FocusRequested;

        /// <summary>
        /// Asks the user to go ahead despite a name warning (#153): the check and the new destination in, true to create.
        /// The view shows the confirmation dialog; tests pass a fake. Without one nothing with a warning is created.
        /// </summary>
        public Func<NameCheck, string, bool>? ConfirmName { get; set; }

        /// <summary>How long the success confirmation shows before the creator resets to Empty.</summary>
        public TimeSpan SuccessDuration { get; set; } = TimeSpan.FromSeconds(2);

        public AsyncRelayCommand CreateCommand { get; }

        public ICommand BrowseFileCommand { get; }

        public ICommand BrowseFolderCommand { get; }

        public ICommand ShowManagerCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        /// <summary>The Settings choice to save file and folder paths with environment variables (#31). The app sets it; off in tests.</summary>
        public bool UseEnvironmentVariablesSetting
        {
            get { return useEnvironmentVariablesSetting; }
            set
            {
                if (SetProperty(ref useEnvironmentVariablesSetting, value))
                {
                    RaiseSavedAsChanged();
                }
            }
        }

        /// <summary>True while Shift is down; it flips <see cref="UseEnvironmentVariablesSetting"/> for this keycut.</summary>
        public bool IsShiftHeld
        {
            get { return isShiftHeld; }
            set
            {
                if (SetProperty(ref isShiftHeld, value))
                {
                    RaiseSavedAsChanged();
                }
            }
        }

        /// <summary>
        /// The variables a path can be saved with, which a create also uses so the keycut matches the preview. The app reads
        /// them from the machine after the window shows; until then (and in tests) there is no preview, and a create reads them itself.
        /// </summary>
        public IReadOnlyList<PathVariable> PathVariables
        {
            get { return pathVariables ?? Array.Empty<PathVariable>(); }
            set
            {
                pathVariables = value;
                RaiseSavedAsChanged();
            }
        }

        /// <summary>
        /// What the destination resolved to while being typed (a shortcut's target, a normalized path), which is what is
        /// saved; the text itself isn't rewritten until it's pasted, dropped or left. Set it after <see cref="Destination"/>, which clears it.
        /// </summary>
        public string? ResolvedDestination
        {
            get { return resolvedDestination; }
            set
            {
                resolvedDestination = value;
                RaiseSavedAsChanged();
            }
        }

        /// <summary>Whether this keycut will be saved with environment variables: the setting, flipped while Shift is held.</summary>
        public bool UseEnvironmentVariables
        {
            get { return useEnvironmentVariablesSetting != isShiftHeld; }
        }

        /// <summary>
        /// The path as it will be saved, shown under the destination: with variables or without, following
        /// <see cref="UseEnvironmentVariables"/>. Empty when there is nothing to choose (not a file or folder, or no variable fits).
        /// </summary>
        public string SavedAsPath
        {
            get
            {
                if (!HasValidDestination || (destinationKind != KeycutKind.File && destinationKind != KeycutKind.Folder))
                {
                    return string.Empty;
                }

                var path = string.IsNullOrEmpty(resolvedDestination) ? destination.Trim() : resolvedDestination;
                var withVariables = Common.PathVariables.Substitute(path, PathVariables);

                if (string.Equals(withVariables, path, StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                return UseEnvironmentVariables ? withVariables : path;
            }
        }

        public bool ShowSavedAs
        {
            get { return SavedAsPath.Length > 0; }
        }

        /// <summary>The first-run panel shown above the form, or null when the app was started without one (tests, design data).</summary>
        public WelcomeViewModel? Welcome { get; init; }

        /// <summary>The "new version is out" line in the footer (#171), or null without one (tests, design data).</summary>
        public UpdateNoticeViewModel? UpdateNotice { get; init; }

        /// <summary>Changing the text invalidates the resolved kind, any destination error, and the success message.</summary>
        public string Destination
        {
            get { return destination; }
            set
            {
                if (SetProperty(ref destination, value ?? string.Empty))
                {
                    destinationKind = null;
                    resolvedDestination = null;
                    destinationError = DestinationError.None;
                    successName = null;
                    submitError = null;
                    OnPropertyChanged(nameof(DestinationKind));
                    OnPropertyChanged(nameof(DestinationError));
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>Changing the name clears its warning, its error, and the success message.</summary>
        public string ShortcutName
        {
            get { return shortcutName; }
            set
            {
                if (SetProperty(ref shortcutName, value ?? string.Empty))
                {
                    nameWarning = null;
                    isInvalidName = false;
                    successName = null;
                    submitError = null;
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

        /// <summary>What the name runs into, while its warning is showing (#153).</summary>
        public NameCheck? NameWarning
        {
            get { return nameWarning; }
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

                if (isInvalidName)
                {
                    return CreatorState.NameError;
                }

                if (nameWarning != null)
                {
                    return CreatorState.NameWarning;
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

        /// <summary>The name field shows its error look: characters Windows doesn't allow, or a keycuts command's name.</summary>
        public bool IsNameError
        {
            get { return isInvalidName; }
        }

        /// <summary>The name field shows its warning look (amber): the name would replace a keycut or clash with a command.</summary>
        public bool IsNameWarning
        {
            get { return nameWarning != null && !isInvalidName; }
        }

        public bool IsSuccess
        {
            get { return successName != null && !isSaving; }
        }

        /// <summary>Create needs a valid destination and a non-empty name, and is off while saving or showing the success message.</summary>
        public bool CanCreate
        {
            get { return !isSaving && successName == null && HasValidDestination && !string.IsNullOrWhiteSpace(shortcutName); }
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
                if (isSaving)
                {
                    return SavingHelperText;
                }

                if (HasDestinationError)
                {
                    return GetDestinationErrorMessage(destinationError);
                }

                if (isInvalidName)
                {
                    return GetInvalidNameMessage(shortcutName);
                }

                if (submitError != null)
                {
                    return submitError;
                }

                if (IsSuccess)
                {
                    return "Created " + successName;
                }

                if (nameWarning != null)
                {
                    return GetNameWarningMessage(nameWarning);
                }

                return DefaultHelperText;
            }
        }

        /// <summary>The destination field's current error for screen readers, or empty when there is none.</summary>
        public string DestinationHelpText
        {
            get { return HasDestinationError ? GetDestinationErrorMessage(destinationError) : string.Empty; }
        }

        /// <summary>The name field's current error or warning for screen readers, or empty when there is none.</summary>
        public string NameHelpText
        {
            get { return !isSaving && !HasDestinationError && (HelperIsError || HelperIsWarning) ? HelperText : string.Empty; }
        }

        /// <summary>Accessible name of the Create button; it reads "Saving" while the keycut is being written.</summary>
        public string CreateButtonName
        {
            get { return isSaving ? "Saving" : "Create"; }
        }

        public bool HelperIsError
        {
            get { return HasDestinationError || isInvalidName || submitError != null; }
        }

        /// <summary>The helper line shows a name warning (amber), and no error outranks it.</summary>
        public bool HelperIsWarning
        {
            get { return !isSaving && !HelperIsError && !IsSuccess && nameWarning != null; }
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

        /// <summary>Shows the name's warning (#153), or clears it for null or a check with nothing to warn about.</summary>
        public void SetNameWarning(NameCheck? check)
        {
            nameWarning = check != null && check.HasWarnings ? check : null;
            isInvalidName = false;
            successName = null;
            RaiseStateChanged();
        }

        /// <summary>Shows the success confirmation (the caller clears it after a moment).</summary>
        public void SetSuccess(string name)
        {
            successName = name ?? throw new ArgumentNullException(nameof(name));
            nameWarning = null;
            isInvalidName = false;
            submitError = null;
            RaiseStateChanged();
        }

        public void Clear()
        {
            destination = string.Empty;
            shortcutName = string.Empty;
            destinationKind = null;
            resolvedDestination = null;
            destinationError = DestinationError.None;
            nameWarning = null;
            isInvalidName = false;
            submitError = null;
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

        /// <summary>
        /// Shows the name's warning early, while typing (#153): a keycut it would replace, or another command with the
        /// name. A result for a name that has since changed is dropped.
        /// </summary>
        public async Task CheckNameAsync()
        {
            var name = shortcutName.Trim();
            if (name.Length == 0 || isSaving || successName != null || !KeycutCreator.IsValidName(name))
            {
                return;
            }

            var check = await RunCheckAsync(name);

            if (shortcutName.Trim() == name && !isSaving && successName == null && !isInvalidName)
            {
                SetNameWarning(check);
            }
        }

        /// <summary>
        /// Checks the name first (#153). With a warning, <see cref="ConfirmName"/> asks, and only a yes creates, replacing
        /// the existing keycut if that was the warning. A name that isn't valid goes straight to <see cref="KeycutCreator"/>,
        /// which refuses it with the right message.
        /// </summary>
        private async Task CreateAsync()
        {
            // The command's CanExecute is rechecked here; the destination text may be unresolved, so KeycutCreator validates it again
            var name = shortcutName.Trim();
            var target = destination.Trim();

            // Read Shift now: holding it while pressing Enter or clicking Create flips the setting for this keycut
            var useEnvironmentVariables = UseEnvironmentVariables;

            isInvalidName = false;
            submitError = null;
            IsSaving = true;

            CreateResult result;
            try
            {
                var replace = false;

                if (KeycutCreator.IsValidName(name))
                {
                    var check = await RunCheckAsync(name);

                    if (check.HasWarnings)
                    {
                        if (!Confirm(check, target))
                        {
                            return;
                        }

                        replace = check.Existing != null;
                    }
                    else
                    {
                        nameWarning = null;
                    }
                }

                result = await createAsync(target, name, useEnvironmentVariables, replace, CancellationToken.None);

                if (result.Outcome == CreateOutcome.DuplicateName && !replace)
                {
                    // The keycut appeared after the check (or the check failed): ask before replacing it
                    var again = await RunCheckAsync(name);
                    var existing = again.Existing ?? new Keycut { Name = name, Destination = string.Empty };

                    if (!Confirm(new NameCheck(name, existing, again.Conflicts), target))
                    {
                        return;
                    }

                    result = await createAsync(target, name, useEnvironmentVariables, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                result = CreateResult.Failure(CreateOutcome.Unexpected, ex.Message);
            }

            IsSaving = false;
            ApplyResult(result, name);
        }

        /// <summary>
        /// Shows the warning and asks. A no (or no way to ask) ends the attempt with the warning showing and the focus in
        /// the name, so the name can be changed; nothing is created.
        /// </summary>
        private bool Confirm(NameCheck check, string target)
        {
            nameWarning = check;
            IsSaving = false;

            if (ConfirmName?.Invoke(check, target) == true)
            {
                IsSaving = true;
                return true;
            }

            FocusRequested?.Invoke(this, CreatorFocusTarget.Name);
            return false;
        }

        private async Task<NameCheck> RunCheckAsync(string name)
        {
            try
            {
                return await Task.Run(() => checkName(name)) ?? NameCheck.None(name);
            }
            catch (Exception)
            {
                // A check that fails warns about nothing; creating still refuses to replace a keycut without asking
                return NameCheck.None(name);
            }
        }

        private void ApplyResult(CreateResult result, string name)
        {
            switch (result.Outcome)
            {
                case CreateOutcome.Success:
                    SetSuccess(name);
                    NotifyKeycutCreated();
                    _ = ResetAfterSuccessAsync();
                    return;
                case CreateOutcome.DuplicateName:
                    // Only after a confirmed replace that still failed, which shouldn't happen: say so and keep the name
                    ShowSubmitError(UnexpectedErrorMessage);
                    return;
                case CreateOutcome.InvalidName:
                    isInvalidName = true;
                    RaiseStateChanged();
                    FocusRequested?.Invoke(this, CreatorFocusTarget.NameSelectAll);
                    return;
                case CreateOutcome.DestinationInvalid:
                    var error = result.DestinationError == DestinationError.None || result.DestinationError == DestinationError.Empty
                        ? DestinationError.NotFound
                        : result.DestinationError;
                    DestinationError = error;
                    FocusRequested?.Invoke(this, CreatorFocusTarget.Destination);
                    return;
                case CreateOutcome.OutputFolderError:
                    ShowSubmitError(OutputFolderErrorMessage);
                    return;
                case CreateOutcome.PathUpdateError:
                    ShowSubmitError(PathUpdateErrorMessage);
                    return;
                default:
                    ShowSubmitError(UnexpectedErrorMessage);
                    return;
            }
        }

        private void ShowSubmitError(string message)
        {
            submitError = message;
            RaiseStateChanged();
            FocusRequested?.Invoke(this, CreatorFocusTarget.Name);
        }

        /// <summary>Shows the success state for <see cref="SuccessDuration"/>, then resets to Empty unless the user started typing.</summary>
        private async Task ResetAfterSuccessAsync()
        {
            var version = ++successVersion;
            await Task.Delay(SuccessDuration);

            if (version == successVersion && successName != null && !isSaving)
            {
                Clear();
                FocusRequested?.Invoke(this, CreatorFocusTarget.Destination);
            }
        }

        private async Task<CreateResult> DefaultCreateAsync(string destination, string name, bool useEnvironmentVariables, bool replace, CancellationToken ct)
        {
#if DEBUG
            // Debug builds can slow creation down to see the Saving state
            if (int.TryParse(Environment.GetEnvironmentVariable("KEYCUTS_CREATE_DELAY_MS"), out var delayMs) && delayMs > 0)
            {
                await Task.Delay(delayMs, ct);
            }
#endif
            // The variables the preview used, so the keycut is saved as shown; null (not read yet) makes the create read them
            // Overwrite only after a confirmed replace; otherwise the setting decides (null): with "Ask before replacing a
            // keycut" off a keycut is replaced without asking, and with it on one that appeared since the check is refused
            var options = new CreateOptions
            {
                UseEnvironmentVariables = useEnvironmentVariables,
                PathVariables = pathVariables,
                Overwrite = replace ? true : null
            };
            return await KeycutCreator.CreateAsync(destination, name, options, ct);
        }

        private static NameCheck DefaultCheckName(string name)
        {
            // With "Ask before replacing a keycut" off, an existing keycut is replaced without asking, so it isn't a warning
            var runner = new Runner();
            return NameCheck.Run(name, runner.GetOutputFolder(), runner.GetAskBeforeReplacing(), NameConflictEnvironment.GetDefault());
        }

        private void RaiseStateChanged()
        {
            CreateCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(HasDestination));
            OnPropertyChanged(nameof(HasDestinationError));
            OnPropertyChanged(nameof(HasValidDestination));
            OnPropertyChanged(nameof(IsNameError));
            OnPropertyChanged(nameof(IsNameWarning));
            OnPropertyChanged(nameof(NameWarning));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(SuccessName));
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(IsInputEnabled));
            OnPropertyChanged(nameof(ShowDragHover));
            OnPropertyChanged(nameof(ShowDestinationError));
            OnPropertyChanged(nameof(DestinationIcon));
            OnPropertyChanged(nameof(HelperText));
            OnPropertyChanged(nameof(HelperIsError));
            OnPropertyChanged(nameof(HelperIsWarning));
            OnPropertyChanged(nameof(DestinationHelpText));
            OnPropertyChanged(nameof(NameHelpText));
            OnPropertyChanged(nameof(CreateButtonName));
            RaiseSavedAsChanged();
        }

        private void RaiseSavedAsChanged()
        {
            OnPropertyChanged(nameof(UseEnvironmentVariables));
            OnPropertyChanged(nameof(SavedAsPath));
            OnPropertyChanged(nameof(ShowSavedAs));
        }
    }
}
