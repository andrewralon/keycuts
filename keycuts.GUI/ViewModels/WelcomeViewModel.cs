using System;
using System.IO;
using System.Security;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The first-run panel at the top of the creator (#61, #11, #213): says what keycuts does and suggests a first keycut,
    /// <c>np</c> for <c>notepad.exe</c>, which it fills into the creator's own fields rather than offering a form of its
    /// own. It shows on the first run (until it's dismissed or used, upgrades included), whenever the keycuts folder is
    /// empty, and on demand from Settings (<see cref="Show"/>).
    /// </summary>
    public class WelcomeViewModel : ObservableObject
    {
        /// <summary>The name of the suggested first keycut.</summary>
        public const string ExampleName = "np";

        /// <summary>The program the suggested keycut opens, as the panel says it.</summary>
        public const string ExampleProgram = "notepad.exe";

        /// <summary>
        /// What the Target field gets. A bare program name isn't a target the creator accepts, so this is the full path of
        /// Notepad in the Windows folder, which every Windows has.
        /// </summary>
        public static string ExampleTarget
        {
            get { return Path.Combine(Environment.SystemDirectory, ExampleProgram); }
        }

        private readonly Func<bool> isDismissed;

        private readonly Action markDismissed;

        private readonly Func<bool> isOnPath;

        private readonly Func<bool> addToPath;

        private bool decided;

        private bool needsPath;

        private bool isVisible;

        private string? createdName;

        private string? error;

        /// <param name="isDismissed">Reads whether the panel was dismissed before (HKCU in production).</param>
        /// <param name="markDismissed">Records that it was dismissed. A failure is ignored: the panel just shows again next time.</param>
        /// <param name="isOnPath">Whether Win+R can find keycuts in the output folder (it's on the user or machine PATH). Null
        /// treats it as on PATH: tests, and the Debug folder override, which must never write PATH.</param>
        /// <param name="addToPath">Adds the output folder to the user PATH, as the installer and Settings do. False on failure.</param>
        public WelcomeViewModel(
            Func<bool> isDismissed,
            Action markDismissed,
            Func<bool>? isOnPath = null,
            Func<bool>? addToPath = null)
        {
            this.isDismissed = isDismissed ?? throw new ArgumentNullException(nameof(isDismissed));
            this.markDismissed = markDismissed ?? throw new ArgumentNullException(nameof(markDismissed));
            this.isOnPath = isOnPath ?? (() => true);
            this.addToPath = addToPath ?? (() => true);

            DismissCommand = new RelayCommand(Dismiss);
            AddToPathCommand = new RelayCommand(AddToPath, () => needsPath);
        }

        /// <summary>Raised when the panel starts showing, so the creator can fill in the example.</summary>
        public event EventHandler? Shown;

        /// <summary>Raised after Got it hid the panel, so the creator can clear the fields the example filled in.</summary>
        public event EventHandler? Dismissed;

        public RelayCommand DismissCommand { get; }

        public RelayCommand AddToPathCommand { get; }

        /// <summary>A keycut was created, but its folder isn't on PATH yet, so Win+R can't find it (a portable copy).</summary>
        public bool NeedsPath
        {
            get { return needsPath; }
        }

        public bool IsVisible
        {
            get { return isVisible; }
            private set { SetProperty(ref isVisible, value); }
        }

        /// <summary>A keycut was created while the panel was showing and Win+R can find it; the panel then only shows how to use it.</summary>
        public bool IsSuccess
        {
            get { return createdName != null && !needsPath && error == null; }
        }

        /// <summary>A PATH update failure, or the PATH warning (which needs action, so it reads as one).</summary>
        public bool HelperIsError
        {
            get { return error != null || needsPath; }
        }

        /// <summary>The line under the prompt: empty until a keycut is created, then how to use it, or why Win+R can't find it yet.</summary>
        public string HelperText
        {
            get
            {
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
                    return string.Format("Created {0}. Press Win+R, type {0}, and press Enter to open it.", createdName);
                }

                return string.Empty;
            }
        }

        public bool HasHelperText
        {
            get { return HelperText.Length > 0; }
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

            if (count == 0 || !ReadDismissed())
            {
                ShowPanel();
            }
        }

        /// <summary>Shows the panel again from the start (Settings → Show welcome), whatever was decided or created before.</summary>
        public void Show()
        {
            decided = true;
            ShowPanel();
        }

        /// <summary>Got it: hides the panel for good and lets the creator clear the example.</summary>
        public void Dismiss()
        {
            IsVisible = false;
            TryMarkDismissed();
            Dismissed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A keycut was made in the creator. While the panel shows, that counts as using it: it is remembered, the panel says how to
        /// open the keycut, and a folder that isn't on PATH is flagged, since GUI creates never change PATH.
        /// </summary>
        public void OnKeycutCreated(string name)
        {
            if (!isVisible)
            {
                return;
            }

            createdName = name;
            error = null;
            needsPath = !isOnPath();
            TryMarkDismissed();
            RaiseStateChanged();
        }

        private void ShowPanel()
        {
            createdName = null;
            needsPath = false;
            error = null;
            IsVisible = true;
            RaiseStateChanged();
            Shown?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Puts the output folder on the user PATH, then checks again; a failure keeps the button and says so.</summary>
        private void AddToPath()
        {
            if (addToPath() && isOnPath())
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
            AddToPathCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NeedsPath));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(HelperIsError));
            OnPropertyChanged(nameof(HelperText));
            OnPropertyChanged(nameof(HasHelperText));
        }
    }
}
