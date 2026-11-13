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
        static Keycut NewKeycut(string name, KeycutKind kind = KeycutKind.File, string target = @"C:\temp\a.txt", string description = "")
        {
            return new Keycut
            {
                Name = name,
                Kind = kind,
                RawType = ShortcutType.File,
                Description = description,
                Target = target,
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
            Assert.Equal("3 keycuts", manager.CountDescription);
            Assert.True(manager.IsTableVisible);
            Assert.True(manager.CanSearch);
            Assert.False(manager.IsLoading);
        }

        [Fact]
        public async Task LoadAsync_SingleKeycut_UsesSingularTexts()
        {
            var manager = NewManager(Returning(NewKeycut("only")));

            await manager.LoadAsync();

            Assert.Equal("1 keycut", manager.CountDescription);
        }

        [Fact]
        public async Task LoadAsync_NoKeycuts_ShowsEmptyState()
        {
            var manager = NewManager(Returning());

            await manager.LoadAsync();

            Assert.Equal(ManagerState.Empty, manager.State);
            Assert.True(manager.IsEmpty);
            Assert.False(manager.CanSearch);
            Assert.Equal("0 keycuts", manager.CountDescription);
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
        public async Task LoadAsync_TargetCheck_FlagsMissingRowsAndRaisesChange()
        {
            var manager = NewManager(
                Returning(NewKeycut("gone"), NewKeycut("here")),
                (keycuts, ct) =>
                {
                    keycuts.Single(k => k.Name == "gone").TargetStatus = TargetStatus.Missing;
                    return Task.CompletedTask;
                });
            var raised = new List<string?>();

            await manager.LoadAsync();
            manager.Rows[0].PropertyChanged += (sender, e) => raised.Add(e.PropertyName);
            await manager.LoadAsync();

            var gone = manager.Rows.Single(r => r.Name == "gone");
            Assert.True(gone.IsTargetMissing);
            Assert.Contains("gone", gone.Keycut.Name);
            Assert.Contains(@"C:\temp\a.txt", gone.MissingTooltip);
            Assert.False(manager.Rows.Single(r => r.Name == "here").IsTargetMissing);
            Assert.Contains(nameof(KeycutRowViewModel.IsTargetMissing), raised);
        }

        // #203: the tooltip names the path the check looked for, expanded, so a stale (x86) folder is plain to see
        [Fact]
        public void MissingTooltip_NamesThePathThatWasLookedFor()
        {
            var keycut = NewKeycut("chrome") with { Target = @"%programfiles(x86)%\Google\Chrome\Application\chrome.exe" };
            keycut.CheckedPath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
            keycut.TargetStatus = TargetStatus.Missing;

            var row = new KeycutRowViewModel(keycut);

            Assert.Equal(@"Not found: C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", row.MissingTooltip);
        }

        [Fact]
        public void MissingTooltip_ForABareName_SaysWhereItWasSearched()
        {
            var keycut = NewKeycut("n") with { Target = "notepad.exe" };
            keycut.CheckedPath = "notepad.exe";
            keycut.CheckedDetail = "searched the Windows folder, System32, PATH and App Paths";

            var row = new KeycutRowViewModel(keycut);

            Assert.Equal("Not found: notepad.exe (searched the Windows folder, System32, PATH and App Paths)", row.MissingTooltip);
        }

        [Fact]
        public void MissingTooltip_ForAStartInFolder_SaysWhatWasSearched()
        {
            var keycut = NewKeycut("h") with { Target = @"%programfiles(x86)%\Google\Chrome\Application\chrome.exe" };
            keycut.CheckedPath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
            keycut.CheckedDetail = "the start-in folder doesn't exist, and START only gets past that for a program that is in App Paths";

            var row = new KeycutRowViewModel(keycut);

            Assert.Equal(
                @"Not found: C:\Program Files (x86)\Google\Chrome\Application\chrome.exe (the start-in folder doesn't exist, and START only gets past that for a program that is in App Paths)",
                row.MissingTooltip);
        }

        // #203: a keycut that starts but names a start-in folder that isn't there gets a warning, not the Missing mark
        [Fact]
        public void AWarning_IsNotMissing_AndShowsItsOwnIndicatorAndTooltip()
        {
            var keycut = NewKeycut("h");
            keycut.TargetStatus = TargetStatus.Warning;
            keycut.CheckedDetail = @"It starts, but its start-in folder C:\gone doesn't exist.";

            var row = new KeycutRowViewModel(keycut);

            Assert.True(row.IsTargetWarning);
            Assert.True(row.ShowWarningIndicator);
            Assert.False(row.IsTargetMissing);
            Assert.False(row.ShowMissingIndicator);
            Assert.Equal(@"It starts, but its start-in folder C:\gone doesn't exist.", row.WarningTooltip);
        }

        [Fact]
        public void AMissingKeycut_HasNoWarningIndicator()
        {
            var keycut = NewKeycut("m");
            keycut.TargetStatus = TargetStatus.Missing;

            var row = new KeycutRowViewModel(keycut);

            Assert.True(row.ShowMissingIndicator);
            Assert.False(row.ShowWarningIndicator);
        }

        [Fact]
        public void MissingTooltip_BeforeACheckHasSaid_FallsBackToTheTarget()
        {
            var row = new KeycutRowViewModel(NewKeycut("a"));

            Assert.Equal(@"Not found: C:\temp\a.txt", row.MissingTooltip);
        }

        // #203: the Target cell shows the program, so the arguments (which profile a Chrome launcher opens) are in its tooltip
        [Theory]
        [InlineData(@"C:\Tools\app.exe", @"C:\Tools\app.exe", "", @"C:\Tools\app.exe")]
        [InlineData(@"%programfiles(x86)%\Google\Chrome\Application\chrome.exe", @"%programfiles(x86)%\Google\Chrome\Application\chrome.exe", "--profile-directory=\"Profile 2\"", @"%programfiles(x86)%\Google\Chrome\Application\chrome.exe --profile-directory=""Profile 2""")]
        // The 0.1.x form with quoted arguments keeps them in its target, spliced; the tooltip reads them as they were typed
        [InlineData("C:\\Tools\\app.exe\" \"arg", @"C:\Tools\app.exe", "\"arg\"", "C:\\Tools\\app.exe \"arg\"")]
        public void TargetTooltip_IsTheProgramWithItsArguments(string target, string program, string arguments, string expected)
        {
            var row = new KeycutRowViewModel(NewKeycut("a") with { Target = target, Program = program, Arguments = arguments });

            Assert.Equal(expected, row.TargetTooltip);
        }

        [Fact]
        public void TargetTooltip_ForAKeycutWithNoProgram_IsTheTarget()
        {
            var row = new KeycutRowViewModel(NewKeycut("a", target: "https://example.com"));

            Assert.Equal("https://example.com", row.TargetTooltip);
        }

        [Fact]
        public async Task LoadAsync_TargetCheckFailure_IsIgnored()
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
            Assert.Equal("https://example.com", row.Target);
            Assert.False(row.IsTargetMissing);
            Assert.False(row.IsMenuOpen);
        }

        [Fact]
        public async Task Repository_MissingFolder_LoadsEmpty_WithoutCreatingIt()
        {
            var folder = Path.Combine(Path.GetTempPath(), "keycuts-mgr-tests-" + Guid.NewGuid().ToString("N"));
            var repository = new KeycutRepository(folder);
            var manager = NewManager(repository.LoadAllAsync, repository.CheckTargetsAsync);

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
