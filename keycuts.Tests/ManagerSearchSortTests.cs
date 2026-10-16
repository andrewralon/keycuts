#nullable enable
using System.Linq;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class ManagerSearchSortTests
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

        static ManagerViewModel NewManager(params Keycut[] keycuts)
        {
            var noop = new RelayCommand(() => { });
            var manager = new ManagerViewModel(noop, noop);
            manager.ShowKeycuts(keycuts);
            return manager;
        }

        static Keycut[] Sample()
        {
            return new[]
            {
                NewKeycut("l-vicki", KeycutKind.Url, "https://example.com/vicki", "Team wiki"),
                NewKeycut("logs", KeycutKind.Folder, @"C:\var\data", "Server logs"),
                NewKeycut("Docs", KeycutKind.Folder, @"C:\docs", ""),
                NewKeycut("notes", KeycutKind.File, @"C:\notes.txt", "scratch")
            };
        }

        static string[] Names(ManagerViewModel manager)
        {
            return manager.Rows.Select(r => r.Name).ToArray();
        }

        [Fact]
        public void DefaultSort_IsShortcutAscending_CaseInsensitive()
        {
            var manager = NewManager(Sample());

            Assert.Equal(SortColumn.Shortcut, manager.SortColumn);
            Assert.False(manager.SortDescending);
            Assert.Equal(new[] { "Docs", "l-vicki", "logs", "notes" }, Names(manager));
        }

        [Fact]
        public void Search_MatchesNameAndDestination_CaseInsensitively()
        {
            var manager = NewManager(Sample());

            manager.SearchText = "VICKI";

            Assert.Equal(new[] { "l-vicki" }, Names(manager));
            Assert.Equal("1 keycut", manager.ResultCountText);
            Assert.Equal("4 items", manager.StatusText);
        }

        [Fact]
        public void Search_MatchesDestinationOnly()
        {
            var manager = NewManager(Sample());

            manager.SearchText = "example.com";

            Assert.Equal(new[] { "l-vicki" }, Names(manager));
        }

        [Fact]
        public void Search_MatchesTypeLabel()
        {
            var manager = NewManager(Sample());

            manager.SearchText = "folder";

            Assert.Equal(new[] { "Docs", "logs" }, Names(manager));
        }

        [Fact]
        public void Search_MatchesDescription()
        {
            var manager = NewManager(Sample());

            manager.SearchText = "logs";

            Assert.Equal(new[] { "logs" }, Names(manager));
            Assert.Equal("Server logs", manager.Rows[0].Description);
        }

        [Fact]
        public void Search_NoMatches_ShowsNoResultsAndClearRestores()
        {
            var manager = NewManager(Sample());

            manager.SearchText = "zzz";

            Assert.Equal(ManagerState.NoResults, manager.State);
            Assert.Empty(manager.Rows);
            Assert.Equal("4 items", manager.StatusText);

            manager.ClearSearchCommand.Execute(null);

            Assert.Equal(ManagerState.Populated, manager.State);
            Assert.Equal(4, manager.Rows.Count);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankSearch_MatchesEverything(string search)
        {
            var manager = NewManager(Sample());

            manager.SearchText = search;

            Assert.Equal(4, manager.Rows.Count);
        }

        [Fact]
        public void Matches_IgnoresSurroundingWhitespace()
        {
            var row = new KeycutRowViewModel(NewKeycut("alpha"));

            Assert.True(row.Matches("  ALP "));
            Assert.False(row.Matches("beta"));
        }

        [Fact]
        public void SortBy_ActiveColumnToggles_OtherColumnStartsAscending()
        {
            var manager = NewManager(Sample());

            manager.SortBy(SortColumn.Shortcut);
            Assert.True(manager.SortDescending);
            Assert.Equal(new[] { "notes", "logs", "l-vicki", "Docs" }, Names(manager));

            manager.SortBy(SortColumn.Description);
            Assert.Equal(SortColumn.Description, manager.SortColumn);
            Assert.False(manager.SortDescending);
            Assert.Equal(new[] { "Docs", "notes", "logs", "l-vicki" }, Names(manager));

            manager.SortBy(SortColumn.Description);
            Assert.Equal(new[] { "l-vicki", "logs", "notes", "Docs" }, Names(manager));
        }

        [Fact]
        public void SortByType_UsesLabelWithNameTieBreaker()
        {
            var manager = NewManager(Sample());

            manager.SortBy(SortColumn.Type);

            var labels = manager.Rows.Select(r => r.TypeLabel).ToArray();
            Assert.Equal(labels.OrderBy(l => l, System.StringComparer.CurrentCultureIgnoreCase), labels);

            var sameType = manager.Rows.Where(r => r.Kind == KeycutKind.Folder).Select(r => r.Name);
            Assert.Equal(new[] { "Docs", "logs" }, sameType);

            manager.SortBy(SortColumn.Type);
            Assert.Equal(labels.Reverse(), manager.Rows.Select(r => r.TypeLabel));
        }

        [Fact]
        public void SortPersists_WhileSearching()
        {
            var manager = NewManager(Sample());
            manager.SortBy(SortColumn.Shortcut);

            manager.SearchText = "folder";

            Assert.Equal(new[] { "logs", "Docs" }, Names(manager));

            manager.SearchText = string.Empty;

            Assert.Equal(new[] { "notes", "logs", "l-vicki", "Docs" }, Names(manager));
        }

        [Fact]
        public void SortCommand_UsesColumnParameter_AndIgnoresOthers()
        {
            var manager = NewManager(Sample());

            manager.SortCommand.Execute(SortColumn.Type);
            Assert.Equal(SortColumn.Type, manager.SortColumn);

            manager.SortCommand.Execute("junk");
            Assert.Equal(SortColumn.Type, manager.SortColumn);
            Assert.False(manager.SortDescending);
        }

        [Fact]
        public void Comparer_IsCultureAwareAndCaseInsensitive()
        {
            var comparer = ManagerViewModel.CreateComparer(SortColumn.Shortcut, false);
            var lower = new KeycutRowViewModel(NewKeycut("apple"));
            var upper = new KeycutRowViewModel(NewKeycut("Banana"));

            Assert.True(comparer.Compare(lower, upper) < 0);
        }

        [Fact]
        public void Reload_KeepsSearchAndSort()
        {
            var manager = NewManager(Sample());
            manager.SortBy(SortColumn.Shortcut);
            manager.SearchText = "folder";

            manager.ShowKeycuts(Sample().Concat(new[] { NewKeycut("zeta", KeycutKind.Folder) }).ToArray());

            Assert.Equal("folder", manager.SearchText);
            Assert.True(manager.SortDescending);
            Assert.Equal(new[] { "zeta", "logs", "Docs" }, Names(manager));
        }

        [Fact]
        public void UnchangedResult_DoesNotRebuildRows()
        {
            var manager = NewManager(Sample());
            var changes = 0;
            manager.Rows.CollectionChanged += (sender, e) => changes++;

            manager.SearchText = "oc";

            Assert.True(changes > 0);
            Assert.Equal(new[] { "Docs" }, Names(manager));

            changes = 0;
            manager.SearchText = "oc ";

            Assert.Equal(0, changes);
        }

        [Theory]
        [InlineData(SortColumn.Shortcut, SortColumn.Shortcut, false, SortHeaderState.Ascending)]
        [InlineData(SortColumn.Shortcut, SortColumn.Shortcut, true, SortHeaderState.Descending)]
        [InlineData(SortColumn.Type, SortColumn.Shortcut, true, SortHeaderState.None)]
        public void HeaderState_ReflectsActiveColumnAndDirection(SortColumn column, SortColumn active, bool descending, SortHeaderState expected)
        {
            Assert.Equal(expected, SortHeaderButton.GetState(column, active, descending));
        }
    }
}
