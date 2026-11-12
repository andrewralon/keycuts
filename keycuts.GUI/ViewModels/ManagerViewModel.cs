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

    public enum SortColumn
    {
        /// <summary>The Name column. The member keeps its old name, as other internal names do (#168).</summary>
        Shortcut,
        Type,
        Description,
        Target,
        Modified
    }

    /// <summary>
    /// State of the manager: the loaded rows plus the loading, empty, no-results and error states.
    /// <see cref="Rows"/> holds the rows that match <see cref="SearchText"/>, in the order given by <see cref="SortColumn"/>.
    /// </summary>
    public class ManagerViewModel : ObservableObject
    {
        private readonly Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? keycutLoader;

        private readonly Func<IReadOnlyList<Keycut>, CancellationToken, Task>? targetChecker;

        private readonly List<KeycutRowViewModel> allRows = new List<KeycutRowViewModel>();

        private readonly IKeycutActions? actions;

        private readonly HashSet<KeycutRowViewModel> deleting = new HashSet<KeycutRowViewModel>();

        private int statusVersion;

        // The keycut the "Deleted" message in the status bar can put back (#179), while that message shows
        private DeletedKeycut? pendingUndo;

        private bool undoing;

        private string statusMessage = string.Empty;

        private bool statusIsError;

        private CancellationTokenSource? loadSource;

        private string searchText = string.Empty;

        private SortColumn sortColumn = SortColumn.Shortcut;

        private bool sortDescending;

        private bool isMenuOpen;

        private KeycutRowViewModel? selectedRow;

        private KeycutRowViewModel? renamingRow;

        // The file a rename is moving, while it's being saved. Actions on a row with that path wait for the save.
        private string? savingPath;

        // A renamed keycut's old path, and that keycut as it is on disk now. Until a reload rebuilds the rows (one may have
        // been cancelled), a row with the old path still shows the old name, and its actions use the renamed keycut. It's
        // matched by path, not by row, because a reload during the save may have rebuilt the rows.
        private string? staleOldPath;

        private Keycut? renamedKeycut;

        // A renamed keycut's new path, selected and focused by the next reload that rebuilds the rows
        private string? pendingSelectPath;

        private int keycutCount;

        private bool isLoading = true;

        private bool hasLoaded;

        private string? errorMessage;

        public ManagerViewModel(
            ICommand showCreatorCommand,
            ICommand openSettingsCommand,
            Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? keycutLoader = null,
            Func<IReadOnlyList<Keycut>, CancellationToken, Task>? targetChecker = null,
            IKeycutActions? actions = null)
        {
            ShowCreatorCommand = showCreatorCommand;
            OpenSettingsCommand = openSettingsCommand;
            this.keycutLoader = keycutLoader;
            this.targetChecker = targetChecker;
            this.actions = actions;

            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            SortCommand = new RelayCommand<SortColumn>(SortBy);
            UndoDeleteCommand = new RelayCommand(() => _ = UndoDeleteAsync());
        }

        public ICommand ShowCreatorCommand { get; }

        /// <summary>The status bar's Undo button, and Ctrl+Z, while <see cref="CanUndoDelete"/>. Does nothing otherwise.</summary>
        public ICommand UndoDeleteCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand ClearSearchCommand { get; }

        /// <summary>Sorts by the column passed as the command parameter, or flips the direction if it is already active.</summary>
        public ICommand SortCommand { get; }

        public SortColumn SortColumn
        {
            get { return sortColumn; }
        }

        public bool SortDescending
        {
            get { return sortDescending; }
        }

        /// <summary>The rows currently shown in the table.</summary>
        public ObservableCollection<KeycutRowViewModel> Rows { get; } = new ObservableCollection<KeycutRowViewModel>();

        /// <summary>The highlighted row, moved with Up and Down and opened with Enter. Survives searching and sorting while it stays visible.</summary>
        public KeycutRowViewModel? SelectedRow
        {
            get { return selectedRow; }
            set { SetProperty(ref selectedRow, value); }
        }

        /// <summary>
        /// What a new name runs into besides another keycut (#153): programs and scripts with the same name. The app sets
        /// it; without one (tests) a rename checks nothing. A taken keycut name is still refused by the rename itself.
        /// </summary>
        public Func<string, NameCheck>? CheckRename { get; set; }

        /// <summary>
        /// Asks to go ahead with a rename despite a name conflict; true renames. The view shows the confirmation dialog.
        /// Without one a rename with a conflict goes back to editing, so nothing is renamed unasked.
        /// </summary>
        public Func<NameCheck, bool>? ConfirmRename { get; set; }

        /// <summary>True while a row's name is being edited (F2 or Rename…). Reloads wait until it ends.</summary>
        public bool IsRenaming
        {
            get { return renamingRow != null; }
        }

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

        /// <summary>Raised after a keycut was deleted, so other views can refresh their counts.</summary>
        public event EventHandler? KeycutDeleted;

        /// <summary>Raised after an Undo put a deleted keycut back, so other views can refresh their counts.</summary>
        public event EventHandler? KeycutRestored;

        /// <summary>
        /// True while the status bar offers to undo the last delete: for <see cref="UndoDuration"/>, until another message
        /// replaces it, or until it's used. The view shows the Undo button, and Ctrl+Z works, only then.
        /// </summary>
        public bool CanUndoDelete
        {
            get { return pendingUndo != null; }
        }

        /// <summary>How long "Deleted" and its Undo stay: longer than other messages, since a button needs time to be reached.</summary>
        public TimeSpan UndoDuration { get; set; } = TimeSpan.FromSeconds(8);

        /// <summary>
        /// Raised after a delete to say where focus should go: the row to focus the menu button of,
        /// or null when no rows are left and the view should fall back to the search box.
        /// </summary>
        public event EventHandler<KeycutRowViewModel?>? FocusRowRequested;

        /// <summary>How long a status message stays visible; errors stay twice as long.</summary>
        public TimeSpan StatusMessageDuration { get; set; } = TimeSpan.FromSeconds(4);

        /// <summary>Brief confirmation or error shown in the status bar; empty when there is nothing to say.</summary>
        public string StatusMessage
        {
            get { return statusMessage; }
        }

        public bool StatusIsError
        {
            get { return statusIsError; }
        }

        /// <summary>
        /// Moves the selection by one row, stopping at either end. With nothing selected, Down starts at the first row and Up at the last.
        /// False when there are no rows to move through.
        /// </summary>
        public bool MoveSelection(int direction)
        {
            if (Rows.Count == 0)
            {
                return false;
            }

            var index = selectedRow == null ? -1 : Rows.IndexOf(selectedRow);

            if (index < 0)
            {
                index = direction > 0 ? 0 : Rows.Count - 1;
            }
            else
            {
                index = Math.Clamp(index + direction, 0, Rows.Count - 1);
            }

            SelectedRow = Rows[index];
            return true;
        }

        /// <summary>Opens the selected row. False when nothing is selected.</summary>
        public bool OpenSelected()
        {
            if (selectedRow == null)
            {
                return false;
            }

            OpenKeycut(selectedRow);
            return true;
        }

        public void OpenKeycut(KeycutRowViewModel row)
        {
            var keycut = KeycutOf(row);

            if (keycut != null)
            {
                Perform(row, "open", () => actions?.Launch(keycut.FilePath));
            }
        }

        public void EditKeycut(KeycutRowViewModel row)
        {
            var keycut = KeycutOf(row);

            if (keycut == null)
            {
                return;
            }

            try
            {
                actions?.Edit(keycut.FilePath);
            }
            catch (System.IO.FileNotFoundException)
            {
                ShowStatusMessage("This keycut no longer exists", true);
                _ = LoadAsync();
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Couldn't edit \u201C" + row.Name + "\u201D: " + ex.Message, true);
            }
        }

        /// <summary>Shows the keycut's .bat file in Explorer. A file that is gone reloads the list, as Edit does.</summary>
        public void OpenFileLocation(KeycutRowViewModel row)
        {
            var keycut = KeycutOf(row);

            if (keycut == null)
            {
                return;
            }

            try
            {
                actions?.OpenFileLocation(keycut.FilePath);
            }
            catch (System.IO.FileNotFoundException)
            {
                ShowStatusMessage("This keycut no longer exists", true);
                _ = LoadAsync();
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Couldn't open the file location of “" + row.Name + "”: " + ex.Message, true);
            }
        }

        public void CopyTarget(KeycutRowViewModel row)
        {
            if (Perform(row, "copy the target of", () => actions?.CopyText(row.Target)) && actions != null)
            {
                ShowStatusMessage("Copied", false);
            }
        }

        /// <summary>
        /// Deletes the keycut's file without asking (to the Recycle Bin), removes the row and asks the view to focus the
        /// next row. The status bar then offers Undo for <see cref="UndoDuration"/> (#179). A file that is already gone
        /// counts as deleted (with an error message); other failures keep the row.
        /// </summary>
        public async Task DeleteKeycutAsync(KeycutRowViewModel row)
        {
            if (actions == null || deleting.Contains(row))
            {
                return;
            }

            var keycut = KeycutOf(row);

            if (keycut == null)
            {
                return;
            }

            deleting.Add(row);

            try
            {
                var deleted = await actions.DeleteAsync(keycut);
                RemoveRow(row);

                // A drive without a Recycle Bin deleted it for good: say so, since Undo is then the only way back
                var message = "Deleted \u201C" + row.Name + "\u201D" + (deleted != null && !deleted.Recycled ? " (this drive has no Recycle Bin)" : string.Empty);
                ShowStatusMessage(message, false, deleted);
            }
            catch (KeycutRepositoryException ex) when (ex.Error == KeycutRepositoryError.NotFound)
            {
                RemoveRow(row);
                ShowStatusMessage("\u201C" + row.Name + "\u201D was already deleted.", true);
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Couldn't delete \u201C" + row.Name + "\u201D: " + ReasonOf(ex), true);
            }
            finally
            {
                deleting.Remove(row);
            }
        }

        /// <summary>
        /// Puts the last deleted keycut back, byte for byte, while the status bar still offers it (#179). The row reappears
        /// where the current sort puts it, selected and focused (a search that hides it is cleared). A name that was taken
        /// again is refused and said so: nothing is overwritten. Does nothing when there is nothing to undo.
        /// </summary>
        public async Task UndoDeleteAsync()
        {
            var deleted = pendingUndo;

            if (deleted == null || actions == null || undoing)
            {
                return;
            }

            var name = deleted.Keycut.Name;
            undoing = true;
            SetPendingUndo(null);

            try
            {
                await actions.RestoreAsync(deleted);
            }
            catch (KeycutRepositoryException ex) when (ex.Error == KeycutRepositoryError.AlreadyExists)
            {
                ShowStatusMessage("Can’t undo: “" + name + "” already exists.", true);
                return;
            }
            catch (Exception ex)
            {
                var binHint = deleted.Recycled ? " It’s still in the Recycle Bin." : string.Empty;
                ShowStatusMessage("Couldn’t restore “" + name + "”: " + ReasonOf(ex) + binHint, true);
                return;
            }
            finally
            {
                undoing = false;
            }

            var row = AddRestoredRow(deleted);
            ShowStatusMessage("Restored “" + name + "”", false);
            KeycutRestored?.Invoke(this, EventArgs.Empty);
            FocusRowRequested?.Invoke(this, row);
        }

        /// <summary>Shows the restored keycut's row again. A reload that already found the file shows it once, not twice.</summary>
        private KeycutRowViewModel AddRestoredRow(DeletedKeycut deleted)
        {
            var path = deleted.Keycut.FilePath;
            var row = allRows.FirstOrDefault(r => string.Equals(r.Keycut.FilePath, path, StringComparison.OrdinalIgnoreCase));

            if (row == null)
            {
                var keycut = deleted.Modified == default ? deleted.Keycut : deleted.Keycut with { Modified = deleted.Modified };
                row = new KeycutRowViewModel(keycut, this);
                allRows.Add(row);
            }

            KeycutCount = allRows.Count;

            // A search that hides it would make Undo look like it did nothing
            if (!row.Matches(searchText))
            {
                SearchText = string.Empty;
            }

            ApplyFilter();
            SelectedRow = row;
            return row;
        }

        /// <summary>
        /// Starts editing the row's name in place (F2 or Rename…). Only one row is edited at a time, and not while a
        /// previous rename is still saving. A load already under way is cancelled, so its result can't replace the row.
        /// </summary>
        public void BeginRename(KeycutRowViewModel row)
        {
            if (actions == null || savingPath != null || renamedKeycut != null || deleting.Contains(row))
            {
                return;
            }

            if (renamingRow != null && !ReferenceEquals(renamingRow, row))
            {
                renamingRow.EndRename();
            }

            Interlocked.Exchange(ref loadSource, null)?.Cancel();

            IsMenuOpen = false;
            SelectedRow = row;
            renamingRow = row;
            row.StartRename();
            OnPropertyChanged(nameof(IsRenaming));
        }

        /// <summary>Leaves the name unchanged, gives the focus back to the row, and catches up on any reload it held off.</summary>
        public void CancelRename()
        {
            var row = renamingRow;

            if (row == null)
            {
                return;
            }

            EndRename(row);
            FocusRowRequested?.Invoke(this, row);
            _ = LoadAsync();
        }

        /// <summary>
        /// Renames the keycut to the typed name (trimmed). An unchanged name just ends the edit. A name another command
        /// uses asks first (#153); a no goes back to editing the typed name. On success the list reloads with the renamed
        /// row selected; a failure (invalid or taken name, a file that's gone, a locked file) ends the edit and shows why
        /// in the status line. Never overwrites another keycut.
        /// </summary>
        public async Task CommitRenameAsync()
        {
            var row = renamingRow;

            if (row == null || actions == null)
            {
                return;
            }

            var oldName = row.Name;
            var newName = row.EditName.Trim();
            EndRename(row);

            if (string.Equals(newName, oldName, StringComparison.Ordinal))
            {
                FocusRowRequested?.Invoke(this, row);
                _ = LoadAsync();
                return;
            }

            if (!await ConfirmConflictsAsync(row, newName))
            {
                return;
            }

            // No other rename while the file is being renamed. The reloads afterwards aren't awaited, so slow target
            // checks can't hold this guard; stale rows after a success are blocked by renamedKeycut instead.
            savingPath = row.Keycut.FilePath;

            try
            {
                string newPath;
                try
                {
                    newPath = await actions.RenameAsync(row.Keycut, newName);
                }
                catch (KeycutRepositoryException ex) when (ex.Error == KeycutRepositoryError.AlreadyExists)
                {
                    ShowStatusMessage("“" + newName + "” already exists. Choose another name.", true);
                    FocusRowRequested?.Invoke(this, row);
                    _ = LoadAsync();
                    return;
                }
                catch (KeycutRepositoryException ex) when (ex.Error == KeycutRepositoryError.InvalidName)
                {
                    ShowStatusMessage(CreatorViewModel.GetInvalidNameMessage(newName), true);
                    FocusRowRequested?.Invoke(this, row);
                    _ = LoadAsync();
                    return;
                }
                catch (KeycutRepositoryException ex) when (ex.Error == KeycutRepositoryError.NotFound)
                {
                    ShowStatusMessage("This keycut no longer exists", true);
                    _ = LoadAsync();
                    return;
                }
                catch (Exception ex)
                {
                    ShowStatusMessage("Couldn't rename “" + oldName + "”: " + ReasonOf(ex), true);
                    FocusRowRequested?.Invoke(this, row);
                    _ = LoadAsync();
                    return;
                }

                ShowStatusMessage("Renamed “" + oldName + "” to “" + newName + "”", false);

                // Selected by whichever reload rebuilds the rows: F5 or reactivating the window can cancel this one.
                // Until then the row shows the old name and path, so no rename may start, and its actions use the new file.
                pendingSelectPath = newPath;
                staleOldPath = row.Keycut.FilePath;
                renamedKeycut = row.Keycut with { Name = newName, FilePath = newPath };
                _ = LoadAsync();
            }
            finally
            {
                savingPath = null;
            }
        }

        /// <summary>
        /// Checks the new name for other commands with it (#153) and asks if there are any. False when the user said no
        /// (or nobody could be asked): the row is back in rename mode with the typed name, so it can be changed.
        /// </summary>
        private async Task<bool> ConfirmConflictsAsync(KeycutRowViewModel row, string newName)
        {
            var checkRename = CheckRename;

            if (checkRename == null || !KeycutCreator.IsValidName(newName))
            {
                return true;
            }

            // Actions on this row wait while its name is checked, as they do while it's being renamed
            savingPath = row.Keycut.FilePath;
            NameCheck check;
            try
            {
                check = await Task.Run(() => checkRename(newName)) ?? NameCheck.None(newName);
            }
            catch (Exception)
            {
                check = NameCheck.None(newName);
            }
            finally
            {
                savingPath = null;
            }

            if (check.Conflicts.Count == 0 || ConfirmRename?.Invoke(check) == true)
            {
                return true;
            }

            // A reload during the check may have rebuilt the rows: carry on with the one for the same file
            var target = allRows.FirstOrDefault(r => string.Equals(r.Keycut.FilePath, row.Keycut.FilePath, StringComparison.OrdinalIgnoreCase)) ?? row;
            BeginRename(target);

            if (ReferenceEquals(renamingRow, target))
            {
                target.EditName = newName;
            }

            return false;
        }

        /// <summary>
        /// The keycut a row's actions work on: for a row a rename has made stale, the file under its new name. Null while
        /// that row's file is being renamed, since its path is about to change; the caller does nothing, and says so.
        /// </summary>
        private Keycut? KeycutOf(KeycutRowViewModel row)
        {
            var path = row.Keycut.FilePath;

            if (savingPath != null && string.Equals(path, savingPath, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusMessage("Still renaming “" + row.Name + "”", false);
                return null;
            }

            if (renamedKeycut != null && string.Equals(path, staleOldPath, StringComparison.OrdinalIgnoreCase))
            {
                return renamedKeycut;
            }

            return row.Keycut;
        }

        private void ForgetStaleRow()
        {
            staleOldPath = null;
            renamedKeycut = null;
        }

        /// <summary>The file system's own reason, which the repository wraps in a message that only names the file.</summary>
        private static string ReasonOf(Exception ex)
        {
            return ex is KeycutRepositoryException && ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        }

        private void EndRename(KeycutRowViewModel row)
        {
            row.EndRename();

            if (ReferenceEquals(renamingRow, row))
            {
                renamingRow = null;
                OnPropertyChanged(nameof(IsRenaming));
            }
        }

        private bool Perform(KeycutRowViewModel row, string verb, Action action)
        {
            if (actions == null)
            {
                return false;
            }

            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Couldn't " + verb + " \u201C" + row.Name + "\u201D: " + ex.Message, true);
                return false;
            }
        }

        private void RemoveRow(KeycutRowViewModel row)
        {
            var index = Rows.IndexOf(row);
            var wasSelected = ReferenceEquals(row, selectedRow);
            IsMenuOpen = false;
            allRows.Remove(row);
            KeycutCount = allRows.Count;
            ApplyFilter();

            KeycutRowViewModel? next = null;

            if (Rows.Count > 0)
            {
                next = Rows[Math.Min(Math.Max(index, 0), Rows.Count - 1)];
            }

            if (wasSelected)
            {
                SelectedRow = next;
            }

            KeycutDeleted?.Invoke(this, EventArgs.Empty);
            FocusRowRequested?.Invoke(this, next);
        }

        /// <param name="undo">A delete this message offers to undo (#179). It lasts as long as the message does: another
        /// message replaces it, and then the Undo button and Ctrl+Z are gone.</param>
        private async void ShowStatusMessage(string message, bool isError, DeletedKeycut? undo = null)
        {
            var version = ++statusVersion;
            SetStatus(message, isError);
            SetPendingUndo(undo);

            var duration = undo != null ? UndoDuration : isError ? StatusMessageDuration + StatusMessageDuration : StatusMessageDuration;
            await Task.Delay(duration);

            if (version == statusVersion)
            {
                SetStatus(string.Empty, false);
                SetPendingUndo(null);
            }
        }

        private void SetPendingUndo(DeletedKeycut? undo)
        {
            if (!ReferenceEquals(pendingUndo, undo))
            {
                pendingUndo = undo;
                OnPropertyChanged(nameof(CanUndoDelete));
            }
        }

        private void SetStatus(string message, bool isError)
        {
            statusMessage = message;
            statusIsError = isError;
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(StatusIsError));
        }

        /// <summary>
        /// Loads the keycuts and then checks their targets in the background. A newer call cancels the previous one.
        /// The loading indicator shows only for the first load; later refreshes update the table quietly.
        /// </summary>
        public async Task LoadAsync()
        {
            // A reload builds new rows, which would throw away the name being typed (the window reloads when it's activated)
            if (renamingRow != null)
            {
                return;
            }

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

                foreach (var row in allRows)
                {
                    row.RefreshModified();
                }

                ForgetStaleRow();
                SelectPendingRename();
            }
            else
            {
                ShowKeycuts(keycuts);
            }

            await CheckTargetsAsync(keycuts, token);
        }

        /// <summary>Replaces the rows with the given keycuts and leaves the loading and error states.</summary>
        public void ShowKeycuts(IReadOnlyList<Keycut> keycuts)
        {
            var selectedPath = selectedRow?.Keycut.FilePath;

            allRows.Clear();
            allRows.AddRange(keycuts.Select(keycut => new KeycutRowViewModel(keycut, this)));
            errorMessage = null;
            isLoading = false;
            hasLoaded = true;
            KeycutCount = allRows.Count;
            ApplyFilter();

            // A reload builds new rows, so find the selected keycut again
            SelectedRow = Rows.FirstOrDefault(row => row.Keycut.FilePath == selectedPath);
            ForgetStaleRow();
            SelectPendingRename();
        }

        /// <summary>
        /// Selects and focuses a just-renamed keycut once a reload has built its row. Its new name may not match the
        /// search, so the search is cleared rather than hiding it. Stays pending until a reload finds it.
        /// </summary>
        private void SelectPendingRename()
        {
            if (pendingSelectPath == null)
            {
                return;
            }

            var renamed = allRows.FirstOrDefault(r => string.Equals(r.Keycut.FilePath, pendingSelectPath, StringComparison.OrdinalIgnoreCase));

            if (renamed == null)
            {
                return;
            }

            pendingSelectPath = null;

            if (!Rows.Contains(renamed))
            {
                SearchText = string.Empty;
            }

            SelectedRow = renamed;
            FocusRowRequested?.Invoke(this, renamed);
        }

        public void ShowError(string message)
        {
            allRows.Clear();
            errorMessage = string.IsNullOrWhiteSpace(message) ? "The keycuts folder can't be read." : message;
            isLoading = false;
            hasLoaded = true;
            KeycutCount = 0;
            ForgetStaleRow();
            ApplyFilter();
        }

        /// <summary>
        /// Default is ascending, except Modified, which starts with the newest first (what a click on it is for);
        /// selecting the active column again toggles the direction.
        /// </summary>
        public void SortBy(SortColumn column)
        {
            if (column == sortColumn)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                sortColumn = column;
                sortDescending = column == SortColumn.Modified;
            }

            OnPropertyChanged(nameof(SortColumn));
            OnPropertyChanged(nameof(SortDescending));
            ApplyFilter();
        }

        /// <summary>Culture-aware, case-insensitive comparer for the column, with the name as the tie-breaker.</summary>
        public static IComparer<KeycutRowViewModel> CreateComparer(SortColumn column, bool descending)
        {
            return Comparer<KeycutRowViewModel>.Create((x, y) =>
            {
                var result = 0;

                switch (column)
                {
                    case SortColumn.Shortcut:
                        result = CompareText(x.Name, y.Name);
                        break;
                    case SortColumn.Type:
                        result = CompareText(x.TypeLabel, y.TypeLabel);
                        break;
                    case SortColumn.Description:
                        result = CompareText(x.Description, y.Description);
                        break;
                    case SortColumn.Target:
                        result = CompareText(x.Target, y.Target);
                        break;
                    case SortColumn.Modified:
                        result = x.Modified.CompareTo(y.Modified);
                        break;
                }

                if (descending)
                {
                    result = -result;
                }

                return result != 0 ? result : CompareText(x.Name, y.Name);
            });
        }

        private static int CompareText(string left, string right)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
        }

        /// <summary>
        /// Rebuilds <see cref="Rows"/> from the loaded rows, filtered by <see cref="SearchText"/> and sorted.
        /// The collection is left alone when the result is unchanged, so the table keeps its scroll position.
        /// </summary>
        protected virtual void ApplyFilter()
        {
            // The table clears its selection along with the rows, so hold on to it
            var selected = selectedRow;

            var visible = allRows
                .Where(row => row.Matches(searchText))
                .OrderBy(row => row, CreateComparer(sortColumn, sortDescending))
                .ToList();

            if (!Rows.SequenceEqual(visible))
            {
                Rows.Clear();

                foreach (var row in visible)
                {
                    Rows.Add(row);
                }
            }

            SelectedRow = selected != null && Rows.Contains(selected) ? selected : null;

            RaiseStateChanged();
        }

        private async Task CheckTargetsAsync(IReadOnlyList<Keycut> keycuts, CancellationToken token)
        {
            if (targetChecker == null || keycuts.Count == 0)
            {
                return;
            }

            try
            {
                await targetChecker(keycuts, token);
            }
            catch (Exception)
            {
                // Missing-target flags are a nicety; cancellation and failures just leave them as they were.
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var row in allRows)
            {
                row.RefreshTargetStatus();
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
                    old.Description != next.Description || old.Target != next.Target || old.Modified != next.Modified)
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
