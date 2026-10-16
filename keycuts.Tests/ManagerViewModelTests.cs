#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class ManagerViewModelTests
    {
        static Keycut NewKeycut(string name, KeycutKind kind = KeycutKind.File, string destination = @"C:\temp\a.txt", string description = "")
        {
            return new Keycut
            {
                Name = name,
                Kind = kind,
                RawType = ShortcutType.File,
                Description = description,
                Destination = destination,
                FilePath = @"C:\Shortcuts\" + name + ".bat"
            };
        }

        static ManagerViewModel NewManager(
            Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? loader,
            Func<IReadOnlyList<Keycut>, CancellationToken, Task>? checker = null)
        {
            var noop = new RelayCommand(() => { });
            return new ManagerViewModel(noop, noop, loader, checker);
        }

        static Func<CancellationToken, Task<IReadOnlyList<Keycut>>> Returning(params Keycut[] keycuts)
        {
            return ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts);
        }

        [Fact]
        public void StartsInLoadingState_WithSearchDisabled()
        {
            var manager = NewManager(Returning());

            Assert.Equal(ManagerState.Loading, manager.State);
            Assert.True(manager.IsLoading);
            Assert.False(manager.CanSearch);
            Assert.False(manager.IsTableVisible);
        }

        [Fact]
        public async Task LoadAsync_Populates_RowsCountsAndTexts()
        {
            var manager = NewManager(Returning(NewKeycut("a"), NewKeycut("b"), NewKeycut("c")));

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Populated, manager.State);
            Assert.Equal(new[] { "a", "b", "c" }, manager.Rows.Select(r => r.Name));
            Assert.Equal(3, manager.KeycutCount);
            Assert.Equal("3 keycuts", manager.ResultCountText);
            Assert.Equal("3 items", manager.StatusText);
            Assert.True(manager.IsTableVisible);
            Assert.True(manager.CanSearch);
            Assert.False(manager.IsLoading);
        }

        [Fact]
        public async Task LoadAsync_SingleKeycut_UsesSingularTexts()
        {
            var manager = NewManager(Returning(NewKeycut("only")));

            await manager.LoadAsync();

            Assert.Equal("1 keycut", manager.ResultCountText);
            Assert.Equal("1 item", manager.StatusText);
        }

        [Fact]
        public async Task LoadAsync_NoKeycuts_ShowsEmptyState()
        {
            var manager = NewManager(Returning());

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Empty, manager.State);
            Assert.True(manager.IsEmpty);
            Assert.False(manager.CanSearch);
            Assert.Equal("0 keycuts", manager.ResultCountText);
            Assert.Equal("0 items", manager.StatusText);
        }

        [Fact]
        public async Task LoadAsync_LoaderFails_ShowsErrorState()
        {
            var manager = NewManager(ct => throw new KeycutRepositoryException(KeycutRepositoryError.AccessDenied, "Can't read folder."));

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Error, manager.State);
            Assert.True(manager.HasError);
            Assert.Equal("Can't read folder.", manager.ErrorMessage);
            Assert.Empty(manager.Rows);
            Assert.Equal(0, manager.KeycutCount);
            Assert.False(manager.CanSearch);
        }

        [Fact]
        public async Task LoadAsync_RecoversFromErrorOnNextLoad()
        {
            var fail = true;
            var manager = NewManager(ct =>
            {
                if (fail)
                {
                    throw new InvalidOperationException("boom");
                }

                return Task.FromResult<IReadOnlyList<Keycut>>(new[] { NewKeycut("a") });
            });

            await manager.LoadAsync();
            Assert.True(manager.HasError);

            fail = false;
            await manager.LoadAsync();

            Assert.Equal(ManagerState.Populated, manager.State);
            Assert.Equal(string.Empty, manager.ErrorMessage);
        }

        [Fact]
        public async Task LoadAsync_UnchangedContent_KeepsRowInstances()
        {
            var manager = NewManager(ct => Task.FromResult<IReadOnlyList<Keycut>>(new[] { NewKeycut("a"), NewKeycut("b") }));

            await manager.LoadAsync();
            var first = manager.Rows.ToList();
            await manager.LoadAsync();

            Assert.Equal(first, manager.Rows);
        }

        [Fact]
        public async Task LoadAsync_ChangedContent_ReplacesRows()
        {
            var keycuts = new[] { NewKeycut("a") };
            var manager = NewManager(ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts));

            await manager.LoadAsync();
            keycuts = new[] { NewKeycut("a"), NewKeycut("b") };
            await manager.LoadAsync();

            Assert.Equal(2, manager.Rows.Count);
            Assert.Equal(2, manager.KeycutCount);
        }

        [Fact]
        public async Task LoadAsync_SecondLoad_DoesNotShowLoadingIndicator()
        {
            var release = new TaskCompletionSource<bool>();
            var calls = 0;
            var manager = NewManager(async ct =>
            {
                if (++calls == 2)
                {
                    await release.Task;
                }

                return new[] { NewKeycut("a") };
            });
            await manager.LoadAsync();

            var second = manager.LoadAsync();

            Assert.False(manager.IsLoading);
            Assert.Equal(ManagerState.Populated, manager.State);
            release.SetResult(true);
            await second;
        }

        [Fact]
        public async Task LoadAsync_StaleLoadIsIgnored()
        {
            var firstGate = new TaskCompletionSource<bool>();
            var calls = 0;
            var manager = NewManager(async ct =>
            {
                if (++calls == 1)
                {
                    await firstGate.Task;
                    return new[] { NewKeycut("old") };
                }

                return new[] { NewKeycut("new") };
            });

            var first = manager.LoadAsync();
            await manager.LoadAsync();
            firstGate.SetResult(true);
            await first;

            Assert.Equal(new[] { "new" }, manager.Rows.Select(r => r.Name));
        }

        [Fact]
        public async Task LoadAsync_DestinationCheck_FlagsMissingRowsAndRaisesChange()
        {
            var manager = NewManager(
                Returning(NewKeycut("gone"), NewKeycut("here")),
                (keycuts, ct) =>
                {
                    keycuts.Single(k => k.Name == "gone").DestinationStatus = DestinationStatus.Missing;
                    return Task.CompletedTask;
                });
            var raised = new List<string?>();

            await manager.LoadAsync();
            manager.Rows[0].PropertyChanged += (sender, e) => raised.Add(e.PropertyName);
            await manager.LoadAsync();

            var gone = manager.Rows.Single(r => r.Name == "gone");
            Assert.True(gone.IsDestinationMissing);
            Assert.Contains("gone", gone.Keycut.Name);
            Assert.Contains(@"C:\temp\a.txt", gone.MissingTooltip);
            Assert.False(manager.Rows.Single(r => r.Name == "here").IsDestinationMissing);
            Assert.Contains(nameof(KeycutRowViewModel.IsDestinationMissing), raised);
        }

        [Fact]
        public async Task LoadAsync_DestinationCheckFailure_IsIgnored()
        {
            var manager = NewManager(Returning(NewKeycut("a")), (keycuts, ct) => throw new IOException("slow disk"));

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Populated, manager.State);
        }

        [Fact]
        public async Task LoadAsync_WithoutLoader_ShowsEmptyState()
        {
            var manager = NewManager(null);

            await manager.LoadAsync();

            Assert.False(manager.CanLoad);
            Assert.Equal(ManagerState.Empty, manager.State);
        }

        [Fact]
        public async Task ClearSearchCommand_ClearsSearchText()
        {
            var manager = NewManager(Returning(NewKeycut("a")));
            await manager.LoadAsync();
            manager.SearchText = "zzz";

            manager.ClearSearchCommand.Execute(null);

            Assert.Equal(string.Empty, manager.SearchText);
        }

        [Fact]
        public async Task NoResultsMessage_QuotesTheQuery()
        {
            var manager = NewManager(Returning(NewKeycut("a")));
            await manager.LoadAsync();
            manager.SearchText = "xyz";

            Assert.Equal("No keycuts match \u201Cxyz\u201D", manager.NoResultsMessage);
        }

        [Fact]
        public void Row_ExposesDisplayValues()
        {
            var row = new KeycutRowViewModel(NewKeycut("n", KeycutKind.Url, "https://example.com", "desc"));

            Assert.Equal("n", row.Name);
            Assert.Equal(KeycutKind.Url, row.Kind);
            Assert.Equal("desc", row.Description);
            Assert.Equal("https://example.com", row.Destination);
            Assert.False(row.IsDestinationMissing);
            Assert.False(row.IsMenuOpen);
        }

        [Fact]
        public async Task Repository_MissingFolder_LoadsEmpty_WithoutCreatingIt()
        {
            var folder = Path.Combine(Path.GetTempPath(), "keycuts-mgr-tests-" + Guid.NewGuid().ToString("N"));
            var repository = new KeycutRepository(folder);
            var manager = NewManager(repository.LoadAllAsync, repository.CheckDestinationsAsync);

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Empty, manager.State);
            Assert.False(Directory.Exists(folder));
        }

        [Fact]
        public async Task Repository_InvalidFolder_ShowsErrorState()
        {
            var repository = new KeycutRepository("bad\0folder");
            var manager = NewManager(repository.LoadAllAsync);

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Error, manager.State);
            Assert.NotEqual(string.Empty, manager.ErrorMessage);
        }

        [Fact]
        public async Task Shell_InManagerMode_ReloadLoadsRowsAndUpdatesCounts()
        {
            var shell = new ShellViewModel(
                () => Task.FromResult(99),
                Returning(NewKeycut("a"), NewKeycut("b")));

            shell.ShowManager();
            await shell.ReloadAsync();

            Assert.Equal(2, shell.Manager.Rows.Count);
            Assert.Equal(2, shell.Manager.KeycutCount);
            Assert.Equal(2, shell.Creator.KeycutCount);
        }

        [Fact]
        public async Task Shell_InCreatorMode_ReloadOnlyReadsTheCount()
        {
            var loads = 0;
            var shell = new ShellViewModel(
                () => Task.FromResult(4),
                ct =>
                {
                    loads++;
                    return Task.FromResult<IReadOnlyList<Keycut>>(Array.Empty<Keycut>());
                });

            await shell.ReloadAsync();

            Assert.Equal(0, loads);
            Assert.Equal(4, shell.Creator.KeycutCount);
        }
    }
}
