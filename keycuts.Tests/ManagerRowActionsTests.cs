#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class ManagerRowActionsTests
    {
        class FakeActions : IKeycutActions
        {
            public List<string> Launched { get; } = new List<string>();

            public List<string> Edited { get; } = new List<string>();

            public List<string> Copied { get; } = new List<string>();

            public List<string> Deleted { get; } = new List<string>();

            public Exception? LaunchError { get; set; }

            public Exception? EditError { get; set; }

            public Exception? CopyError { get; set; }

            public Exception? DeleteError { get; set; }

            public void Launch(string path)
            {
                if (LaunchError != null)
                {
                    throw LaunchError;
                }

                Launched.Add(path);
            }

            public void Edit(string path)
            {
                if (EditError != null)
                {
                    throw EditError;
                }

                Edited.Add(path);
            }

            public void CopyText(string text)
            {
                if (CopyError != null)
                {
                    throw CopyError;
                }

                Copied.Add(text);
            }

            public Task DeleteAsync(Keycut keycut)
            {
                if (DeleteError != null)
                {
                    return Task.FromException(DeleteError);
                }

                Deleted.Add(keycut.FilePath);
                return Task.CompletedTask;
            }

            public List<(string Path, string NewName)> Renamed { get; } = new List<(string, string)>();

            public Exception? RenameError { get; set; }

            /// <summary>When set, a rename waits for this before it finishes.</summary>
            public Task? RenameGate { get; set; }

            public async Task<string> RenameAsync(Keycut keycut, string newName)
            {
                if (RenameGate != null)
                {
                    await RenameGate;
                }

                if (RenameError != null)
                {
                    throw RenameError;
                }

                Renamed.Add((keycut.FilePath, newName));
                return @"C:\fixture\" + newName + ".bat";
            }
        }

        static Keycut NewKeycut(string name, string destination = @"C:\temp\a.txt")
        {
            return new Keycut
            {
                Name = name,
                Kind = KeycutKind.File,
                RawType = ShortcutType.File,
                Description = string.Empty,
                Destination = destination,
                FilePath = @"C:\fixture\" + name + ".bat"
            };
        }

        static async Task<ManagerViewModel> LoadedAsync(FakeActions actions, params string[] names)
        {
            var noop = new RelayCommand(() => { });
            var keycuts = names.Select(n => NewKeycut(n)).ToList();
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            return manager;
        }

        [Fact]
        public async Task Open_LaunchesTheKeycutFile()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");

            manager.Rows[1].OpenCommand.Execute(null);

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Launched);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task Open_Failure_ShowsErrorInStatus()
        {
            var actions = new FakeActions { LaunchError = new InvalidOperationException("nope") };
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].OpenCommand.Execute(null);

            Assert.True(manager.StatusIsError);
            Assert.Contains("nope", manager.StatusMessage);
            Assert.Contains("a", manager.StatusMessage);
        }

        [Fact]
        public async Task Edit_UsesTheEditSeam()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].EditCommand.Execute(null);

            Assert.Equal(new[] { @"C:\fixture\a.bat" }, actions.Edited);
        }

        [Fact]
        public async Task Edit_FileGone_ShowsMessage_AndRefreshesTheList()
        {
            var noop = new RelayCommand(() => { });
            var actions = new FakeActions { EditError = new System.IO.FileNotFoundException("gone") };
            var current = new List<Keycut> { NewKeycut("a"), NewKeycut("b") };
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(current.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            current.RemoveAt(0);

            manager.Rows[0].EditCommand.Execute(null);

            Assert.Equal("This keycut no longer exists", manager.StatusMessage);
            Assert.True(manager.StatusIsError);
            await Task.Delay(200);
            Assert.Equal(new[] { "b" }, manager.Rows.Select(r => r.Name).ToArray());
        }

        [Fact]
        public async Task Edit_OtherFailure_ShowsErrorInStatus()
        {
            var actions = new FakeActions { EditError = new InvalidOperationException("no notepad") };
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].EditCommand.Execute(null);

            Assert.True(manager.StatusIsError);
            Assert.Contains("no notepad", manager.StatusMessage);
        }

        [Fact]
        public async Task Rename_StartsWithTheCurrentName_AndSelectsTheRow()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b");
            var row = manager.Rows[1];

            row.RenameCommand.Execute(null);

            Assert.True(manager.IsRenaming);
            Assert.True(row.IsRenaming);
            Assert.Equal("b", row.EditName);
            Assert.Same(row, manager.SelectedRow);
            Assert.Equal("New name for b", row.RenameLabel);
        }

        [Fact]
        public async Task Rename_Cancel_ChangesNothing()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            var row = manager.Rows[0];
            manager.BeginRename(row);
            row.EditName = "z";

            manager.CancelRename();

            Assert.False(manager.IsRenaming);
            Assert.False(row.IsRenaming);
            Assert.Empty(actions.Renamed);
            Assert.Equal("a", row.Name);
        }

        [Fact]
        public async Task Rename_UnchangedName_JustEndsTheEdit()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = " a ";

            await manager.CommitRenameAsync();

            Assert.False(manager.IsRenaming);
            Assert.Empty(actions.Renamed);
        }

        [Fact]
        public async Task Rename_Success_RenamesTrimmed_ReloadsAndSelectsTheRenamedRow()
        {
            var noop = new RelayCommand(() => { });
            var actions = new FakeActions();
            var current = new List<Keycut> { NewKeycut("a"), NewKeycut("b") };
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(current.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            KeycutRowViewModel? focused = null;
            manager.FocusRowRequested += (sender, row) => focused = row;

            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "  zed ";
            current = new List<Keycut> { NewKeycut("b"), NewKeycut("zed") };
            await manager.CommitRenameAsync();

            Assert.Equal(new[] { (@"C:\fixture\a.bat", "zed") }, actions.Renamed);
            Assert.Equal(new[] { "b", "zed" }, manager.Rows.Select(r => r.Name).ToArray());
            Assert.Equal("zed", manager.SelectedRow?.Name);
            Assert.Same(manager.SelectedRow, focused);
            Assert.Equal("Renamed \u201Ca\u201D to \u201Czed\u201D", manager.StatusMessage);
            Assert.False(manager.StatusIsError);
            Assert.False(manager.IsRenaming);
        }

        [Theory]
        [InlineData(KeycutRepositoryError.AlreadyExists, "\u201Cb\u201D already exists. Choose another name.")]
        [InlineData(KeycutRepositoryError.InvalidName, CreatorViewModel.InvalidNameMessage)]
        [InlineData(KeycutRepositoryError.InUse, "Couldn't rename \u201Ca\u201D: locked")]
        public async Task Rename_Failure_EndsTheEdit_AndSaysWhy(KeycutRepositoryError error, string message)
        {
            var actions = new FakeActions { RenameError = new KeycutRepositoryException(error, "locked") };
            var manager = await LoadedAsync(actions, "a", "b");
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "b";

            await manager.CommitRenameAsync();

            Assert.False(manager.IsRenaming);
            Assert.True(manager.StatusIsError);
            Assert.Equal(message, manager.StatusMessage);
            Assert.Equal(new[] { "a", "b" }, manager.Rows.Select(r => r.Name).ToArray());
        }

        [Fact]
        public async Task Rename_FileGone_ShowsMessage_AndRefreshesTheList()
        {
            var noop = new RelayCommand(() => { });
            var actions = new FakeActions { RenameError = new KeycutRepositoryException(KeycutRepositoryError.NotFound, "gone") };
            var current = new List<Keycut> { NewKeycut("a"), NewKeycut("b") };
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(current.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "c";
            current.RemoveAt(0);

            await manager.CommitRenameAsync();

            Assert.Equal("This keycut no longer exists", manager.StatusMessage);
            Assert.Equal(new[] { "b" }, manager.Rows.Select(r => r.Name).ToArray());
        }

        [Fact]
        public async Task Reload_WhileRenaming_IsSkipped_SoTheTypedNameSurvives()
        {
            var noop = new RelayCommand(() => { });
            var loads = 0;
            var manager = new ManagerViewModel(noop, noop, ct =>
            {
                loads++;
                return Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a") });
            }, null, new FakeActions());
            await manager.LoadAsync();
            var row = manager.Rows[0];
            manager.BeginRename(row);
            row.EditName = "typing";

            await manager.LoadAsync();

            Assert.Equal(1, loads);
            Assert.Same(row, manager.Rows[0]);
            Assert.Equal("typing", row.EditName);
        }

        [Fact]
        public async Task Rename_CancelsALoadAlreadyUnderWay_SoItCantReplaceTheRow()
        {
            var noop = new RelayCommand(() => { });
            var gate = new TaskCompletionSource<IReadOnlyList<Keycut>>();
            var first = true;
            var manager = new ManagerViewModel(noop, noop, ct =>
            {
                if (first)
                {
                    first = false;
                    return Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a") });
                }

                return gate.Task;
            }, null, new FakeActions());
            await manager.LoadAsync();
            var row = manager.Rows[0];

            var pending = manager.LoadAsync();
            manager.BeginRename(row);
            row.EditName = "typing";
            gate.SetResult(new List<Keycut> { NewKeycut("a"), NewKeycut("b") });
            await pending;

            Assert.Same(row, manager.Rows.Single());
            Assert.True(row.IsRenaming);
            Assert.Equal("typing", row.EditName);
        }

        [Fact]
        public async Task Rename_AnotherCantStart_UntilTheFirstIsSaved()
        {
            var gate = new TaskCompletionSource();
            var actions = new FakeActions { RenameGate = gate.Task };
            var manager = await LoadedAsync(actions, "a", "b");
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "z";

            var saving = manager.CommitRenameAsync();
            manager.BeginRename(manager.Rows[1]);

            Assert.False(manager.IsRenaming);
            Assert.False(manager.Rows[1].IsRenaming);

            gate.SetResult();
            await saving;
            manager.BeginRename(manager.Rows[1]);
            Assert.True(manager.IsRenaming);
        }

        [Fact]
        public async Task Rename_ASearchResult_ToANameTheSearchMisses_ClearsTheSearchAndSelectsIt()
        {
            var noop = new RelayCommand(() => { });
            var current = new List<Keycut> { NewKeycut("alpha"), NewKeycut("bravo") };
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(current.ToList()), null, new FakeActions())
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            manager.SearchText = "alp";
            KeycutRowViewModel? focused = null;
            manager.FocusRowRequested += (sender, row) => focused = row;

            manager.BeginRename(manager.Rows.Single());
            manager.Rows.Single().EditName = "zulu";
            current = new List<Keycut> { NewKeycut("bravo"), NewKeycut("zulu") };
            await manager.CommitRenameAsync();

            Assert.Equal(string.Empty, manager.SearchText);
            Assert.Equal("zulu", manager.SelectedRow?.Name);
            Assert.Contains(manager.SelectedRow!, manager.Rows);
            Assert.Same(manager.SelectedRow, focused);
        }

        [Fact]
        public async Task Rename_StartingAnother_EndsTheFirst()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b");

            manager.BeginRename(manager.Rows[0]);
            manager.BeginRename(manager.Rows[1]);

            Assert.False(manager.Rows[0].IsRenaming);
            Assert.True(manager.Rows[1].IsRenaming);
        }

        [Fact]
        public async Task Reload_AfterDescriptionEdit_ShowsNewDescription()
        {
            var noop = new RelayCommand(() => { });
            var description = string.Empty;
            var manager = new ManagerViewModel(noop, noop, ct =>
            {
                var keycut = new Keycut
                {
                    Name = "a",
                    Kind = KeycutKind.File,
                    RawType = ShortcutType.File,
                    Description = description,
                    Destination = @"C:\temp\a.txt",
                    FilePath = @"C:\fixture\a.bat"
                };
                return Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { keycut });
            }, null, new FakeActions());
            await manager.LoadAsync();

            description = "my notes";
            await manager.LoadAsync();

            Assert.Equal("my notes", manager.Rows[0].Description);
        }

        [Fact]
        public void SystemEdit_MissingFile_ThrowsWithoutLaunching()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.bat");

            Assert.Throws<System.IO.FileNotFoundException>(() => new SystemKeycutActions().Edit(path));
        }

        [Fact]
        public async Task CopyDestination_CopiesAndConfirms()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].CopyDestinationCommand.Execute(null);

            Assert.Equal(new[] { @"C:\temp\a.txt" }, actions.Copied);
            Assert.Equal("Copied", manager.StatusMessage);
            Assert.False(manager.StatusIsError);
        }

        [Fact]
        public async Task CopyDestination_ClipboardLocked_ShowsError()
        {
            var actions = new FakeActions { CopyError = new InvalidOperationException("clipboard busy") };
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].CopyDestinationCommand.Execute(null);

            Assert.True(manager.StatusIsError);
            Assert.Contains("clipboard busy", manager.StatusMessage);
        }

        [Fact]
        public async Task Delete_RemovesRow_UpdatesCounts_AndSaysSo()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b", "c");
            var deletedEvents = 0;
            manager.KeycutDeleted += (s, e) => deletedEvents++;

            await manager.DeleteKeycutAsync(manager.Rows[1]);

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Deleted);
            Assert.Equal(new[] { "a", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(2, manager.KeycutCount);
            Assert.Equal("2 items", manager.StatusText);
            Assert.Equal("2 keycuts", manager.ResultCountText);
            Assert.Equal("Deleted \u201Cb\u201D", manager.StatusMessage);
            Assert.False(manager.StatusIsError);
            Assert.Equal(1, deletedEvents);
        }

        [Fact]
        public async Task Delete_RequestsFocusOnTheNextRow()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b", "c");
            var requests = new List<KeycutRowViewModel?>();
            manager.FocusRowRequested += (s, row) => requests.Add(row);

            await manager.DeleteKeycutAsync(manager.Rows[1]);
            Assert.Equal("c", requests.Single()!.Name);

            requests.Clear();
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            Assert.Equal("a", requests.Single()!.Name);
        }

        [Fact]
        public async Task Delete_LastRow_RequestsNoRowAndShowsEmptyState()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "only");
            var requests = new List<KeycutRowViewModel?>();
            manager.FocusRowRequested += (s, row) => requests.Add(row);

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Null(requests.Single());
            Assert.Equal(ManagerState.Empty, manager.State);
            Assert.Equal(0, manager.KeycutCount);
        }

        [Fact]
        public async Task Delete_WhileSearching_KeepsFilterAndUpdatesBothCounts()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "alpha", "alpine", "beta");
            manager.SearchText = "al";

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Equal(new[] { "alpine" }, manager.Rows.Select(r => r.Name));
            Assert.Equal("1 keycut", manager.ResultCountText);
            Assert.Equal("2 items", manager.StatusText);
        }

        [Fact]
        public async Task Delete_Failure_KeepsRowAndShowsError()
        {
            var actions = new FakeActions
            {
                DeleteError = new KeycutRepositoryException(KeycutRepositoryError.InUse, "File is in use.")
            };
            var manager = await LoadedAsync(actions, "a", "b");
            var requests = 0;
            manager.FocusRowRequested += (s, row) => requests++;

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Equal(2, manager.Rows.Count);
            Assert.Equal(2, manager.KeycutCount);
            Assert.True(manager.StatusIsError);
            Assert.Contains("File is in use.", manager.StatusMessage);
            Assert.Equal(0, requests);
        }

        [Fact]
        public async Task Delete_AlreadyGone_RemovesRowWithErrorMessage()
        {
            var actions = new FakeActions
            {
                DeleteError = new KeycutRepositoryException(KeycutRepositoryError.NotFound, "gone")
            };
            var manager = await LoadedAsync(actions, "a", "b");

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Equal(new[] { "b" }, manager.Rows.Select(r => r.Name));
            Assert.True(manager.StatusIsError);
            Assert.Contains("already deleted", manager.StatusMessage);
        }

        [Fact]
        public async Task Delete_ClosesTheOpenMenuFlag()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            manager.IsMenuOpen = true;

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.False(manager.IsMenuOpen);
        }

        [Fact]
        public async Task StatusMessage_ClearsAfterTheDuration()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.StatusMessageDuration = TimeSpan.FromMilliseconds(30);

            manager.Rows[0].CopyDestinationCommand.Execute(null);
            Assert.Equal("Copied", manager.StatusMessage);

            for (var i = 0; i < 100 && manager.StatusMessage.Length > 0; i++)
            {
                await Task.Delay(20);
            }

            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task ActionsLabel_NamesTheRow()
        {
            var manager = await LoadedAsync(new FakeActions(), "l-vicki");

            Assert.Equal("Actions for l-vicki", manager.Rows[0].ActionsLabel);
        }

        [Fact]
        public async Task Shell_DeleteUpdatesTheCreatorCount()
        {
            var actions = new FakeActions();
            var keycuts = new List<Keycut> { NewKeycut("a"), NewKeycut("b") };
            var shell = new ShellViewModel(
                () => Task.FromResult(keycuts.Count),
                ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts),
                null,
                actions);
            shell.ShowManager();
            await shell.ReloadAsync();
            Assert.Equal(2, shell.Creator.KeycutCount);

            await shell.Manager.DeleteKeycutAsync(shell.Manager.Rows[0]);

            Assert.Equal(1, shell.Creator.KeycutCount);
        }

        #region Selection

        [Fact]
        public async Task MoveSelection_StartsAtTheFirstRowGoingDown_AndTheLastGoingUp()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b", "c");

            Assert.True(manager.MoveSelection(1));
            Assert.Equal("a", manager.SelectedRow?.Name);

            manager.SelectedRow = null;

            Assert.True(manager.MoveSelection(-1));
            Assert.Equal("c", manager.SelectedRow?.Name);
        }

        [Fact]
        public async Task MoveSelection_StopsAtBothEnds()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b");

            manager.MoveSelection(1);
            manager.MoveSelection(1);
            manager.MoveSelection(1);
            Assert.Equal("b", manager.SelectedRow?.Name);

            manager.MoveSelection(-1);
            manager.MoveSelection(-1);
            Assert.Equal("a", manager.SelectedRow?.Name);
        }

        [Fact]
        public async Task MoveSelection_WithNoRows_ReportsThatNothingMoved()
        {
            var manager = await LoadedAsync(new FakeActions());

            Assert.False(manager.MoveSelection(1));
            Assert.Null(manager.SelectedRow);
        }

        [Fact]
        public async Task OpenSelected_LaunchesTheSelectedRow()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            manager.MoveSelection(1);
            manager.MoveSelection(1);

            Assert.True(manager.OpenSelected());
            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Launched);
        }

        [Fact]
        public async Task OpenSelected_WithoutASelection_DoesNothing()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");

            Assert.False(manager.OpenSelected());
            Assert.Empty(actions.Launched);
        }

        [Fact]
        public async Task Selection_SurvivesASearchThatKeepsTheRow_AndClearsWhenItIsFilteredOut()
        {
            var manager = await LoadedAsync(new FakeActions(), "alpha", "bravo");
            manager.MoveSelection(1);

            manager.SearchText = "alp";
            Assert.Equal("alpha", manager.SelectedRow?.Name);

            manager.SearchText = "bra";
            Assert.Null(manager.SelectedRow);
        }

        [Fact]
        public async Task Selection_SurvivesSorting()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b");
            manager.MoveSelection(1);

            manager.SortBy(SortColumn.Shortcut);

            Assert.Equal(new[] { "b", "a" }, manager.Rows.Select(row => row.Name));
            Assert.Equal("a", manager.SelectedRow?.Name);
        }

        [Fact]
        public async Task Selection_FollowsTheKeycutThroughAReload()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b");
            manager.MoveSelection(1);
            manager.MoveSelection(1);

            manager.ShowKeycuts(new[] { NewKeycut("a"), NewKeycut("b"), NewKeycut("c") });

            Assert.Equal("b", manager.SelectedRow?.Name);
            Assert.Contains(manager.SelectedRow, manager.Rows);
        }

        [Fact]
        public async Task DeletingTheSelectedRow_SelectsTheNextOne()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b", "c");
            manager.MoveSelection(1);

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Equal("b", manager.SelectedRow?.Name);
        }

        [Fact]
        public async Task DeletingAnotherRow_KeepsTheSelection()
        {
            var manager = await LoadedAsync(new FakeActions(), "a", "b", "c");
            manager.MoveSelection(-1);

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Equal("c", manager.SelectedRow?.Name);
        }

        #endregion Selection
    }
}
