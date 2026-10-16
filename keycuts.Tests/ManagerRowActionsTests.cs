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
    }
}
