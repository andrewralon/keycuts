using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    public enum ManagerState
    {
        Loading,
        Populated,
        Empty,
        NoResults,
        Error
    }

    /// <summary>
    /// State of the manager: the loaded rows plus the loading, empty, no-results and error states.
    /// Search filtering (KC-13) and row menus (KC-14) build on <see cref="Rows"/> and <see cref="ApplyFilter"/>.
    /// </summary>
    public class ManagerViewModel : ObservableObject
    {
        private readonly Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? keycutLoader;

        private readonly Func<IReadOnlyList<Keycut>, CancellationToken, Task>? destinationChecker;

        private readonly List<KeycutRowViewModel> allRows = new List<KeycutRowViewModel>();

        private CancellationTokenSource? loadSource;

        private string searchText = string.Empty;

        private bool isMenuOpen;

        private int keycutCount;

        private bool isLoading = true;

        private bool hasLoaded;

        private string? errorMessage;

        public ManagerViewModel(
            ICommand showCreatorCommand,
            ICommand openSettingsCommand,
            Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? keycutLoader = null,
            Func<IReadOnlyList<Keycut>, CancellationToken, Task>? destinationChecker = null)
        {
            ShowCreatorCommand = showCreatorCommand;
            OpenSettingsCommand = openSettingsCommand;
            this.keycutLoader = keycutLoader;
            this.destinationChecker = destinationChecker;

            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        }

        public ICommand ShowCreatorCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand ClearSearchCommand { get; }

        /// <summary>The rows currently shown in the table.</summary>
        public ObservableCollection<KeycutRowViewModel> Rows { get; } = new ObservableCollection<KeycutRowViewModel>();

        /// <summary>True when this view model can load keycuts itself.</summary>
        public bool CanLoad
        {
            get { return keycutLoader != null; }
        }

        public string SearchText
        {
            get { return searchText; }
            set
            {
                if (SetProperty(ref searchText, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>True while a row actions menu is open. Escape closes it before anything else.</summary>
        public bool IsMenuOpen
        {
            get { return isMenuOpen; }
            set { SetProperty(ref isMenuOpen, value); }
        }

        /// <summary>Total number of keycuts (not filtered).</summary>
        public int KeycutCount
        {
            get { return keycutCount; }
            set
            {
                if (SetProperty(ref keycutCount, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public ManagerState State
        {
            get
            {
                if (errorMessage != null)
                {
                    return ManagerState.Error;
                }

                if (isLoading)
                {
                    return ManagerState.Loading;
                }

                if (allRows.Count == 0)
                {
                    return ManagerState.Empty;
                }

                return Rows.Count == 0 ? ManagerState.NoResults : ManagerState.Populated;
            }
        }

        public bool IsLoading
        {
            get { return State == ManagerState.Loading; }
        }

        public bool IsEmpty
        {
            get { return State == ManagerState.Empty; }
        }

        public bool IsNoResults
        {
            get { return State == ManagerState.NoResults; }
        }

        public bool HasError
        {
            get { return State == ManagerState.Error; }
        }

        public bool IsTableVisible
        {
            get { return State == ManagerState.Populated; }
        }

        /// <summary>Search is available only when there is something to search.</summary>
        public bool CanSearch
        {
            get { return State == ManagerState.Populated || State == ManagerState.NoResults; }
        }

        public string ErrorMessage
        {
            get { return errorMessage ?? string.Empty; }
        }

        /// <summary>Toolbar count: the filtered results.</summary>
        public string ResultCountText
        {
            get { return Rows.Count + (Rows.Count == 1 ? " keycut" : " keycuts"); }
        }

        /// <summary>Status bar text: the total count.</summary>
        public string StatusText
        {
            get { return keycutCount + (keycutCount == 1 ? " item" : " items"); }
        }

        public string NoResultsMessage
        {
            get { return "No keycuts match \u201C" + searchText + "\u201D"; }
        }

        /// <summary>
        /// Loads the keycuts and then checks their destinations in the background. A newer call cancels the previous one.
        /// The loading indicator shows only for the first load; later refreshes update the table quietly.
        /// </summary>
        public async Task LoadAsync()
        {
            if (keycutLoader == null)
            {
                ShowKeycuts(Array.Empty<Keycut>());
                return;
            }

            var source = new CancellationTokenSource();
            Interlocked.Exchange(ref loadSource, source)?.Cancel();
            var token = source.Token;

            if (!hasLoaded)
            {
                SetLoading(true);
            }

            IReadOnlyList<Keycut> keycuts;

            try
            {
                keycuts = await keycutLoader(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    ShowError(ex.Message);
                }

                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Keep the existing rows (and the scroll position) when nothing changed on disk.
            if (IsSameContent(keycuts))
            {
                keycuts = allRows.Select(row => row.Keycut).ToList();
            }
            else
            {
                ShowKeycuts(keycuts);
            }

            await CheckDestinationsAsync(keycuts, token);
        }

        /// <summary>Replaces the rows with the given keycuts and leaves the loading and error states.</summary>
        public void ShowKeycuts(IReadOnlyList<Keycut> keycuts)
        {
            allRows.Clear();
            allRows.AddRange(keycuts.Select(keycut => new KeycutRowViewModel(keycut)));
            errorMessage = null;
            isLoading = false;
            hasLoaded = true;
            KeycutCount = allRows.Count;
            ApplyFilter();
        }

        public void ShowError(string message)
        {
            allRows.Clear();
            errorMessage = string.IsNullOrWhiteSpace(message) ? "The keycuts folder can't be read." : message;
            isLoading = false;
            hasLoaded = true;
            KeycutCount = 0;
            ApplyFilter();
        }

        /// <summary>
        /// Rebuilds <see cref="Rows"/> from the loaded rows. KC-13 narrows this by <see cref="SearchText"/>.
        /// </summary>
        protected virtual void ApplyFilter()
        {
            Rows.Clear();

            foreach (var row in allRows)
            {
                Rows.Add(row);
            }

            RaiseStateChanged();
        }

        private async Task CheckDestinationsAsync(IReadOnlyList<Keycut> keycuts, CancellationToken token)
        {
            if (destinationChecker == null || keycuts.Count == 0)
            {
                return;
            }

            try
            {
                await destinationChecker(keycuts, token);
            }
            catch (Exception)
            {
                // Missing-destination flags are a nicety; cancellation and failures just leave them as they were.
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var row in allRows)
            {
                row.RefreshDestinationStatus();
            }
        }

        private bool IsSameContent(IReadOnlyList<Keycut> keycuts)
        {
            if (!hasLoaded || errorMessage != null || keycuts.Count != allRows.Count)
            {
                return false;
            }

            for (var i = 0; i < keycuts.Count; i++)
            {
                var old = allRows[i].Keycut;
                var next = keycuts[i];

                if (old.FilePath != next.FilePath || old.Name != next.Name || old.Kind != next.Kind ||
                    old.Description != next.Description || old.Destination != next.Destination)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetLoading(bool value)
        {
            isLoading = value;
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsNoResults));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(IsTableVisible));
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(ResultCountText));
            OnPropertyChanged(nameof(NoResultsMessage));
        }
    }
}
