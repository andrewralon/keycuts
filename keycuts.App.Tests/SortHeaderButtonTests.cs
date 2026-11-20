using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Manager's sort headers (WPF's Controls/SortHeaderButton, ported in Phase 3.4 with the SortColumn it compares).</summary>
    public class SortHeaderButtonTests
    {
        [Theory]
        [InlineData(SortColumn.Shortcut, SortColumn.Shortcut, false, SortHeaderState.Ascending)]
        [InlineData(SortColumn.Shortcut, SortColumn.Shortcut, true, SortHeaderState.Descending)]
        [InlineData(SortColumn.Target, SortColumn.Shortcut, true, SortHeaderState.None)]
        public void State_IsTheSortWhenTheColumnIsTheActiveOne(SortColumn column, SortColumn active, bool descending, SortHeaderState expected)
        {
            Assert.Equal(expected, SortHeaderButton.GetState(column, active, descending));
        }

        [Theory]
        [InlineData(SortHeaderState.None, null, "")]
        [InlineData(SortHeaderState.Ascending, null, "Sorted ascending")]
        [InlineData(SortHeaderState.None, "Counting since Nov 15, 2026", "Counting since Nov 15, 2026")]
        [InlineData(SortHeaderState.Descending, "Counting since Nov 15, 2026", "Sorted descending. Counting since Nov 15, 2026")]
        public void HelpText_IsTheStateThenTheNote(SortHeaderState state, string? note, string expected)
        {
            Assert.Equal(expected, SortHeaderButton.GetHelpText(state, note));
        }

        [AvaloniaFact]
        public void AHeader_FollowsTheSort_AndShowsItsNoteAsATooltip()
        {
            var header = new SortHeaderButton { Column = SortColumn.Target, ActiveColumn = SortColumn.Shortcut };
            Assert.Equal(SortHeaderState.None, header.State);
            Assert.Null(AutomationProperties.GetHelpText(header));

            header.ActiveColumn = SortColumn.Target;
            header.IsDescending = true;
            Assert.Equal(SortHeaderState.Descending, header.State);
            Assert.Equal("Sorted descending", AutomationProperties.GetHelpText(header));

            header.Note = "Counting since Nov 15, 2026";
            Assert.Equal("Counting since Nov 15, 2026", ToolTip.GetTip(header));
            Assert.Equal("Sorted descending. Counting since Nov 15, 2026", AutomationProperties.GetHelpText(header));

            header.Note = string.Empty;
            Assert.Null(ToolTip.GetTip(header));
        }
    }
}
