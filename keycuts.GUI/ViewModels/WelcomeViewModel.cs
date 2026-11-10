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
    /// keycuts itself. It shows only when the keycuts folder is empty and the panel was never dismissed, so people who
    /// upgrade with keycuts already in place don't see it.
    /// </summary>
    public class WelcomeViewModel : ObservableObject
    {
        public const string DefaultName = "keycuts";

        private readonly string appPath;

        private readonly Func<bool> isDismissed;

        private readonly Action markDismissed;

        private readonly Func<string, string, CancellationToken, Task<CreateResult>> createAsync;

        private bool decided;

        private bool isVisible;

        private string name = DefaultName;

        private bool isSaving;

        private string? createdName;

        private string? error;

        /// <param name="appPath">The keycuts.GUI.exe the self-keycut opens.</param>
        /// <param name="isDismissed">Reads whether the panel was dismissed before (HKCU in production).</param>
        /// <param name="markDismissed">Records that it was dismissed. A failure is ignored: the panel just shows again next time.</param>
        /// <param name="createAsync">Creates a keycut (destination, name), the same call the creator uses.</param>
        public WelcomeViewModel(
            string appPath,
            Func<bool> isDismissed,
            Action markDismissed,
            Func<string, string, CancellationToken, Task<CreateResult>> createAsync)
        {
            this.appPath = appPath ?? throw new ArgumentNullException(nameof(appPath));
            this.isDismissed = isDismissed ?? throw new ArgumentNullException(nameof(isDismissed));
            this.markDismissed = markDismissed ?? throw new ArgumentNullException(nameof(markDismissed));
            this.createAsync = createAsync ?? throw new ArgumentNullException(nameof(createAsync));

            CreateCommand = new AsyncRelayCommand(CreateAsync, () => CanCreate);
            DismissCommand = new RelayCommand(Dismiss);
        }

        /// <summary>Raised after the self-keycut was created so the shell can refresh the count.</summary>
        public event EventHandler? KeycutCreated;

        public AsyncRelayCommand CreateCommand { get; }

        public RelayCommand DismissCommand { get; }

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

        /// <summary>The self-keycut exists; the panel then only shows how to use it.</summary>
        public bool IsSuccess
        {
            get { return createdName != null; }
        }

        public bool HelperIsError
        {
            get { return error != null; }
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

                if (createdName != null)
                {
                    return string.Format("Created {0}. Press Win+R, type {0}, and press Enter to open keycuts.", createdName);
                }

                return "Then Win+R and this name opens keycuts.";
            }
        }

        /// <summary>
        /// Decides once, from the first keycut count, whether the panel shows: only for an empty folder and a panel that
        /// was never dismissed. Later counts (after a create, or from the manager) don't change it.
        /// </summary>
        public void OnKeycutCountLoaded(int count)
        {
            if (decided)
            {
                return;
            }

            decided = true;
            IsVisible = count == 0 && !ReadDismissed();
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
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(HelperIsError));
            OnPropertyChanged(nameof(IsInputEnabled));
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(HelperText));
        }
    }
}
