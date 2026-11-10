using System;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The first-run panel at the top of the creator (#61, #11): explains the Win+R workflow and offers a keycut to
    /// keycuts itself. It shows on the first run (until it's dismissed or used, upgrades included), whenever the keycuts
    /// folder is empty, and on demand from Settings (<see cref="Show"/>).
    /// </summary>
    public class WelcomeViewModel : ObservableObject
    {
        /// <summary>
        /// Not "keycuts": the installer puts the CLI, keycuts.exe, on PATH too, and depending on PATH order a keycuts.bat
        /// would either shadow the CLI (dropping its arguments) or be shadowed by it (Win+R opening a console).
        /// </summary>
        public const string DefaultName = "kc";

        private readonly string appPath;

        private readonly Func<bool> isDismissed;

        private readonly Action markDismissed;

        private readonly Func<string, string, CancellationToken, Task<CreateResult>> createAsync;

        private readonly Func<string?, bool> isOnPath;

        private readonly Func<string?, bool> addToPath;

        // Where the self-keycut was written, so the PATH check and Add to PATH use that folder even if Settings changes it
        private string? createdFolder;

        private bool decided;

        private bool needsPath;

        private bool isVisible;

        private string name = DefaultName;

        private bool isSaving;

        private string? createdName;

        private string? error;

        /// <param name="appPath">The keycuts.GUI.exe the self-keycut opens.</param>
        /// <param name="isDismissed">Reads whether the panel was dismissed before (HKCU in production).</param>
        /// <param name="markDismissed">Records that it was dismissed. A failure is ignored: the panel just shows again next time.</param>
        /// <param name="createAsync">Creates a keycut (destination, name), the same call the creator uses.</param>
        /// <param name="isOnPath">Whether Win+R can find keycuts in the given folder (it's on the user or machine PATH). The
        /// folder is where the self-keycut was written, or null if the create result didn't say (then use the output folder).
        /// Null treats it as on PATH: tests, and the Debug folder override, which must never write PATH.</param>
        /// <param name="addToPath">Adds the given folder (as for <paramref name="isOnPath"/>) to the user PATH, as the installer and Settings do. False on failure.</param>
        public WelcomeViewModel(
            string appPath,
            Func<bool> isDismissed,
            Action markDismissed,
            Func<string, string, CancellationToken, Task<CreateResult>> createAsync,
            Func<string?, bool>? isOnPath = null,
            Func<string?, bool>? addToPath = null)
        {
            this.appPath = appPath ?? throw new ArgumentNullException(nameof(appPath));
            this.isDismissed = isDismissed ?? throw new ArgumentNullException(nameof(isDismissed));
            this.markDismissed = markDismissed ?? throw new ArgumentNullException(nameof(markDismissed));
            this.createAsync = createAsync ?? throw new ArgumentNullException(nameof(createAsync));
            this.isOnPath = isOnPath ?? (folder => true);
            this.addToPath = addToPath ?? (folder => true);

            CreateCommand = new AsyncRelayCommand(CreateAsync, () => CanCreate);
            DismissCommand = new RelayCommand(Dismiss);
            AddToPathCommand = new RelayCommand(AddToPath, () => needsPath);
        }

        /// <summary>Raised after the self-keycut was created so the shell can refresh the count.</summary>
        public event EventHandler? KeycutCreated;

        public AsyncRelayCommand CreateCommand { get; }

        public RelayCommand DismissCommand { get; }

        public RelayCommand AddToPathCommand { get; }

        /// <summary>The self-keycut exists, but its folder isn't on PATH yet, so Win+R can't find it (a portable copy).</summary>
        public bool NeedsPath
        {
            get { return needsPath; }
        }

        public bool IsVisible
        {
            get { return isVisible; }
            private set { SetProperty(ref isVisible, value); }
        }

        /// <summary>The self-keycut's name, pre-filled with <see cref="DefaultName"/>. Editing it clears a name error.</summary>
        public string Name
        {
            get { return name; }
            set
            {
                if (SetProperty(ref name, value ?? string.Empty))
                {
                    error = null;
                    RaiseStateChanged();
                }
            }
        }

        public bool IsSaving
        {
            get { return isSaving; }
            private set
            {
                if (SetProperty(ref isSaving, value))
                {
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>The self-keycut exists and Win+R can find it; the panel then only shows how to use it.</summary>
        public bool IsSuccess
        {
            get { return createdName != null && !needsPath && error == null; }
        }

        /// <summary>A name or create error, a PATH update failure, or the PATH warning (which needs action, so it reads as one).</summary>
        public bool HelperIsError
        {
            get { return error != null || needsPath; }
        }

        /// <summary>The name box and Create are off while saving and after the keycut exists.</summary>
        public bool IsInputEnabled
        {
            get { return !isSaving && createdName == null; }
        }

        public bool CanCreate
        {
            get { return IsInputEnabled && !string.IsNullOrWhiteSpace(name); }
        }

        public string HelperText
        {
            get
            {
                if (isSaving)
                {
                    return CreatorViewModel.SavingHelperText;
                }

                if (error != null)
                {
                    return error;
                }

                if (needsPath)
                {
                    return string.Format("Created {0}, but Win+R can’t find it yet: the keycuts folder isn’t on your PATH.", createdName);
                }

                if (createdName != null)
                {
                    return string.Format("Created {0}. Press Win+R, type {0}, and press Enter to open keycuts.", createdName);
                }

                return "Then Win+R and this name opens keycuts.";
            }
        }

        /// <summary>
        /// Decides once, from the first keycut count, whether the panel shows: on the first run (never dismissed or used)
        /// or for an empty folder. Later counts (after a create, or from the manager) don't change it.
        /// </summary>
        public void OnKeycutCountLoaded(int count)
        {
            if (decided)
            {
                return;
            }

            decided = true;
            IsVisible = count == 0 || !ReadDismissed();
        }

        /// <summary>Shows the panel again from the start (Settings → Show welcome), whatever was decided or created before.</summary>
        public void Show()
        {
            decided = true;
            createdName = null;
            createdFolder = null;
            needsPath = false;
            error = null;
            Name = DefaultName;
            IsVisible = true;
            RaiseStateChanged();
        }

        public void Dismiss()
        {
            IsVisible = false;
            TryMarkDismissed();
        }

        private async Task CreateAsync()
        {
            var target = name.Trim();
            error = null;
            IsSaving = true;

            CreateResult result;
            try
            {
                result = await createAsync(appPath, target, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = CreateResult.Failure(CreateOutcome.Unexpected, ex.Message);
            }

            switch (result.Outcome)
            {
                case CreateOutcome.Success:
                    createdName = target;
                    createdFolder = string.IsNullOrEmpty(result.Keycut?.FilePath) ? null : Path.GetDirectoryName(result.Keycut.FilePath);
                    needsPath = !isOnPath(createdFolder);
                    TryMarkDismissed();
                    break;
                case CreateOutcome.DuplicateName:
                    error = string.Format("“{0}” already exists. Choose another name.", target);
                    break;
                case CreateOutcome.InvalidName:
                    error = CreatorViewModel.InvalidNameMessage;
                    break;
                case CreateOutcome.OutputFolderError:
                    error = CreatorViewModel.OutputFolderErrorMessage;
                    break;
                case CreateOutcome.PathUpdateError:
                    error = CreatorViewModel.PathUpdateErrorMessage;
                    break;
                default:
                    error = CreatorViewModel.UnexpectedErrorMessage;
                    break;
            }

            IsSaving = false;
            RaiseStateChanged();

            if (createdName != null)
            {
                KeycutCreated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Puts the output folder on the user PATH, then checks again; a failure keeps the button and says so.</summary>
        private void AddToPath()
        {
            if (addToPath(createdFolder) && isOnPath(createdFolder))
            {
                needsPath = false;
                error = null;
            }
            else
            {
                error = CreatorViewModel.PathUpdateErrorMessage;
            }

            RaiseStateChanged();
        }

        private bool ReadDismissed()
        {
            try
            {
                return isDismissed();
            }
            catch (Exception e) when (IsSettingsFailure(e))
            {
                return false;
            }
        }

        private void TryMarkDismissed()
        {
            try
            {
                markDismissed();
            }
            catch (Exception e) when (IsSettingsFailure(e))
            {
                // Not worth an error: the panel shows again next launch if the folder is still empty
            }
        }

        private static bool IsSettingsFailure(Exception e)
        {
            return e is UnauthorizedAccessException || e is SecurityException || e is IOException;
        }

        private void RaiseStateChanged()
        {
            CreateCommand.RaiseCanExecuteChanged();
            AddToPathCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NeedsPath));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(HelperIsError));
            OnPropertyChanged(nameof(IsInputEnabled));
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(HelperText));
        }
    }
}
