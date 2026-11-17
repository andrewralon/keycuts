#nullable enable
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    // The Manager's table is a whole number of rows tall, so its last row never shows in part
    public partial class AccessibilityTests
    {
        [Theory]
        [InlineData(300, 38, 37, 297)]      // 7 rows (259) and 3 left over
        [InlineData(297, 38, 37, 297)]      // exactly 7 rows
        [InlineData(296, 38, 37, 297)]      // 1 short of 7: layout rounding, still 7 (1 over)
        [InlineData(295, 38, 37, 260)]      // 2 short of 7: 6 rows
        [InlineData(60, 38, 37, 60)]        // less than one row: all of it
        [InlineData(300, 38, 0, 300)]       // no row height known
        public void FitToRows_KeepsWholeRows(double available, double chrome, double rowHeight, double expected)
        {
            Assert.Equal(expected, ManagerView.FitToRows(available, chrome, rowHeight));
        }

        [Fact]
        public void ManagerTable_IsAWholeNumberOfRowsTall_AtEveryHeight()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var names = Enumerable.Range(1, 40).Select(i => "keycut" + i).ToArray();
                var view = new ManagerView();
                var window = Host(view, NewLoadedManager(null, null, names));

                try
                {
                    foreach (var height in new[] { 500.0, 517, 533, 560, 611 })
                    {
                        window.Height = height;
                        Pump();
                        window.UpdateLayout();

                        var host = (FrameworkElement)view.FindName("TableHost");
                        var table = (DataGrid)view.FindName("Table");
                        var row = Descendants<DataGridRow>(table).First(r => r.IsVisible);
                        var rows = host.ActualHeight - 2 - table.ColumnHeaderHeight;

                        Assert.True(row.ActualHeight > 0);
                        Assert.Equal(0, rows % row.ActualHeight, 3);
                        Assert.True(rows >= row.ActualHeight, $"at {height}: {rows} for rows");
                    }
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
