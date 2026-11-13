#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    // Resizing the Manager's columns by dragging the dividers (#202). The columns are Name (0), Type (1), Description (2),
    // Target (3), Modified (4) and the menu (5). A drag is sent the way the table's own header gets it, as the grippers'
    // Thumb events, so no pointer is needed.
    public partial class AccessibilityTests
    {
        const int NameColumn = 0;
        const int TypeColumn = 1;
        const int DescriptionColumn = 2;
        const int TargetColumn = 3;
        const int ModifiedColumn = 4;
        const int MenuColumn = 5;

        /// <summary>Runs <paramref name="body"/> with a Manager of a few keycuts in a window <paramref name="width"/> wide.</summary>
        static void WithWideManager(double width, Action<Window, ManagerViewModel, DataGrid> body)
        {
            RunSta(() =>
            {
                EnsureApplication();
                var noop = new RelayCommand(() => { });
                var keycuts = Enumerable.Range(1, 5).Select(i => new Keycut
                {
                    Name = "k" + i,
                    Kind = KeycutKind.File,
                    RawType = ShortcutType.File,
                    // One long description, as real ones can be: what a column sized to its content would be as wide as
                    Description = i == 3 ? new string('d', 700) : "description " + i,
                    Target = @"C:\temp\k" + i + ".txt",
                    FilePath = @"C:\fixture\k" + i + ".bat",
                    Modified = DateTime.Now.AddDays(-i)
                }).ToList();
                var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null, new RecordingActions());
                manager.LoadAsync().GetAwaiter().GetResult();
                var window = new Window
                {
                    Content = new ManagerView(),
                    DataContext = manager,
                    Width = width,
                    Height = 460,   // tall enough for the five rows beside the footer (#205), or a scroll bar takes the table's width
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowInTaskbar = false,
                    ShowActivated = false
                };
                window.Show();
                Settle(window);

                try
                {
                    body(window, manager, Descendants<DataGrid>(window).Single());
                }
                finally
                {
                    window.Close();
                }
            });
        }

        /// <summary>The grid works out its column widths a layout at a time, so let it finish after a change.</summary>
        static void Settle(Window window)
        {
            Pump();
            Pump();
            Pump();
            window.UpdateLayout();
        }

        static DataGridColumnHeader HeaderOf(Window window, DataGrid table, int column)
        {
            return Descendants<DataGridColumnHeader>(window).Single(h => h.Column == table.Columns[column]);
        }

        static Thumb GripperOf(Window window, DataGrid table, int column, bool right)
        {
            return Descendants<Thumb>(HeaderOf(window, table, column)).Single(t => t.Name == (right ? "PART_RightHeaderGripper" : "PART_LeftHeaderGripper"));
        }

        /// <summary>Drags the gripper by <paramref name="change"/> device-independent pixels, positive to the right.</summary>
        static void DragGripper(Window window, DataGrid table, int column, bool right, double change)
        {
            var gripper = GripperOf(window, table, column, right);
            gripper.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent, Source = gripper });
            gripper.RaiseEvent(new DragDeltaEventArgs(change, 0) { RoutedEvent = Thumb.DragDeltaEvent, Source = gripper });
            gripper.RaiseEvent(new DragCompletedEventArgs(change, 0, false) { RoutedEvent = Thumb.DragCompletedEvent, Source = gripper });
            Settle(window);
        }

        static double Viewport(DataGrid table)
        {
            return ((ScrollViewer)table.Template.FindName("DG_ScrollViewer", table)).ViewportWidth;
        }

        static double ColumnsWidth(DataGrid table)
        {
            return table.Columns.Where(c => c.Visibility == Visibility.Visible).Sum(c => c.ActualWidth);
        }

        static double WidthOf(DataGrid table, int column)
        {
            return table.Columns[column].ActualWidth;
        }

        /// <summary>The columns fill the table exactly: not wider (nothing scrolls sideways) and not shorter (no gap).</summary>
        static void AssertFillsTheTable(DataGrid table)
        {
            Assert.InRange(ColumnsWidth(table), Viewport(table) - 1, Viewport(table) + 1);

            foreach (var column in table.Columns.Where(c => c.Visibility == Visibility.Visible))
            {
                Assert.True(column.ActualWidth >= column.MinWidth - 0.5, $"{column.Header} is {column.ActualWidth}, under its minimum of {column.MinWidth}");
            }
        }

        #region Which dividers can be dragged

        [Fact]
        public void Manager_Resizing_IsOnTheNameDescriptionAndTargetColumns()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                Assert.True(table.CanUserResizeColumns);
                Assert.True(table.Columns[NameColumn].CanUserResize);
                Assert.True(table.Columns[DescriptionColumn].CanUserResize);
                Assert.True(table.Columns[TargetColumn].CanUserResize);

                // A badge, a date and the menu button have one width
                Assert.False(table.Columns[TypeColumn].CanUserResize);
                Assert.False(table.Columns[ModifiedColumn].CanUserResize);
                Assert.False(table.Columns[MenuColumn].CanUserResize);
            });
        }

        // Only the dividers that can move show a gripper: a gripper that did nothing would only be in the way
        [Fact]
        public void Manager_Grippers_AreOnlyOnTheDividersThatCanMove()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                bool Shown(int column, bool right) => GripperOf(window, table, column, right).IsVisible;

                // Name | Type: from either side, since it resizes Name
                Assert.False(Shown(NameColumn, right: false));
                Assert.True(Shown(NameColumn, right: true));
                Assert.True(Shown(TypeColumn, right: false));
                Assert.False(Shown(TypeColumn, right: true));

                // Type | Description: nothing there to move
                Assert.False(Shown(DescriptionColumn, right: false));

                // Description | Target
                Assert.True(Shown(DescriptionColumn, right: true));
                Assert.True(Shown(TargetColumn, right: false));

                // The right of Target, and the fixed columns after it: pinned
                Assert.False(Shown(TargetColumn, right: true));
                Assert.False(Shown(ModifiedColumn, right: false));
                Assert.False(Shown(ModifiedColumn, right: true));
                Assert.False(Shown(MenuColumn, right: false));
                Assert.False(Shown(MenuColumn, right: true));
            });
        }

        #endregion Which dividers can be dragged

        #region Dragging

        [Fact]
        public void Manager_DraggingTheNameDivider_WidensName_AndTheOtherColumnsGiveWay()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                var name = WidthOf(table, NameColumn);
                var description = WidthOf(table, DescriptionColumn);
                var target = WidthOf(table, TargetColumn);
                var type = WidthOf(table, TypeColumn);
                var menu = WidthOf(table, MenuColumn);

                DragGripper(window, table, NameColumn, right: true, change: 80);

                Assert.Equal(name + 80, WidthOf(table, NameColumn), 1);
                Assert.True(WidthOf(table, DescriptionColumn) < description);
                Assert.True(WidthOf(table, TargetColumn) < target);
                Assert.Equal(type, WidthOf(table, TypeColumn), 1);
                Assert.Equal(menu, WidthOf(table, MenuColumn), 1);
                AssertFillsTheTable(table);

                // From the other side of the same divider, and back
                DragGripper(window, table, TypeColumn, right: false, change: -80);

                Assert.Equal(name, WidthOf(table, NameColumn), 1);
                AssertFillsTheTable(table);
            });
        }

        [Fact]
        public void Manager_DraggingTheDescriptionDivider_TradesWidthBetweenDescriptionAndTarget()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                var name = WidthOf(table, NameColumn);
                var description = WidthOf(table, DescriptionColumn);
                var target = WidthOf(table, TargetColumn);

                DragGripper(window, table, DescriptionColumn, right: true, change: 40);

                Assert.Equal(description + 40, WidthOf(table, DescriptionColumn), 1);
                Assert.Equal(target - 40, WidthOf(table, TargetColumn), 1);
                Assert.Equal(name, WidthOf(table, NameColumn), 1);
                AssertFillsTheTable(table);

                // The same divider from Target's side: that is what widens Target
                DragGripper(window, table, TargetColumn, right: false, change: 25);

                Assert.Equal(description + 65, WidthOf(table, DescriptionColumn), 1);
                Assert.Equal(target - 65, WidthOf(table, TargetColumn), 1);
                AssertFillsTheTable(table);

                DragGripper(window, table, TargetColumn, right: false, change: -200);

                Assert.True(WidthOf(table, TargetColumn) > target);
                AssertFillsTheTable(table);
            });
        }

        // The table has no horizontal scroll bar: a drag that wants more than there is stops at the others' minimums
        [Fact]
        public void Manager_Dragging_NeverMakesTheColumnsWiderThanTheTable_OrNarrowerThanTheirMinimums()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                foreach (var (column, right) in new[] { (NameColumn, true), (TypeColumn, false), (DescriptionColumn, true), (TargetColumn, false) })
                {
                    foreach (var change in new[] { 5000.0, -5000.0, 37.0, -113.0 })
                    {
                        DragGripper(window, table, column, right, change);

                        AssertFillsTheTable(table);
                    }
                }

                // All the way to the right leaves the others at their minimums, and Name has all the rest
                DragGripper(window, table, NameColumn, right: true, change: 5000);
                Assert.Equal(table.Columns[DescriptionColumn].MinWidth, WidthOf(table, DescriptionColumn), 1);
                Assert.Equal(table.Columns[TargetColumn].MinWidth, WidthOf(table, TargetColumn), 1);

                // And all the way back leaves Name at its own
                DragGripper(window, table, NameColumn, right: true, change: -5000);
                Assert.Equal(table.Columns[NameColumn].MinWidth, WidthOf(table, NameColumn), 1);
                AssertFillsTheTable(table);
            });
        }

        // Double-clicking a divider is a reflex for "fit this column", and the grid answers by sizing the column to its
        // content: a star column becomes a content-width one, and a long description pushes the others, the menu button
        // included, out of a table that can't scroll sideways. A double-click on a divider does nothing here.
        [Theory]
        [InlineData(NameColumn, true)]
        [InlineData(TypeColumn, false)]
        [InlineData(DescriptionColumn, true)]
        [InlineData(TargetColumn, false)]
        public void Manager_DoubleClickingADivider_DoesNotSizeTheColumnToItsContent(int column, bool right)
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                var gripper = GripperOf(window, table, column, right);
                var starred = table.Columns.Select(c => c.Width.UnitType).ToList();

                // What the framework does for a double click: the tunneling event first, and the bubbling one if that wasn't handled
                var preview = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Control.PreviewMouseDoubleClickEvent,
                    Source = gripper
                };
                gripper.RaiseEvent(preview);

                if (!preview.Handled)
                {
                    gripper.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    {
                        RoutedEvent = Control.MouseDoubleClickEvent,
                        Source = gripper
                    });
                }

                Settle(window);

                Assert.True(preview.Handled, "the tunneling double click should have been handled over a divider");
                Assert.Equal(starred, table.Columns.Select(c => c.Width.UnitType).ToList());
                AssertFillsTheTable(table);
            });
        }

        // The header holds the sort buttons, so a drag on a divider must never be a click on one
        [Fact]
        public void Manager_DraggingADivider_DoesNotSort()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                Assert.Equal(SortColumn.Shortcut, manager.SortColumn);
                Assert.False(manager.SortDescending);

                foreach (var (column, right) in new[] { (NameColumn, true), (TypeColumn, false), (DescriptionColumn, true), (TargetColumn, false) })
                {
                    DragGripper(window, table, column, right, 20);
                    DragGripper(window, table, column, right, -20);
                }

                Assert.Equal(SortColumn.Shortcut, manager.SortColumn);
                Assert.False(manager.SortDescending);
            });
        }

        // What a real pointer reaches over a divider is the gripper, not the sort button under the header's edge
        [Fact]
        public void Manager_AGripper_IsWhatThePointerHits_OverItsDivider()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                var content = (FrameworkElement)window.Content;

                foreach (var (column, right) in new[] { (NameColumn, true), (TypeColumn, false), (DescriptionColumn, true), (TargetColumn, false) })
                {
                    var gripper = GripperOf(window, table, column, right);
                    var middle = gripper.TranslatePoint(new Point(gripper.ActualWidth / 2, gripper.ActualHeight / 2), content);
                    var hit = VisualTreeHelper.HitTest(content, middle)?.VisualHit;

                    Assert.NotNull(hit);
                    Assert.True(ReferenceEquals(hit, gripper) || gripper.IsAncestorOf(hit), $"column {column}, right={right}: hit {hit!.GetType().Name}");
                    Assert.Null(FindSortButton(hit));
                }
            });
        }

        static SortHeaderButton? FindSortButton(DependencyObject node)
        {
            for (DependencyObject? current = node; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is SortHeaderButton button)
                {
                    return button;
                }
            }

            return null;
        }

        #endregion Dragging

        #region The window changing size

        // The Modified column makes way below a table width of 780 (#167); a drag mustn't fight that, nor the window the drag
        [Fact]
        public void Manager_AfterADrag_TheWindowShrinking_HidesModified_AndGrowingBringsItBack_WithTheWidthsKept()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                DragGripper(window, table, NameColumn, right: true, change: 60);
                DragGripper(window, table, DescriptionColumn, right: true, change: 30);
                var starName = table.Columns[NameColumn].Width.Value;
                var starDescription = table.Columns[DescriptionColumn].Width.Value;
                var starTarget = table.Columns[TargetColumn].Width.Value;
                Assert.Equal(Visibility.Visible, table.Columns[ModifiedColumn].Visibility);

                window.Width = 760;
                Settle(window);

                Assert.True(((FrameworkElement)window.Content).FindName("TableHost") is Border { ActualWidth: < 780 });
                Assert.Equal(Visibility.Collapsed, table.Columns[ModifiedColumn].Visibility);
                Assert.Equal(110, WidthOf(table, TypeColumn), 1);
                Assert.Equal(44, WidthOf(table, MenuColumn), 1);
                AssertFillsTheTable(table);

                window.Width = 1200;
                Settle(window);

                Assert.Equal(Visibility.Visible, table.Columns[ModifiedColumn].Visibility);
                Assert.Equal(110, WidthOf(table, ModifiedColumn), 1);
                Assert.Equal(110, WidthOf(table, TypeColumn), 1);
                AssertFillsTheTable(table);

                // The proportions the user chose are what the star columns still hold
                Assert.Equal(starName, table.Columns[NameColumn].Width.Value, 2);
                Assert.Equal(starDescription, table.Columns[DescriptionColumn].Width.Value, 2);
                Assert.Equal(starTarget, table.Columns[TargetColumn].Width.Value, 2);
            });
        }

        // Dragging with Modified hidden works the same, and the divider drag can't undo the hiding
        [Fact]
        public void Manager_DraggingWhileModifiedIsHidden_WorksTheSame()
        {
            WithWideManager(760, (window, manager, table) =>
            {
                Assert.Equal(Visibility.Collapsed, table.Columns[ModifiedColumn].Visibility);
                var name = WidthOf(table, NameColumn);

                DragGripper(window, table, NameColumn, right: true, change: 50);

                Assert.True(WidthOf(table, NameColumn) > name);
                Assert.Equal(Visibility.Collapsed, table.Columns[ModifiedColumn].Visibility);
                AssertFillsTheTable(table);

                DragGripper(window, table, NameColumn, right: true, change: 5000);
                AssertFillsTheTable(table);
            });
        }

        // The grid squeezed Type and the menu to its default 20 when the table jumped narrower than all the columns need
        // (a window snapped, or restored from maximized), and kept them that way once Modified had made way
        [Theory]
        [InlineData(1100, 720)]
        [InlineData(1500, 760)]
        [InlineData(900, 720)]
        public void Manager_ABigJumpNarrower_KeepsTheFixedColumns_AndFillsTheTable(double from, double to)
        {
            WithWideManager(from, (window, manager, table) =>
            {
                window.Width = to;
                Settle(window);

                Assert.Equal(Visibility.Collapsed, table.Columns[ModifiedColumn].Visibility);
                Assert.Equal(110, WidthOf(table, TypeColumn), 1);
                Assert.Equal(44, WidthOf(table, MenuColumn), 1);
                AssertFillsTheTable(table);
            });
        }

        [Fact]
        public void Manager_ModifiedReturnsAtTheRightWidth_AfterAnyDrag()
        {
            WithWideManager(1000, (window, manager, table) =>
            {
                var host = (Border)((FrameworkElement)window.Content).FindName("TableHost");

                // Either side of the threshold, whatever the drag did before
                DragGripper(window, table, NameColumn, right: true, change: 5000);

                foreach (var width in new[] { 800.0, 840.0, 846.0, 848.0, 860.0, 900.0, 846.0, 840.0 })
                {
                    window.Width = width;
                    Settle(window);

                    // 780 is ManagerView.ModifiedColumnMinTableWidth
                    var expected = host.ActualWidth < 780 ? Visibility.Collapsed : Visibility.Visible;
                    Assert.Equal(expected, table.Columns[ModifiedColumn].Visibility);
                    AssertFillsTheTable(table);
                }
            });
        }

        #endregion The window changing size
    }
}
