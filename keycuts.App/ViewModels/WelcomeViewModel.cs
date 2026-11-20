using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The first-run panel at the top of the creator (#61, #11, #213): says what keycuts does and offers a few keycuts to
    /// start with, as bubbles. A bubble fills its target and name into the creator's own fields rather than the panel having
    /// a form of its own, and the creator's Create button makes it. The panel shows on the first run (until it's dismissed or
    /// used, upgrades included), whenever the keycuts folder is empty, and on demand from Settings (<see cref="Show"/>).
    /// </summary>
    public class WelcomeViewModel : ObservableObject
    {
        /// <summary>The line under the bubbles once one is picked: the rocket is the creator's Create button.</summary>
        public const string CreateHint = "Click the rocket button to create it";

        /// <summary>The line after a create. It never names the keycut: the Name field does.</summary>
        public const string CreatedHint = "Created. Now try it out! Press Win+R, type its name, and press Enter to open it.";

        private readonly Func<bool> isDismissed;

        private readonly Action markDismissed;

        private readonly Func<string?, bool> isOnPath;

        private readonly Func<string?, bool> addToPath;

        // Where the last keycut was written, so the PATH check and Add to PATH use that folder even if Settings changes it
        private string? createdFolder;

        private bool decided;

        private bool needsPath;

        private bool isVisible;

        private bool created;

        private string? error;

        private WelcomeExample? selected;

        /// <param name="isDismissed">Reads whether the panel was dismissed before (HKCU in production).</param>
        /// <param name="markDismissed">Records that it was dismissed. A failure is ignored: the panel just shows again next time.</param>
        /// <param name="isOnPath">Whether Win+R can find keycuts in the given folder (it's on the user or machine PATH). The folder
        /// is where the keycut was written, or null if the create result didn't say (then use the output folder). Null treats it as on
        /// PATH: tests, and the Debug folder override, which must never write PATH.</param>
        /// <param name="addToPath">Adds the given folder (as for <paramref name="isOnPath"/>) to the user PATH, as the installer and
        /// Settings do. False on failure.</param>
        /// <param name="examples">The bubbles, first one first; defaults to <see cref="WelcomeExample.CreateDefaults"/>.</param>
        public WelcomeViewModel(
            Func<bool> isDismissed,
            Action markDismissed,
            Func<string?, bool>? isOnPath = null,
            Func<string?, bool>? addToPath = null,
            IReadOnlyList<WelcomeExample>? examples = null)
        {
            this.isDismissed = isDismissed ?? throw new ArgumentNullException(nameof(isDismissed));
            this.markDismissed = markDismissed ?? throw new ArgumentNullException(nameof(markDismissed));
            this.isOnPath = isOnPath ?? (folder => true);
            this.addToPath = addToPath ?? (folder => true);
            Examples = examples ?? WelcomeExample.CreateDefaults();

            DismissCommand = new RelayCommand(Dismiss);
            AddToPathCommand = new RelayCommand(AddToPath, () => needsPath);
            SelectExampleCommand = new RelayCommand<WelcomeExample>(Select);
        }

        /// <summary>Raised when the panel starts showing, so the creator can pick the first example if its form is empty.</summary>
        public event EventHandler? Shown;

        /// <summary>Raised when an example is picked, so the creator can put it in its fields.</summary>
        public event EventHandler<WelcomeExample>? ExampleSelected;

        /// <summary>Raised after Got it hid the panel, so the creator can clear the fields an example filled in.</summary>
        public event EventHandler? Dismissed;

        public IReadOnlyList<WelcomeExample> Examples { get; }

        public RelayCommand DismissCommand { get; }

        public RelayCommand AddToPathCommand { get; }

        /// <summary>Picks a bubble; the parameter is the <see cref="WelcomeExample"/>.</summary>
        public RelayCommand<WelcomeExample> SelectExampleCommand { get; }

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
            get { return created && !needsPath && error == null; }
        }

        /// <summary>A PATH update failure, or the PATH warning (which needs action, so it reads as one).</summary>
        public bool HelperIsError
        {
            get { return error != null || needsPath; }
        }

        /// <summary>The rocket hint is showing, so the line gets a small rocket in front of it.</summary>
        public bool ShowRocket
        {
            get { return selected != null && !created && !HelperIsError; }
        }

        /// <summary>
        /// The line under the bubbles: empty until one is picked, then which button to click, then how to use the keycut once it
        /// exists, or why Win+R can't find it yet.
        /// </summary>
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
                    return "Created, but Win+R can’t find it yet: the keycuts folder isn’t on your PATH.";
                }

                if (created)
                {
                    return CreatedHint;
                }

                return selected != null ? CreateHint : string.Empty;
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

        /// <summary>
        /// Picks a bubble: it is highlighted, its target and name go to the creator, and the line under the bubbles says to click
        /// Create. Picking another one later does the same again, so each can be created in turn, and clears a PATH warning left by
        /// an earlier create.
        /// </summary>
        public void Select(WelcomeExample example)
        {
            if (example == null)
            {
                throw new ArgumentNullException(nameof(example));
            }

            SetSelected(example);
            created = false;
            error = null;

            // The warning was about the keycut made before; the next create checks its folder again and brings it back if need be
            needsPath = false;
            createdFolder = null;
            RaiseStateChanged();
            ExampleSelected?.Invoke(this, example);
        }

        /// <summary>Got it: hides the panel for good and lets the creator clear the example.</summary>
        public void Dismiss()
        {
            IsVisible = false;
            SetSelected(null);
            TryMarkDismissed();
            Dismissed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A keycut was made in the creator. While the panel shows, that counts as using it: it is remembered, the panel says how to
        /// open the keycut, and a folder that isn't on PATH is flagged, since GUI creates never change PATH.
        /// </summary>
        /// <param name="folder">The folder the keycut was written to, or null if unknown (the output folder is used).</param>
        public void OnKeycutCreated(string? folder = null)
        {
            if (!isVisible)
            {
                return;
            }

            created = true;
            error = null;
            createdFolder = folder;
            needsPath = !isOnPath(createdFolder);
            TryMarkDismissed();
            RaiseStateChanged();
        }

        private void ShowPanel()
        {
            created = false;
            needsPath = false;
            error = null;
            createdFolder = null;
            SetSelected(null);
            IsVisible = true;
            RaiseStateChanged();
            Shown?.Invoke(this, EventArgs.Empty);
        }

        private void SetSelected(WelcomeExample? example)
        {
            selected = example;

            foreach (var each in Examples)
            {
                each.IsSelected = ReferenceEquals(each, example);
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
            AddToPathCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NeedsPath));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(HelperIsError));
            OnPropertyChanged(nameof(ShowRocket));
            OnPropertyChanged(nameof(HelperText));
            OnPropertyChanged(nameof(HasHelperText));
        }
    }
}
