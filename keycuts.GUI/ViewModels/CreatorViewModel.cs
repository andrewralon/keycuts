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
        TargetSelected,
        Saving,
        /// <summary>The name is invalid (characters Windows doesn't allow, or a keycuts command's name).</summary>
        NameError,
        /// <summary>The name would replace a keycut or clash with another command (#153); Create asks first.</summary>
        NameWarning,
        InvalidTarget,
        Success
    }

    /// <summary>Where the view should move keyboard focus after a create attempt.</summary>
    public enum CreatorFocusTarget
    {
        Target,
        Name,
        NameSelectAll,
        Description
    }

    /// <summary>
    /// State of the compact creator. The input handling (KC-10) and submit flow (KC-11) set the flags and errors;
    /// the view binds to the derived properties.
    /// </summary>
    public class CreatorViewModel : ObservableObject
    {
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

        /// <summary>An existing keycut whose target couldn't be read.</summary>
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
                lines.Add(string.IsNullOrEmpty(check.Existing.Target)
                    ? string.Format(ReplaceUnknownMessage, check.Name)
                    : string.Format(ReplaceMessage, check.Name, check.Existing.Target));
            }

            if (check.MainConflict is NameConflict conflict)
            {
                lines.Add(GetConflictMessage(check.Name, conflict));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string target = string.Empty;

        private string shortcutName = string.Empty;

        private string description = string.Empty;

        private bool isDescriptionVisible;

        private readonly IDescriptionVisibilityStore? descriptionStore;

        private int keycutCount;

        private KeycutKind? targetKind;

        private TargetError targetError;

        private bool isDragHover;

        private bool isSaving;

        // What the typed name runs into (#153), while the warning is showing; null when there's nothing to warn about
        private NameCheck? nameWarning;

        private string? successName;

        private bool isInvalidName;

        private string? submitError;

        private int successVersion;

        private readonly Func<string, string, string, bool, bool, CancellationToken, Task<CreateResult>> createAsync;

        private readonly Func<string, NameCheck> checkName;

        private bool useEnvironmentVariablesSetting;

        private bool isShiftHeld;

        // True while the target box has the keyboard focus: then it always holds the real path, so typing never edits the %VAR% form
        private bool isTargetEditing;

        // Null until the app has read them (in the background, since a custom folder on a network share can be slow)
        private IReadOnlyList<PathVariable>? pathVariables;

        // What the typed target resolved to (a shortcut's target, a normalized path), while the text keeps what was typed
        private string? resolvedTarget;

        /// <param name="createAsync">Creates the keycut (target, name, description, use environment variables, replace an
        /// existing one); defaults to <see cref="KeycutCreator"/>. Replace is true only after the user confirmed it. Tests pass a fake.</param>
        /// <param name="checkName">What a name runs into (#153), checked while typing and before each create; defaults to
        /// <see cref="NameCheck.Run"/> on this machine. Tests pass a fake.</param>
        /// <param name="descriptionStore">Remembers whether the description box is shown (#205): read once here, written each
        /// time the user shows or hides it. Without one (tests, design data) the box starts hidden and nothing is remembered.</param>
        public CreatorViewModel(
            ICommand showManagerCommand,
            ICommand openSettingsCommand,
            Func<string, string, string, bool, bool, CancellationToken, Task<CreateResult>>? createAsync = null,
            Func<string, NameCheck>? checkName = null,
            IDescriptionVisibilityStore? descriptionStore = null)
        {
            ShowManagerCommand = showManagerCommand;
            OpenSettingsCommand = openSettingsCommand;
            this.createAsync = createAsync ?? DefaultCreateAsync;
            this.checkName = checkName ?? DefaultCheckName;
            this.descriptionStore = descriptionStore;
            isDescriptionVisible = descriptionStore?.Load() ?? false;
            CreateCommand = new AsyncRelayCommand(CreateAsync, () => CanCreate);
            ToggleDescriptionCommand = new RelayCommand(ToggleDescription, () => IsInputEnabled);
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
        /// Asks the user to go ahead despite a name warning (#153): the check and the new target in, true to create.
        /// The view shows the confirmation dialog; tests pass a fake. Without one nothing with a warning is created.
        /// </summary>
        public Func<NameCheck, string, bool>? ConfirmName { get; set; }

        /// <summary>How long "Created" shows before it goes away and Create works again. The fields keep their text.</summary>
        public TimeSpan SuccessDuration { get; set; } = TimeSpan.FromSeconds(2);

        public AsyncRelayCommand CreateCommand { get; }

        /// <summary>
        /// The description label: "Add a description (optional)" shows the field and moves the focus into it, "Description (optional)"
        /// hides it again. Either way the choice is remembered.
        /// </summary>
        public ICommand ToggleDescriptionCommand { get; }

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
                    RaiseTargetDisplayChanged();
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
                    RaiseTargetDisplayChanged();
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
                RaiseTargetDisplayChanged();
            }
        }

        /// <summary>
        /// What the target resolved to while being typed (a shortcut's target, a normalized path), which is what is
        /// saved; the text itself isn't rewritten until it's pasted, dropped or left. Set it after <see cref="Target"/>, which clears it.
        /// </summary>
        public string? ResolvedTarget
        {
            get { return resolvedTarget; }
            set
            {
                resolvedTarget = value;
                RaiseTargetDisplayChanged();
            }
        }

        /// <summary>Whether this keycut will be saved with environment variables: the setting, flipped while Shift is held.</summary>
        public bool UseEnvironmentVariables
        {
            get { return useEnvironmentVariablesSetting != isShiftHeld; }
        }

        /// <summary>
        /// The path with environment variables in it, which is how a file or folder keycut will be saved while
        /// <see cref="UseEnvironmentVariables"/> is on. Empty when it doesn't apply: the setting is off, the target isn't a
        /// valid file or folder, or no variable fits it.
        /// </summary>
        public string TargetWithVariables
        {
            get
            {
                if (!UseEnvironmentVariables || !HasValidTarget || (targetKind != KeycutKind.File && targetKind != KeycutKind.Folder))
                {
                    return string.Empty;
                }

                var path = string.IsNullOrEmpty(resolvedTarget) ? target.Trim() : resolvedTarget;
                var withVariables = Common.PathVariables.Substitute(path, PathVariables);

                return string.Equals(withVariables, path, StringComparison.Ordinal) ? string.Empty : withVariables;
            }
        }

        /// <summary>
        /// True while the target box has the keyboard focus. The view sets it. Then the box shows the real path (what is typed
        /// or pasted), so Shift, which flips the variables, never rewrites text that is being edited.
        /// </summary>
        public bool IsTargetEditing
        {
            get { return isTargetEditing; }
            set
            {
                if (SetProperty(ref isTargetEditing, value))
                {
                    OnPropertyChanged(nameof(TargetText));
                }
            }
        }

        /// <summary>
        /// What the target box shows (#205): the %VAR% form of the path while it isn't being edited and
        /// <see cref="TargetWithVariables"/> has one (Shift held, or the Settings choice), otherwise <see cref="Target"/>.
        /// Typing sets <see cref="Target"/>; the form is only ever displayed, never stored in it.
        /// </summary>
        public string TargetText
        {
            get
            {
                var withVariables = isTargetEditing ? string.Empty : TargetWithVariables;
                return withVariables.Length > 0 ? withVariables : target;
            }
            set { Target = value; }
        }

        /// <summary>The first-run panel shown above the form, or null when the app was started without one (tests, design data).</summary>
        public WelcomeViewModel? Welcome { get; init; }

        /// <summary>The "new version is out" line in the footer (#171), or null without one (tests, design data).</summary>
        public UpdateNoticeViewModel? UpdateNotice { get; init; }

        /// <summary>Changing the text invalidates the resolved kind, any target error, and the success message.</summary>
        public string Target
        {
            get { return target; }
            set
            {
                if (SetProperty(ref target, value ?? string.Empty))
                {
                    targetKind = null;
                    resolvedTarget = null;
                    targetError = TargetError.None;
                    successName = null;
                    submitError = null;
                    OnPropertyChanged(nameof(TargetKind));
                    OnPropertyChanged(nameof(TargetError));
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

        /// <summary>
        /// The optional note stored in the keycut (#162). Text is never hidden: setting one shows the field. Changing it
        /// clears the success message and any submit error, as typing in the other fields does.
        /// </summary>
        public string Description
        {
            get { return description; }
            set
            {
                if (SetProperty(ref description, value ?? string.Empty))
                {
                    successName = null;
                    submitError = null;

                    if (description.Length > 0 && !isDescriptionVisible)
                    {
                        // Text is never hidden. Not remembered: only the user's own show and hide is
                        isDescriptionVisible = true;
                        RaiseDescriptionVisibilityChanged();
                    }

                    RaiseStateChanged();
                }
            }
        }

        /// <summary>
        /// The description field is shown: the last choice (remembered between launches), or while it holds text.
        /// Hidden by default. A hidden description is not saved, whatever the field still holds.
        /// </summary>
        public bool IsDescriptionVisible
        {
            get { return isDescriptionVisible; }
        }

        /// <summary>The label says "Add a description (optional)" because the field is hidden.</summary>
        public bool ShowAddDescription
        {
            get { return !isDescriptionVisible; }
        }

        /// <summary>The label above the description field, which is also the button that hides it.</summary>
        public string DescriptionToggleText
        {
            get { return isDescriptionVisible ? "Description (optional)" : "Add a description (optional)"; }
        }

        /// <summary>What a screen reader calls the label button: what pressing it does.</summary>
        public string DescriptionToggleName
        {
            get { return isDescriptionVisible ? "Hide description" : "Add a description (optional)"; }
        }

        private void ToggleDescription()
        {
            SetDescriptionVisible(!isDescriptionVisible);

            if (isDescriptionVisible)
            {
                FocusRequested?.Invoke(this, CreatorFocusTarget.Description);
            }
        }

        // The user's choice: remembered, unlike the field showing itself because it holds text
        private void SetDescriptionVisible(bool visible)
        {
            if (isDescriptionVisible == visible)
            {
                return;
            }

            isDescriptionVisible = visible;
            RaiseDescriptionVisibilityChanged();
            descriptionStore?.Save(visible);
        }

        private void RaiseDescriptionVisibilityChanged()
        {
            OnPropertyChanged(nameof(IsDescriptionVisible));
            OnPropertyChanged(nameof(ShowAddDescription));
            OnPropertyChanged(nameof(DescriptionToggleText));
            OnPropertyChanged(nameof(DescriptionToggleName));
        }

        public int KeycutCount
        {
            get { return keycutCount; }
            set
            {
                if (SetProperty(ref keycutCount, value))
                {
                    OnPropertyChanged(nameof(CountDescription));
                }
            }
        }

        /// <summary>What a screen reader and the tooltip say for the footer's count pill, which shows just the number.</summary>
        public string CountDescription
        {
            get { return keycutCount + (keycutCount == 1 ? " keycut" : " keycuts"); }
        }

        /// <summary>The kind the target resolved to. Set it after <see cref="Target"/>, which clears it.</summary>
        public KeycutKind? TargetKind
        {
            get { return targetKind; }
            set
            {
                if (SetProperty(ref targetKind, value))
                {
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>The reason the target was rejected. Set it after <see cref="Target"/>, which clears it.</summary>
        public TargetError TargetError
        {
            get { return targetError; }
            set
            {
                if (SetProperty(ref targetError, value))
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

                if (HasTargetError)
                {
                    return CreatorState.InvalidTarget;
                }

                if (isInvalidName)
                {
                    return CreatorState.NameError;
                }

                if (nameWarning != null)
                {
                    return CreatorState.NameWarning;
                }

                return HasTarget ? CreatorState.TargetSelected : CreatorState.Empty;
            }
        }

        public bool HasTarget
        {
            get { return !string.IsNullOrWhiteSpace(target); }
        }

        public bool HasTargetError
        {
            get { return targetError != TargetError.None && targetError != TargetError.Empty; }
        }

        /// <summary>A target that has text and was not rejected. KC-10 refines this by resolving the text.</summary>
        public bool HasValidTarget
        {
            get { return HasTarget && !HasTargetError; }
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

        /// <summary>Create needs a valid target and a non-empty name, and is off while saving or showing the success message.</summary>
        public bool CanCreate
        {
            get { return !isSaving && successName == null && HasValidTarget && !string.IsNullOrWhiteSpace(shortcutName); }
        }

        /// <summary>The target field, Browse, and the name field are off while saving; the drop area ignores drops too.</summary>
        public bool IsInputEnabled
        {
            get { return !isSaving; }
        }

        /// <summary>The drag-hover look is hidden while saving, since drops are ignored.</summary>
        public bool ShowDragHover
        {
            get { return isDragHover && !isSaving; }
        }

        /// <summary>Only target errors turn the drop area red; name errors style just the name field.</summary>
        public bool ShowTargetError
        {
            get { return HasTargetError; }
        }

        public IconKind TargetIcon
        {
            get { return GetTargetIcon(targetKind); }
        }

        public string HelperText
        {
            get
            {
                if (isSaving)
                {
                    return SavingHelperText;
                }

                if (HasTargetError)
                {
                    return GetTargetErrorMessage(targetError);
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

                return string.Empty;
            }
        }

        /// <summary>
        /// "Saving…" or "Created name", shown at the right of the Name label (#205), so the window keeps its height while a
        /// keycut is made. Empty when the helper line says something else (an error outranks the success message).
        /// </summary>
        public string NameStatusText
        {
            get
            {
                if (isSaving)
                {
                    return SavingHelperText;
                }

                return IsSuccess && !HelperIsError ? "Created " + successName : string.Empty;
            }
        }

        public bool HasNameStatus
        {
            get { return NameStatusText.Length > 0; }
        }

        /// <summary>
        /// The line under the name field has a warning or an error to show; otherwise it takes no room. Saving and Created are
        /// on the Name label's row instead (<see cref="NameStatusText"/>).
        /// </summary>
        public bool ShowHelperLine
        {
            get { return HelperText.Length > 0 && !HasNameStatus; }
        }

        /// <summary>The target field's current error for screen readers, or empty when there is none.</summary>
        public string TargetHelpText
        {
            get { return HasTargetError ? GetTargetErrorMessage(targetError) : string.Empty; }
        }

        /// <summary>The name field's current error or warning for screen readers, or empty when there is none.</summary>
        public string NameHelpText
        {
            get { return !isSaving && !HasTargetError && (HelperIsError || HelperIsWarning) ? HelperText : string.Empty; }
        }

        /// <summary>Accessible name of the Create button; it reads "Saving" while the keycut is being written.</summary>
        public string CreateButtonName
        {
            get { return isSaving ? "Saving" : "Create"; }
        }

        public bool HelperIsError
        {
            get { return HasTargetError || isInvalidName || submitError != null; }
        }

        /// <summary>The helper line shows a name warning (amber), and no error outranks it.</summary>
        public bool HelperIsWarning
        {
            get { return !isSaving && !HelperIsError && !IsSuccess && nameWarning != null; }
        }

        /// <summary>The target field is focused unless one is already set, in which case the name field is.</summary>
        public bool FocusNameFieldOnShow
        {
            get { return !string.IsNullOrWhiteSpace(target); }
        }

        public static IconKind GetTargetIcon(KeycutKind? kind)
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

        public static string GetTargetErrorMessage(TargetError error)
        {
            switch (error)
            {
                case TargetError.NotFound:
                    return "That target wasn\u2019t found.";
                case TargetError.AccessDenied:
                    return "Access to that target was denied.";
                case TargetError.Unsupported:
                    return "That target type isn\u2019t supported.";
                case TargetError.Timeout:
                    return "That target didn\u2019t respond in time.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Applies a resolver result. A valid result replaces the text with the normalized target and sets the kind.
        /// An invalid result is shown as an error only when <paramref name="showError"/> is true (not while the user is typing).
        /// </summary>
        public void ApplyResolved(TargetResult result, bool showError)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.IsValid)
            {
                Target = result.Target;
                TargetKind = result.Kind;
                TargetError = TargetError.None;
            }
            else if (showError && result.Error != TargetError.Empty)
            {
                TargetError = result.Error;
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
            // The command's CanExecute is rechecked here; the target text may be unresolved, so KeycutCreator validates it again
            var name = shortcutName.Trim();
            var trimmedTarget = target.Trim();
            // A hidden box is out of sight, so what it still holds isn't saved
            var trimmedDescription = isDescriptionVisible ? description.Trim() : string.Empty;

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
                        if (!Confirm(check, trimmedTarget))
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

                result = await createAsync(trimmedTarget, name, trimmedDescription, useEnvironmentVariables, replace, CancellationToken.None);

                if (result.Outcome == CreateOutcome.DuplicateName && !replace)
                {
                    // The keycut appeared after the check (or the check failed): ask before replacing it
                    var again = await RunCheckAsync(name);
                    var existing = again.Existing ?? new Keycut { Name = name, Target = string.Empty };

                    if (!Confirm(new NameCheck(name, existing, again.Conflicts), trimmedTarget))
                    {
                        return;
                    }

                    result = await createAsync(trimmedTarget, name, trimmedDescription, useEnvironmentVariables, true, CancellationToken.None);
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
                    _ = HideSuccessAfterDelayAsync();
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
                case CreateOutcome.TargetInvalid:
                    var error = result.TargetError == TargetError.None || result.TargetError == TargetError.Empty
                        ? TargetError.NotFound
                        : result.TargetError;
                    TargetError = error;
                    FocusRequested?.Invoke(this, CreatorFocusTarget.Target);
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

        /// <summary>
        /// Shows "Created" for <see cref="SuccessDuration"/>, then takes it away (unless the user started typing, which did already)
        /// so Create works again. The target, name and description are left as they were (#205), so a keycut made a little
        /// differently, or the same one replaced, is a small edit away.
        /// </summary>
        private async Task HideSuccessAfterDelayAsync()
        {
            var version = ++successVersion;
            await Task.Delay(SuccessDuration);

            if (version == successVersion && successName != null && !isSaving)
            {
                successName = null;
                RaiseStateChanged();
            }
        }

        private async Task<CreateResult> DefaultCreateAsync(string target, string name, string description, bool useEnvironmentVariables, bool replace, CancellationToken ct)
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
                Description = description,
                UseEnvironmentVariables = useEnvironmentVariables,
                PathVariables = pathVariables,
                Overwrite = replace ? true : null
            };
            return await KeycutCreator.CreateAsync(target, name, options, ct);
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
            OnPropertyChanged(nameof(HasTarget));
            OnPropertyChanged(nameof(HasTargetError));
            OnPropertyChanged(nameof(HasValidTarget));
            OnPropertyChanged(nameof(IsNameError));
            OnPropertyChanged(nameof(IsNameWarning));
            OnPropertyChanged(nameof(NameWarning));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(SuccessName));
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(IsInputEnabled));
            OnPropertyChanged(nameof(ShowDragHover));
            OnPropertyChanged(nameof(ShowTargetError));
            OnPropertyChanged(nameof(TargetIcon));
            OnPropertyChanged(nameof(HelperText));
            OnPropertyChanged(nameof(NameStatusText));
            OnPropertyChanged(nameof(HasNameStatus));
            OnPropertyChanged(nameof(ShowHelperLine));
            OnPropertyChanged(nameof(HelperIsError));
            OnPropertyChanged(nameof(HelperIsWarning));
            OnPropertyChanged(nameof(TargetHelpText));
            OnPropertyChanged(nameof(NameHelpText));
            OnPropertyChanged(nameof(CreateButtonName));
            RaiseTargetDisplayChanged();
        }

        private void RaiseTargetDisplayChanged()
        {
            OnPropertyChanged(nameof(UseEnvironmentVariables));
            OnPropertyChanged(nameof(TargetWithVariables));
            OnPropertyChanged(nameof(TargetText));
        }
    }
}
