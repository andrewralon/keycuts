#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    public class ManagerSearchSortTests
    {
        static Keycut NewKeycut(string name, KeycutKind kind = KeycutKind.File, string target = @"C:\temp\a.txt", string description = "", DateTime modified = default)
        {
            return new Keycut
            {
                Name = name,
                Kind = kind,
                RawType = ShortcutType.File,
                Description = description,
                Target = target,
                FilePath = @"C:\Shortcuts\" + name + ".bat",
                Modified = modified
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
        public void Search_MatchesNameAndTarget_CaseInsensitively()
        {
            var manager = NewManager(Sample());

            manager.SearchText = "VICKI";

            Assert.Equal(new[] { "l-vicki" }, Names(manager));
            Assert.Equal("1 keycut", manager.ResultCountText);
            Assert.Equal("4 items", manager.StatusText);
        }

        [Fact]
        public void Search_MatchesTargetOnly()
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
        [InlineData(SortColumn.Target, SortColumn.Target, false, SortHeaderState.Ascending)]
        [InlineData(SortColumn.Modified, SortColumn.Modified, true, SortHeaderState.Descending)]
        [InlineData(SortColumn.Modified, SortColumn.Target, true, SortHeaderState.None)]
        public void HeaderState_ReflectsActiveColumnAndDirection(SortColumn column, SortColumn active, bool descending, SortHeaderState expected)
        {
            Assert.Equal(expected, SortHeaderButton.GetState(column, active, descending));
        }

        #region Target and Modified (#167)

        static readonly DateTime Now = new DateTime(2026, 11, 12, 15, 0, 0);

        static Keycut[] WithDates()
        {
            return new[]
            {
                NewKeycut("old", KeycutKind.File, @"C:\b\old.txt", "", Now.AddDays(-40)),
                NewKeycut("new", KeycutKind.Url, "https://example.com", "", Now.AddMinutes(-5)),
                NewKeycut("mid", KeycutKind.Folder, @"C:\a\mid", "", Now.AddDays(-3)),
                NewKeycut("same", KeycutKind.File, @"C:\a\mid", "", Now.AddDays(-3)),
                NewKeycut("unknown", KeycutKind.File, @"c:\c\unknown.txt")
            };
        }

        [Fact]
        public void SortByTarget_UsesTheTargetText_CaseInsensitively_WithNameAsTieBreaker()
        {
            var manager = NewManager(WithDates());

            manager.SortBy(SortColumn.Target);

            Assert.Equal(SortColumn.Target, manager.SortColumn);
            Assert.False(manager.SortDescending);
            // C:\a\mid twice (name order), C:\b, c:\c (the case doesn't matter), then https
            Assert.Equal(new[] { "mid", "same", "old", "unknown", "new" }, Names(manager));

            manager.SortBy(SortColumn.Target);

            Assert.Equal(new[] { "new", "unknown", "old", "mid", "same" }, Names(manager));
        }

        [Fact]
        public void SortByModified_StartsWithTheNewestFirst_AndToggles()
        {
            var manager = NewManager(WithDates());

            manager.SortBy(SortColumn.Modified);

            Assert.Equal(SortColumn.Modified, manager.SortColumn);
            Assert.True(manager.SortDescending);
            // Unknown (no date) is the oldest; equal dates fall back to the name
            Assert.Equal(new[] { "new", "mid", "same", "old", "unknown" }, Names(manager));

            manager.SortBy(SortColumn.Modified);

            Assert.False(manager.SortDescending);
            Assert.Equal(new[] { "unknown", "old", "mid", "same", "new" }, Names(manager));
        }

        [Fact]
        public void SortingBackToAnotherColumn_StartsAscendingAgain()
        {
            var manager = NewManager(WithDates());
            manager.SortBy(SortColumn.Modified);

            manager.SortBy(SortColumn.Target);

            Assert.False(manager.SortDescending);
        }

        [Fact]
        public void SortByModified_PersistsWhileSearching()
        {
            var manager = NewManager(WithDates());
            manager.SortBy(SortColumn.Modified);

            manager.SearchText = "file";

            Assert.Equal(new[] { "same", "old", "unknown" }, Names(manager));
        }

        [Fact]
        public async Task Reload_KeepsTheRowsWhileNothingChanged_ButRebuildsThemForANewDate()
        {
            var current = WithDates();
            var noop = new RelayCommand(() => { });
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(current));
            await manager.LoadAsync();
            var first = manager.Rows.ToArray();

            await manager.LoadAsync();

            Assert.True(first.SequenceEqual(manager.Rows));

            // Saved again elsewhere (a hand edit in Notepad): the file's date moved and nothing else did
            current = WithDates().Select(k => k.Name == "new" ? k with { Modified = Now } : k).ToArray();
            await manager.LoadAsync();

            Assert.False(first.SequenceEqual(manager.Rows));
            Assert.Equal(Now, manager.Rows.Single(r => r.Name == "new").Modified);
        }

        [Theory]
        [InlineData(0, "just now")]
        [InlineData(59, "just now")]
        [InlineData(60, "1 minute ago")]
        [InlineData(5 * 60, "5 minutes ago")]
        [InlineData(59 * 60, "59 minutes ago")]
        [InlineData(60 * 60, "1 hour ago")]
        [InlineData(5 * 3600, "5 hours ago")]
        [InlineData(23 * 3600, "23 hours ago")]
        [InlineData(24 * 3600, "yesterday")]
        [InlineData(47 * 3600, "yesterday")]
        [InlineData(2 * 86400, "2 days ago")]
        [InlineData(29 * 86400, "29 days ago")]
        [InlineData(30 * 86400, "1 month ago")]
        [InlineData(75 * 86400, "2 months ago")]
        [InlineData(364 * 86400, "12 months ago")]
        [InlineData(365 * 86400, "1 year ago")]
        [InlineData(800 * 86400, "2 years ago")]
        public void RelativeTime_SaysHowLongAgo_InOneUnit(int secondsAgo, string expected)
        {
            Assert.Equal(expected, RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now));
        }

        [Fact]
        public void RelativeTime_ATimeInTheFuture_ReadsJustNow()
        {
            Assert.Equal("just now", RelativeTime.Format(Now.AddHours(2), Now));
        }

        [Fact]
        public void RelativeTime_AnUnknownTime_IsEmpty()
        {
            Assert.Equal(string.Empty, RelativeTime.Format(default, Now));
            Assert.Equal(string.Empty, RelativeTime.FormatFull(default));
        }

        [Fact]
        public void Row_ShowsHowLongAgo_WithTheExactTimeAsTheTooltip()
        {
            var modified = DateTime.Now.AddDays(-3);
            var row = new KeycutRowViewModel(NewKeycut("alpha", modified: modified));

            Assert.Equal("3 days ago", row.ModifiedText);
            Assert.Equal(modified.ToString("f"), row.ModifiedTooltip);
            Assert.Equal(modified, row.Modified);
        }

        [Fact]
        public void Row_WithoutADate_ShowsNothing()
        {
            var row = new KeycutRowViewModel(NewKeycut("alpha"));

            Assert.Equal(string.Empty, row.ModifiedText);
            Assert.Equal(string.Empty, row.ModifiedTooltip);
        }

        #endregion Target and Modified (#167)
    }
}
