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

            public List<string> Located { get; } = new List<string>();

            public Exception? LocateError { get; set; }

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

            public void OpenFileLocation(string path)
            {
                if (LocateError != null)
                {
                    throw LocateError;
                }

                Located.Add(path);
            }

            public void CopyText(string text)
            {
                if (CopyError != null)
                {
                    throw CopyError;
                }

                Copied.Add(text);
            }

            /// <summary>Whether the fake says the Edit form can save a keycut (its file is one the emitter writes identically).</summary>
            public bool Editable { get; set; } = true;

            public Exception? CanEditError { get; set; }

            public Task<bool> CanEditInFormAsync(Keycut keycut)
            {
                return CanEditError != null ? Task.FromException<bool>(CanEditError) : Task.FromResult(Editable);
            }

            public List<(Keycut Keycut, KeycutEdit Edit)> Updated { get; } = new List<(Keycut, KeycutEdit)>();

            public Exception? UpdateError { get; set; }

            public Task<Keycut> UpdateAsync(Keycut keycut, KeycutEdit edit)
            {
                if (UpdateError != null)
                {
                    return Task.FromException<Keycut>(UpdateError);
                }

                Updated.Add((keycut, edit));
                var saved = keycut with
                {
                    Name = edit.Name,
                    Target = edit.Target,
                    Description = edit.Description,
                    FilePath = @"C:\fixture\" + edit.Name + ".bat",
                    Modified = new DateTime(2026, 6, 7, 8, 9, 10)
                };

                // What a loader reads next time, so the reload after an edit sees the saved keycut as the folder would
                if (Disk != null)
                {
                    Disk[Disk.FindIndex(k => k.FilePath == keycut.FilePath)] = saved;
                    Disk.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.FilePath, y.FilePath));
                }

                return Task.FromResult(saved);
            }

            /// <summary>The keycuts "on disk", for <see cref="LoadedOnDiskAsync"/>; an update replaces its entry.</summary>
            public List<Keycut>? Disk { get; set; }

            /// <summary>What the fake reports for a delete: false is a drive without a Recycle Bin.</summary>
            public bool Recycled { get; set; } = true;

            /// <summary>When set, a delete waits for this before it finishes.</summary>
            public Task? DeleteGate { get; set; }

            public async Task<DeletedKeycut> DeleteAsync(Keycut keycut)
            {
                if (DeleteGate != null)
                {
                    await DeleteGate;
                }

                if (DeleteError != null)
                {
                    throw DeleteError;
                }

                Deleted.Add(keycut.FilePath);
                return new DeletedKeycut(keycut, new byte[] { 1, 2, 3 }, new DateTime(2026, 1, 2, 3, 4, 5), Recycled);
            }

            public List<DeletedKeycut> Restored { get; } = new List<DeletedKeycut>();

            public Exception? RestoreError { get; set; }

            /// <summary>When set, a restore waits for this before it finishes.</summary>
            public Task? RestoreGate { get; set; }

            public async Task RestoreAsync(DeletedKeycut deleted)
            {
                if (RestoreGate != null)
                {
                    await RestoreGate;
                }

                if (RestoreError != null)
                {
                    throw RestoreError;
                }

                Restored.Add(deleted);
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

        static Keycut NewKeycut(string name, string target = @"C:\temp\a.txt")
        {
            return new Keycut
            {
                Name = name,
                Kind = KeycutKind.File,
                RawType = ShortcutType.File,
                Description = string.Empty,
                Target = target,
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

        /// <summary>Like <see cref="LoadedAsync"/>, but every load reads <see cref="FakeActions.Disk"/>, which saving an edit changes.</summary>
        static async Task<ManagerViewModel> LoadedOnDiskAsync(FakeActions actions, params string[] names)
        {
            var noop = new RelayCommand(() => { });
            actions.Disk = names.Select(n => NewKeycut(n)).ToList();
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(actions.Disk.ToList()), null, actions)
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

            manager.Rows[0].EditInNotepadCommand.Execute(null);

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

            manager.Rows[0].EditInNotepadCommand.Execute(null);

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

            manager.Rows[0].EditInNotepadCommand.Execute(null);

            Assert.True(manager.StatusIsError);
            Assert.Contains("no notepad", manager.StatusMessage);
        }

        [Fact]
        public async Task OpenFileLocation_ShowsTheKeycutFile()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");

            manager.Rows[1].OpenFileLocationCommand.Execute(null);

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Located);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task OpenFileLocation_FileGone_ShowsMessage_AndRefreshesTheList()
        {
            var noop = new RelayCommand(() => { });
            var actions = new FakeActions { LocateError = new System.IO.FileNotFoundException("gone") };
            var current = new List<Keycut> { NewKeycut("a"), NewKeycut("b") };
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(current.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            current.RemoveAt(0);

            manager.Rows[0].OpenFileLocationCommand.Execute(null);

            Assert.Equal("This keycut no longer exists", manager.StatusMessage);
            Assert.True(manager.StatusIsError);
            await Task.Delay(200);
            Assert.Equal(new[] { "b" }, manager.Rows.Select(r => r.Name).ToArray());
        }

        [Fact]
        public async Task OpenFileLocation_OtherFailure_ShowsErrorInStatus()
        {
            var actions = new FakeActions { LocateError = new InvalidOperationException("no explorer") };
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].OpenFileLocationCommand.Execute(null);

            Assert.True(manager.StatusIsError);
            Assert.Contains("no explorer", manager.StatusMessage);
            Assert.Contains("file location", manager.StatusMessage);
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
        public async Task Rename_ToANameAnotherCommandUses_Asks_AndNoKeepsEditingTheTypedName()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.CheckRename = name => TestNameChecks.Conflict(name);
            var asked = new List<string>();
            manager.ConfirmRename = check => { asked.Add(check.Name); return false; };
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = " winword ";

            await manager.CommitRenameAsync();

            Assert.Equal(new[] { "winword" }, asked);
            Assert.Empty(actions.Renamed);
            Assert.True(manager.IsRenaming);
            Assert.True(manager.Rows[0].IsRenaming);
            Assert.Equal("winword", manager.Rows[0].EditName);
        }

        [Fact]
        public async Task Rename_ToANameAnotherCommandUses_RenamesAfterAYes()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.CheckRename = name => TestNameChecks.Conflict(name);
            manager.ConfirmRename = check => true;
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "winword";

            await manager.CommitRenameAsync();

            Assert.Equal(new[] { (@"C:\fixture\a.bat", "winword") }, actions.Renamed);
            Assert.False(manager.IsRenaming);
        }

        [Fact]
        public async Task Rename_WithNoWayToAsk_DoesNotRenameOverAConflict()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.CheckRename = name => TestNameChecks.Conflict(name);
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "winword";

            await manager.CommitRenameAsync();

            Assert.Empty(actions.Renamed);
            Assert.True(manager.IsRenaming);
        }

        [Fact]
        public async Task Rename_ToAFreeName_DoesNotAsk()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.CheckRename = name => NameCheck.None(name);
            manager.ConfirmRename = check => throw new InvalidOperationException("should not ask");
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "b";

            await manager.CommitRenameAsync();

            Assert.Equal(new[] { (@"C:\fixture\a.bat", "b") }, actions.Renamed);
        }

        [Fact]
        public async Task Rename_ToAnInvalidName_IsNotChecked_AndFailsAsBefore()
        {
            var actions = new FakeActions { RenameError = new KeycutRepositoryException(KeycutRepositoryError.InvalidName, "bad") };
            var manager = await LoadedAsync(actions, "a");
            var checks = 0;
            manager.CheckRename = name => { checks++; return TestNameChecks.Conflict(name); };
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "a/b";

            await manager.CommitRenameAsync();

            Assert.Equal(0, checks);
            Assert.True(manager.StatusIsError);
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
        public async Task Rename_Failure_ShowsTheFileSystemsReason_NotJustTheFileName()
        {
            var locked = new System.IO.IOException("The process cannot access the file because it is being used by another process.");
            var actions = new FakeActions { RenameError = new KeycutRepositoryException(KeycutRepositoryError.InUse, "Can't rename \"a.bat\".", locked) };
            var manager = await LoadedAsync(actions, "a");
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "z";

            await manager.CommitRenameAsync();

            Assert.Equal("Couldn't rename “a”: " + locked.Message, manager.StatusMessage);
        }

        [Fact]
        public async Task Rename_BeforeTheReloadRebuildsTheRows_TheOldRowsActionsUseTheRenamedFile()
        {
            var noop = new RelayCommand(() => { });
            var reload = new TaskCompletionSource<IReadOnlyList<Keycut>>();
            var calls = 0;
            var actions = new FakeActions();
            var manager = new ManagerViewModel(noop, noop, ct => ++calls == 1 ?
                Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a") }) :
                reload.Task, null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            var row = manager.Rows[0];
            manager.BeginRename(row);
            row.EditName = "zed";

            await manager.CommitRenameAsync();   // its reload waits, so the row still shows "a"
            var newPath = @"C:\fixture\zed.bat";

            manager.OpenKeycut(row);
            manager.EditInNotepad(row);
            manager.OpenFileLocation(row);
            await manager.DeleteKeycutAsync(row);

            Assert.Equal(new[] { newPath }, actions.Launched);
            Assert.Equal(new[] { newPath }, actions.Edited);
            Assert.Equal(new[] { newPath }, actions.Located);
            Assert.Equal(new[] { newPath }, actions.Deleted);
            Assert.Equal("Deleted “a”", manager.StatusMessage);

            reload.SetResult(new List<Keycut>());
        }

        [Fact]
        public async Task Rename_WhileItsSaving_TheRowsFileActionsWait()
        {
            var gate = new TaskCompletionSource();
            var actions = new FakeActions { RenameGate = gate.Task };
            var manager = await LoadedAsync(actions, "a", "b");
            var row = manager.Rows[0];
            manager.BeginRename(row);
            row.EditName = "zed";

            var saving = manager.CommitRenameAsync();   // the file is being moved
            manager.OpenKeycut(row);
            manager.EditInNotepad(row);
            manager.OpenFileLocation(row);
            await manager.DeleteKeycutAsync(row);
            manager.OpenKeycut(manager.Rows[1]);       // other rows aren't affected

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Launched);
            Assert.Empty(actions.Edited);
            Assert.Empty(actions.Located);
            Assert.Empty(actions.Deleted);

            gate.SetResult();
            await saving;
        }

        [Fact]
        public async Task Rename_WhenAReloadRebuiltTheRowsDuringTheSave_TheRebuiltRowUsesTheRenamedFile()
        {
            var noop = new RelayCommand(() => { });
            var gate = new TaskCompletionSource();
            var afterSave = new TaskCompletionSource<IReadOnlyList<Keycut>>();
            var calls = 0;
            var actions = new FakeActions { RenameGate = gate.Task };
            var manager = new ManagerViewModel(noop, noop, ct =>
            {
                calls++;
                return calls switch
                {
                    1 => Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a") }),
                    2 => Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a"), NewKeycut("b") }),
                    _ => afterSave.Task,
                };
            }, null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "zed";

            var saving = manager.CommitRenameAsync();
            await manager.LoadAsync();                  // F5 during the save rebuilds the rows, still with "a"
            gate.SetResult();
            await saving;                               // the reload after it waits

            var rebuilt = manager.Rows.Single(r => r.Name == "a");
            manager.OpenKeycut(rebuilt);

            Assert.Equal(new[] { @"C:\fixture\zed.bat" }, actions.Launched);

            afterSave.SetResult(new List<Keycut> { NewKeycut("b"), NewKeycut("zed") });
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
        public async Task Rename_RetryWorksRightAway_EvenWhileTargetChecksAreSlow()
        {
            var noop = new RelayCommand(() => { });
            var checks = 0;
            var neverDone = new TaskCompletionSource();
            var actions = new FakeActions { RenameError = new KeycutRepositoryException(KeycutRepositoryError.InvalidName, "bad") };
            var manager = new ManagerViewModel(noop, noop,
                ct => Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a") }),
                (keycuts, ct) => ++checks == 1 ? Task.CompletedTask : neverDone.Task,
                actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "bad/name";

            await manager.CommitRenameAsync();   // the reload after it hangs in the target check

            manager.BeginRename(manager.Rows[0]);
            Assert.True(manager.IsRenaming);
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
        public async Task Rename_WhenItsReloadIsCancelled_TheNextReloadSelectsTheRenamedRow()
        {
            var noop = new RelayCommand(() => { });
            var renameReload = new TaskCompletionSource<IReadOnlyList<Keycut>>();
            var f5Reload = new TaskCompletionSource<IReadOnlyList<Keycut>>();
            var calls = 0;
            var manager = new ManagerViewModel(noop, noop, ct =>
            {
                calls++;
                return calls switch
                {
                    1 => Task.FromResult<IReadOnlyList<Keycut>>(new List<Keycut> { NewKeycut("a"), NewKeycut("b") }),
                    2 => renameReload.Task,
                    _ => f5Reload.Task,
                };
            }, null, new FakeActions())
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            KeycutRowViewModel? focused = null;
            manager.FocusRowRequested += (sender, row) => focused = row;
            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "zed";

            var saving = manager.CommitRenameAsync();   // its reload waits
            var f5 = manager.LoadAsync();               // F5 meanwhile cancels it, and waits too
            renameReload.SetResult(new List<Keycut> { NewKeycut("b"), NewKeycut("zed") });
            await saving;                               // the cancelled reload returns first, changing nothing

            // The rows still show "a" with its old path, so no rename may start from them yet
            Assert.Equal("a", manager.Rows[0].Name);
            manager.BeginRename(manager.Rows[0]);
            Assert.False(manager.IsRenaming);

            f5Reload.SetResult(new List<Keycut> { NewKeycut("b"), NewKeycut("zed") });
            await f5;

            Assert.Equal("zed", manager.SelectedRow?.Name);
            Assert.Same(manager.SelectedRow, focused);
            Assert.Equal(new[] { "b", "zed" }, manager.Rows.Select(r => r.Name).ToArray());

            // Fresh rows: renaming works again
            manager.BeginRename(manager.Rows[1]);
            Assert.True(manager.IsRenaming);
        }

        [Fact]
        public void MissingIndicator_IsHiddenWhileRenaming()
        {
            var keycut = NewKeycut("a");
            keycut.TargetStatus = TargetStatus.Missing;
            var manager = new ManagerViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), null, null, new FakeActions());
            var row = new KeycutRowViewModel(keycut, manager);
            var changed = new List<string?>();
            row.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            Assert.True(row.ShowMissingIndicator);

            manager.BeginRename(row);
            Assert.False(row.ShowMissingIndicator);
            Assert.Contains(nameof(KeycutRowViewModel.ShowMissingIndicator), changed);

            manager.CancelRename();
            Assert.True(row.ShowMissingIndicator);
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
                    Target = @"C:\temp\a.txt",
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
        public async Task CopyTarget_CopiesAndConfirms()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].CopyTargetCommand.Execute(null);

            Assert.Equal(new[] { @"C:\temp\a.txt" }, actions.Copied);
            Assert.Equal("Copied", manager.StatusMessage);
            Assert.False(manager.StatusIsError);
        }

        [Fact]
        public async Task CopyTarget_ClipboardLocked_ShowsError()
        {
            var actions = new FakeActions { CopyError = new InvalidOperationException("clipboard busy") };
            var manager = await LoadedAsync(actions, "a");

            manager.Rows[0].CopyTargetCommand.Execute(null);

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

        #region Edit form (#180)

        /// <summary>A form that saves what it's given: the dialog the view would show, without a window.</summary>
        static Func<EditKeycutViewModel, bool> Saving(Action<EditKeycutViewModel> fill, List<EditKeycutViewModel>? shown = null)
        {
            return form =>
            {
                shown?.Add(form);
                fill(form);
                form.SaveAsync().GetAwaiter().GetResult();
                return form.Saved != null;
            };
        }

        [Fact]
        public async Task Edit_OpensTheFormFilledInFromTheKeycut()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            var shown = new List<EditKeycutViewModel>();
            manager.ShowEditForm = form => { shown.Add(form); return false; };

            await manager.EditKeycutAsync(manager.Rows[0]);

            var form = Assert.Single(shown);
            Assert.Equal("a", form.Name);
            Assert.Equal(@"C:\temp\a.txt", form.Target);
            Assert.Equal(string.Empty, form.Description);
            Assert.Equal(@"C:\fixture\a.bat", form.Original.FilePath);
        }

        [Fact]
        public async Task Edit_TheRowCommandOpensTheForm()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            var shown = 0;
            manager.ShowEditForm = form => { shown++; return false; };

            manager.Rows[0].EditCommand.Execute(null);
            await WaitFor(() => shown == 1);

            Assert.Empty(actions.Edited);
        }

        [Fact]
        public async Task Edit_Saving_ReplacesTheRow_SelectsItAndFocusesIt_AndSaysSo()
        {
            var actions = new FakeActions();
            var manager = await LoadedOnDiskAsync(actions, "a", "b", "c");
            var requests = new List<KeycutRowViewModel?>();
            manager.FocusRowRequested += (s, row) => requests.Add(row);
            manager.ShowEditForm = Saving(form =>
            {
                form.Target = "https://example.com/new";
                form.Description = "Changed";
            });

            await manager.EditKeycutAsync(manager.Rows[1]);

            var edit = Assert.Single(actions.Updated);
            Assert.Equal(@"C:\fixture\b.bat", edit.Keycut.FilePath);
            Assert.Equal(new KeycutEdit("https://example.com/new", "b", "Changed"), edit.Edit);
            Assert.Equal(new[] { "a", "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(3, manager.KeycutCount);
            var row = manager.Rows[1];
            Assert.Equal("https://example.com/new", row.Target);
            Assert.Equal("Changed", row.Description);
            Assert.Same(row, manager.SelectedRow);
            Assert.Same(row, requests.Single());
            Assert.Equal("Saved \u201Cb\u201D", manager.StatusMessage);
            Assert.False(manager.StatusIsError);

            // The reload that follows the dialog finds the folder as the manager now shows it, so it keeps this row
            // (and the focus that was just sent to it) instead of building another
            await manager.LoadAsync();
            Assert.Same(row, manager.Rows[1]);
        }

        [Fact]
        public async Task Edit_SavingANewName_MovesTheRowToWhereTheSortPutsIt()
        {
            var actions = new FakeActions();
            var manager = await LoadedOnDiskAsync(actions, "a", "b", "c");
            manager.ShowEditForm = Saving(form => form.Name = "z");

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(new[] { "b", "c", "z" }, manager.Rows.Select(r => r.Name));
            Assert.Equal("z", manager.SelectedRow!.Name);
            Assert.Equal(@"C:\fixture\z.bat", manager.SelectedRow.Keycut.FilePath);
            Assert.Equal("Saved \u201Cz\u201D", manager.StatusMessage);
            Assert.Equal(3, manager.KeycutCount);
        }

        [Fact]
        public async Task Edit_TheSavedRowShowsTheNewModifiedDate()
        {
            var actions = new FakeActions();
            var manager = await LoadedOnDiskAsync(actions, "a");
            manager.ShowEditForm = Saving(form => form.Description = "x");

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(new DateTime(2026, 6, 7, 8, 9, 10), manager.Rows[0].Modified);
        }

        [Fact]
        public async Task Edit_ASearchThatNoLongerMatchesTheSavedKeycut_IsCleared()
        {
            var actions = new FakeActions();
            var manager = await LoadedOnDiskAsync(actions, "alpha", "beta");
            manager.SearchText = "alp";
            manager.ShowEditForm = Saving(form => form.Name = "gamma");

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(string.Empty, manager.SearchText);
            Assert.Equal("gamma", manager.SelectedRow!.Name);
            Assert.Equal(new[] { "beta", "gamma" }, manager.Rows.Select(r => r.Name));
        }

        [Fact]
        public async Task Edit_Cancelling_ChangesNothing_AndSaysNothing()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            var before = manager.Rows.ToArray();
            manager.ShowEditForm = form => false;

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.True(before.SequenceEqual(manager.Rows));
            Assert.Empty(actions.Updated);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task Edit_ClosingTheFormWithNothingChanged_ChangesNothing()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.ShowEditForm = Saving(form => { });

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Empty(actions.Updated);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task Edit_AKeycutTheFormCantSave_SaysToUseNotepad_AndShowsNoForm()
        {
            var actions = new FakeActions { Editable = false };
            var manager = await LoadedAsync(actions, "a");
            var shown = 0;
            manager.ShowEditForm = form => { shown++; return false; };

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(0, shown);
            Assert.True(manager.StatusIsError);
            Assert.Equal("This keycut can\u2019t be edited here. Edit it in Notepad.", manager.StatusMessage);
            Assert.Empty(actions.Edited);
        }

        [Fact]
        public async Task Edit_AKeycutThatIsGone_SaysSo_AndReloads()
        {
            var actions = new FakeActions { CanEditError = new KeycutRepositoryException(KeycutRepositoryError.NotFound, "gone") };
            var loads = 0;
            var noop = new RelayCommand(() => { });
            var keycuts = new List<Keycut> { NewKeycut("a") };
            var manager = new ManagerViewModel(noop, noop, ct => { loads++; return Task.FromResult<IReadOnlyList<Keycut>>(keycuts.ToList()); }, null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            manager.ShowEditForm = form => false;
            keycuts.Clear();

            await manager.EditKeycutAsync(manager.Rows[0]);
            await WaitFor(() => loads == 2);

            Assert.True(manager.StatusIsError);
            Assert.Equal("This keycut no longer exists", manager.StatusMessage);
            await WaitFor(() => manager.Rows.Count == 0);
        }

        [Fact]
        public async Task Edit_AFileThatCantBeChecked_SaysWhy()
        {
            var actions = new FakeActions { CanEditError = new UnauthorizedAccessException("Access to the path is denied.") };
            var manager = await LoadedAsync(actions, "a");
            manager.ShowEditForm = form => false;

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.True(manager.StatusIsError);
            Assert.Equal("Couldn\u2019t edit \u201Ca\u201D: Access to the path is denied.", manager.StatusMessage);
        }

        [Fact]
        public async Task Edit_WithoutAWayToShowTheForm_DoesNothing()
        {
            var actions = new FakeActions { CanEditError = new InvalidOperationException("must not be asked") };
            var manager = await LoadedAsync(actions, "a");

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task Edit_ANewNameAsksAboutConflictsThroughTheManagersOwnChecks()
        {
            var actions = new FakeActions();
            var manager = await LoadedOnDiskAsync(actions, "a");
            var asked = new List<string>();
            manager.CheckRename = name => TestNameChecks.Conflict(name, NameConflictEffect.Hidden, @"C:\Windows\" + name + ".exe");
            manager.ConfirmRename = check => { asked.Add(check.Name); return true; };
            manager.ShowEditForm = Saving(form => form.Name = "notepad");

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(new[] { "notepad" }, asked);
            Assert.Equal("notepad", actions.Updated.Single().Edit.Name);
        }

        [Fact]
        public async Task Edit_ADeclinedConflict_SavesNothing()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            manager.CheckRename = name => TestNameChecks.Conflict(name, NameConflictEffect.Hidden, @"C:\Windows\" + name + ".exe");
            manager.ConfirmRename = check => false;
            manager.ShowEditForm = Saving(form => form.Name = "notepad");

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Empty(actions.Updated);
            Assert.Equal("a", manager.Rows.Single().Name);
        }

        [Fact]
        public async Task Edit_AReloadThatAlreadyFoundTheSavedFile_LeavesOneRowForIt()
        {
            var actions = new FakeActions();
            var onDisk = new List<Keycut> { NewKeycut("a"), NewKeycut("b") };
            var noop = new RelayCommand(() => { });
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(onDisk.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            manager.ShowEditForm = form =>
            {
                form.Name = "c";
                form.SaveAsync().GetAwaiter().GetResult();

                // The window was activated while the form was open: the folder now holds c, not a
                onDisk[0] = form.Saved!;
                manager.LoadAsync().GetAwaiter().GetResult();
                return true;
            };

            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(new[] { "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(2, manager.KeycutCount);
            Assert.Equal("c", manager.SelectedRow!.Name);
        }

        [Fact]
        public async Task EditInNotepad_StaysAsASeparateAction_AndNeverShowsTheForm()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");
            var shown = 0;
            manager.ShowEditForm = form => { shown++; return false; };

            manager.Rows[0].EditInNotepadCommand.Execute(null);

            Assert.Equal(new[] { @"C:\fixture\a.bat" }, actions.Edited);
            Assert.Equal(0, shown);
        }

        #endregion Edit form (#180)

        #region Edit when the form can't (#202)

        static async Task<ManagerViewModel> LoadedKeycutsAsync(FakeActions actions, params Keycut[] keycuts)
        {
            var noop = new RelayCommand(() => { });
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            return manager;
        }

        [Fact]
        public async Task EditCommand_CannotRun_ForAKeycutTheFormCantEdit()
        {
            var manager = await LoadedKeycutsAsync(new FakeActions(), NewKeycut("fine"), NewKeycut("handmade") with { CanEditInForm = false });

            var fine = manager.Rows.Single(r => r.Name == "fine");
            var handmade = manager.Rows.Single(r => r.Name == "handmade");

            Assert.True(fine.EditCommand.CanExecute(null));
            Assert.True(fine.CanEditInForm);
            Assert.Equal(string.Empty, fine.EditTooltip);
            Assert.False(handmade.EditCommand.CanExecute(null));
            Assert.False(handmade.CanEditInForm);
            Assert.Equal(EditKeycutViewModel.NotEditableMessage, handmade.EditTooltip);

            // Edit in Notepad is how these are edited, so it stays available on every row
            Assert.True(handmade.EditInNotepadCommand.CanExecute(null));
        }

        [Fact]
        public async Task Edit_AKeycutKnownNotToBeEditable_SaysSoAtOnce_WithoutAskingTheFileOrShowingTheForm()
        {
            var actions = new FakeActions { CanEditError = new InvalidOperationException("must not be asked") };
            var manager = await LoadedKeycutsAsync(actions, NewKeycut("handmade") with { CanEditInForm = false });
            var shown = 0;
            manager.ShowEditForm = form => { shown++; return false; };

            // Ctrl+E goes here, not through the command: a key press that does nothing would be worse than a message
            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(0, shown);
            Assert.True(manager.StatusIsError);
            Assert.Equal(EditKeycutViewModel.NotEditableMessage, manager.StatusMessage);
        }

        [Fact]
        public async Task Edit_TheMenuItemOfANotEditableKeycut_DoesNothingWhenRun()
        {
            var manager = await LoadedKeycutsAsync(new FakeActions(), NewKeycut("handmade") with { CanEditInForm = false });
            var shown = 0;
            manager.ShowEditForm = form => { shown++; return false; };

            manager.Rows[0].EditCommand.Execute(null);

            Assert.Equal(0, shown);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        // The file can change after the list loaded (Notepad); the check when Edit is chosen is the one that counts
        [Fact]
        public async Task Edit_AFileThatChangedAfterTheLoad_StillEndsInTheMessage_NotAForm()
        {
            var actions = new FakeActions { Editable = false };
            var manager = await LoadedKeycutsAsync(actions, NewKeycut("fine"));
            var shown = 0;
            manager.ShowEditForm = form => { shown++; return false; };

            Assert.True(manager.Rows[0].EditCommand.CanExecute(null));
            await manager.EditKeycutAsync(manager.Rows[0]);

            Assert.Equal(0, shown);
            Assert.Equal(EditKeycutViewModel.NotEditableMessage, manager.StatusMessage);
        }

        [Fact]
        public async Task TheNameCanStillBeChanged_OnAKeycutTheFormCantEdit()
        {
            var actions = new FakeActions();
            var manager = await LoadedKeycutsAsync(actions, NewKeycut("handmade") with { CanEditInForm = false });

            manager.BeginRename(manager.Rows[0]);
            manager.Rows[0].EditName = "renamed";
            await manager.CommitRenameAsync();

            Assert.Equal(new[] { (@"C:\fixture\handmade.bat", "renamed") }, actions.Renamed);
        }

        #endregion Edit when the form can't (#202)

        #region Undo (#179)

        [Fact]
        public async Task Delete_OffersUndo_WhileTheMessageShows()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b", "c");
            var changed = new List<string?>();
            manager.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
            Assert.False(manager.CanUndoDelete);

            await manager.DeleteKeycutAsync(manager.Rows[1]);

            Assert.True(manager.CanUndoDelete);
            Assert.Contains(nameof(ManagerViewModel.CanUndoDelete), changed);
            Assert.Equal("Deleted \u201Cb\u201D", manager.StatusMessage);
        }

        [Fact]
        public async Task Undo_PutsTheKeycutBackWhereTheSortPutsIt_SelectedAndFocused()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b", "c");
            var restored = 0;
            var requests = new List<KeycutRowViewModel?>();
            manager.KeycutRestored += (s, e) => restored++;
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            manager.FocusRowRequested += (s, row) => requests.Add(row);

            await manager.UndoDeleteAsync();

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Restored.Select(d => d.Keycut.FilePath));
            Assert.Equal(new[] { "a", "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(3, manager.KeycutCount);
            Assert.Equal("3 items", manager.StatusText);
            Assert.Equal("b", manager.SelectedRow!.Name);
            Assert.Equal("b", requests.Single()!.Name);
            Assert.Equal("Restored \u201Cb\u201D", manager.StatusMessage);
            Assert.False(manager.StatusIsError);
            Assert.False(manager.CanUndoDelete);
            Assert.Equal(1, restored);
        }

        [Fact]
        public async Task Undo_RestoresTheLastKeycutOutOfTheEmptyState_WithItsOldDate()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "only");
            await manager.DeleteKeycutAsync(manager.Rows[0]);
            Assert.Equal(ManagerState.Empty, manager.State);

            await manager.UndoDeleteAsync();

            Assert.Equal(ManagerState.Populated, manager.State);
            Assert.Equal("only", manager.Rows.Single().Name);
            Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), manager.Rows[0].Modified);
        }

        [Fact]
        public async Task Undo_ASearchThatHidesTheKeycut_IsCleared()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "alpha", "beta");
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            manager.SearchText = "alpha";

            await manager.UndoDeleteAsync();

            Assert.Equal(string.Empty, manager.SearchText);
            Assert.Equal(new[] { "alpha", "beta" }, manager.Rows.Select(r => r.Name));
            Assert.Equal("beta", manager.SelectedRow!.Name);
        }

        [Fact]
        public async Task Undo_ANameTakenAgain_IsRefusedAndSaidSo_NothingIsAdded_AndItCanBeRetried()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            actions.RestoreError = new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, "\"b\" already exists.");

            await manager.UndoDeleteAsync();

            Assert.Equal(new[] { "a" }, manager.Rows.Select(r => r.Name));
            Assert.True(manager.StatusIsError);
            Assert.Equal("Can\u2019t undo: \u201Cb\u201D already exists.", manager.StatusMessage);

            // The other keycut is renamed or deleted, and the Undo is still there: on a drive without a bin it's the only copy
            Assert.True(manager.CanUndoDelete);
            actions.RestoreError = null;

            await manager.UndoDeleteAsync();

            Assert.Equal(new[] { "a", "b" }, manager.Rows.Select(r => r.Name));
            Assert.Equal("Restored \u201Cb\u201D", manager.StatusMessage);
            Assert.False(manager.CanUndoDelete);
        }

        [Theory]
        [InlineData(true, " It\u2019s still in the Recycle Bin.")]
        [InlineData(false, "")]
        public async Task Undo_AnotherFailure_SaysWhy_AndPointsToTheRecycleBinWhenItIsThere(bool recycled, string hint)
        {
            var actions = new FakeActions { Recycled = recycled };
            var manager = await LoadedAsync(actions, "a", "b");
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            actions.RestoreError = new System.IO.IOException("disk is full");

            await manager.UndoDeleteAsync();

            Assert.Equal(new[] { "a" }, manager.Rows.Select(r => r.Name));
            Assert.True(manager.StatusIsError);
            Assert.Equal("Couldn\u2019t restore \u201Cb\u201D: disk is full" + hint, manager.StatusMessage);

            // Kept for another try, so the disk can be fixed first
            Assert.True(manager.CanUndoDelete);
        }

        [Fact]
        public async Task Undo_AFailedRestoreStaysOnOffer_ForAsLongAsUndoDoes_ThenGoes()
        {
            var actions = new FakeActions { Recycled = false };
            var manager = await LoadedAsync(actions, "a", "b");
            manager.UndoDuration = TimeSpan.FromMilliseconds(80);
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            actions.RestoreError = new System.IO.IOException("disk is full");

            await manager.UndoDeleteAsync();
            Assert.True(manager.CanUndoDelete);

            await WaitFor(() => !manager.CanUndoDelete);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task Undo_ADeleteWhileARestoreRuns_IsRefused_SoTheNewerDeleteIsNotLost()
        {
            var gate = new TaskCompletionSource<bool>();
            var actions = new FakeActions { RestoreGate = gate.Task };
            var manager = await LoadedAsync(actions, "a", "b", "c");
            await manager.DeleteKeycutAsync(manager.Rows[1]);

            var restoring = manager.UndoDeleteAsync();
            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Deleted);
            Assert.Equal("Still restoring \u201Cb\u201D", manager.StatusMessage);
            Assert.Equal(new[] { "a", "c" }, manager.Rows.Select(r => r.Name));

            gate.SetResult(true);
            await restoring;

            Assert.Equal(new[] { "a", "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal("Restored \u201Cb\u201D", manager.StatusMessage);
        }

        [Fact]
        public async Task Undo_WhileADeleteIsStillRunning_DoesNothing_AndTheNewerDeleteIsTheOneOffered()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b", "c");
            await manager.DeleteKeycutAsync(manager.Rows[2]);

            var gate = new TaskCompletionSource<bool>();
            actions.DeleteGate = gate.Task;
            var deleting = manager.DeleteKeycutAsync(manager.Rows[1]);

            await manager.UndoDeleteAsync();
            Assert.Empty(actions.Restored);

            gate.SetResult(true);
            await deleting;

            Assert.True(manager.CanUndoDelete);
            Assert.Equal("Deleted \u201Cb\u201D", manager.StatusMessage);
        }

        [Fact]
        public async Task Undo_WithNothingToUndo_DoesNothing()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a");

            await manager.UndoDeleteAsync();
            manager.UndoDeleteCommand.Execute(null);

            Assert.Empty(actions.Restored);
            Assert.Equal(string.Empty, manager.StatusMessage);
        }

        [Fact]
        public async Task Undo_IsUsedOnce()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            await manager.DeleteKeycutAsync(manager.Rows[1]);

            await manager.UndoDeleteAsync();
            await manager.UndoDeleteAsync();

            Assert.Single(actions.Restored);
            Assert.Equal(2, manager.Rows.Count);
        }

        [Fact]
        public async Task Undo_TheCommandDoesItToo()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            await manager.DeleteKeycutAsync(manager.Rows[1]);

            manager.UndoDeleteCommand.Execute(null);
            await WaitFor(() => manager.Rows.Count == 2);

            Assert.Single(actions.Restored);
        }

        [Fact]
        public async Task Undo_ExpiresWithTheMessage()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            manager.UndoDuration = TimeSpan.FromMilliseconds(50);

            await manager.DeleteKeycutAsync(manager.Rows[1]);
            Assert.True(manager.CanUndoDelete);
            await WaitFor(() => !manager.CanUndoDelete);

            Assert.Equal(string.Empty, manager.StatusMessage);

            await manager.UndoDeleteAsync();

            Assert.Empty(actions.Restored);
        }

        [Fact]
        public async Task Undo_IsGoneOnceAnotherMessageReplacesTheDeletedOne()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b");
            await manager.DeleteKeycutAsync(manager.Rows[1]);

            manager.CopyTarget(manager.Rows[0]);

            Assert.Equal("Copied", manager.StatusMessage);
            Assert.False(manager.CanUndoDelete);
        }

        [Fact]
        public async Task ASecondDelete_ReplacesTheUndo_SoOnlyTheLatestComesBack()
        {
            var actions = new FakeActions();
            var manager = await LoadedAsync(actions, "a", "b", "c");
            await manager.DeleteKeycutAsync(manager.Rows[0]);
            await manager.DeleteKeycutAsync(manager.Rows[0]);

            await manager.UndoDeleteAsync();

            Assert.Equal(new[] { @"C:\fixture\b.bat" }, actions.Restored.Select(d => d.Keycut.FilePath));
            Assert.Equal(new[] { "b", "c" }, manager.Rows.Select(r => r.Name));
        }

        [Fact]
        public async Task Delete_OnADriveWithoutARecycleBin_SaysSo_AndStillOffersUndo()
        {
            var actions = new FakeActions { Recycled = false };
            var manager = await LoadedAsync(actions, "a", "b");

            await manager.DeleteKeycutAsync(manager.Rows[1]);

            Assert.Equal("Deleted \u201Cb\u201D (this drive has no Recycle Bin)", manager.StatusMessage);
            Assert.True(manager.CanUndoDelete);
        }

        [Fact]
        public async Task Delete_AFailureOrAnAlreadyGoneFile_OffersNoUndo()
        {
            var actions = new FakeActions { DeleteError = new KeycutRepositoryException(KeycutRepositoryError.NotFound, "gone") };
            var manager = await LoadedAsync(actions, "a", "b");

            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.False(manager.CanUndoDelete);

            actions.DeleteError = new KeycutRepositoryException(KeycutRepositoryError.InUse, "in use");
            await manager.DeleteKeycutAsync(manager.Rows[0]);

            Assert.False(manager.CanUndoDelete);
        }

        [Fact]
        public async Task Undo_AReloadThatFindsTheFileBeforeTheRestoreFinishes_DoesNotMakeASecondRow()
        {
            var gate = new TaskCompletionSource<bool>();
            var actions = new FakeActions { RestoreGate = gate.Task };
            var onDisk = new List<Keycut> { NewKeycut("a"), NewKeycut("b"), NewKeycut("c") };
            var noop = new RelayCommand(() => { });
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(onDisk.ToList()), null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            onDisk.RemoveAt(1);

            var undo = manager.UndoDeleteAsync();

            // The file is back on disk and the window was activated: a reload reads it before the undo reports
            onDisk.Insert(1, NewKeycut("b"));
            await manager.LoadAsync();
            Assert.Equal(3, manager.Rows.Count);

            gate.SetResult(true);
            await undo;

            Assert.Equal(new[] { "a", "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(3, manager.KeycutCount);
            Assert.Equal("b", manager.SelectedRow!.Name);
        }

        [Fact]
        public async Task Undo_AReloadThatStartedBeforeTheRestore_CannotDropTheRestoredRow()
        {
            var actions = new FakeActions();
            var onDisk = new List<Keycut> { NewKeycut("a"), NewKeycut("b"), NewKeycut("c") };
            var noop = new RelayCommand(() => { });
            TaskCompletionSource<bool>? loadGate = null;
            var manager = new ManagerViewModel(noop, noop, async ct =>
            {
                // What the folder holds when the load starts; a gate can then hold the result back
                var snapshot = onDisk.ToList();

                if (loadGate != null)
                {
                    await loadGate.Task;
                }

                return (IReadOnlyList<Keycut>)snapshot;
            }, null, actions)
            {
                StatusMessageDuration = TimeSpan.FromHours(1)
            };
            await manager.LoadAsync();
            await manager.DeleteKeycutAsync(manager.Rows[1]);
            onDisk.RemoveAt(1);

            // The window is activated: a reload reads the folder without b, then stalls
            loadGate = new TaskCompletionSource<bool>();
            var reload = manager.LoadAsync();

            // The restore puts the file back, and the stalled reload finishes after it
            onDisk.Insert(1, NewKeycut("b"));
            await manager.UndoDeleteAsync();
            loadGate.SetResult(true);
            await reload;

            Assert.Equal(new[] { "a", "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(3, manager.KeycutCount);
            Assert.Equal("b", manager.SelectedRow!.Name);
        }

        static async Task WaitFor(Func<bool> condition)
        {
            for (var i = 0; i < 100 && !condition(); i++)
            {
                await Task.Delay(20);
            }

            Assert.True(condition());
        }

        #endregion Undo (#179)

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

            manager.Rows[0].CopyTargetCommand.Execute(null);
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
