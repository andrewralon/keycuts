#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    // Editing the name and the description where they are (#202)
    public partial class AccessibilityTests
    {
        /// <summary>A mouse press on <paramref name="target"/>, as the window sees it before the table does. True if it was handled.</summary>
        static bool Click(UIElement target, MouseButton button = MouseButton.Left)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent,
                Source = target
            };
            target.RaiseEvent(args);
            Pump();
            return args.Handled;
        }

        /// <summary>The cell of a row in a column of the table: 0 Name, 1 Type, 2 Description, 3 Target, 4 Modified, 5 the menu.</summary>
        static DataGridCell CellOf(Window window, KeycutRowViewModel row, int column)
        {
            var table = Descendants<DataGrid>(window).Single();

            return Descendants<DataGridCell>(window).Single(c => ReferenceEquals(c.DataContext, row) && c.Column == table.Columns[column]);
        }

        static TextBox VisibleBox(Window window, string accessibleName)
        {
            return Descendants<TextBox>(window).Single(t => t.IsVisible && AutomationProperties.GetName(t) == accessibleName);
        }

        #region Shift+F2

        [Fact]
        public void Manager_ShiftF2_EditsTheDescriptionInPlace_EnterSaves_EscapeCancels()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                // Nothing selected: nothing to edit
                Assert.False(Press(search, Key.F2, ModifierKeys.Shift));
                Assert.False(manager.IsEditing);

                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.F2, ModifierKeys.Shift));
                Pump();

                var box = VisibleBox(window, "New description for bravo");
                Assert.True(manager.IsEditingDescription);
                Assert.False(manager.IsRenaming);
                Assert.Equal(string.Empty, box.Text);
                Assert.True(box.IsKeyboardFocusWithin);

                // The arrows stay in the box instead of moving the selection, and Escape cancels without leaving the manager
                Assert.True(Press(box, Key.Up));
                Assert.Equal("bravo", manager.SelectedRow?.Name);
                box.Text = "The second row";
                Assert.True(Press(box, Key.Escape));
                Assert.False(manager.IsEditingDescription);
                Assert.Empty(actions.Described);

                Assert.True(Press(search, Key.F2, ModifierKeys.Shift));
                Pump();
                box = VisibleBox(window, "New description for bravo");
                Assert.Equal(string.Empty, box.Text);
                box.Text = "The second row";
                Assert.True(Press(box, Key.Enter));
                Pump();

                var described = Assert.Single(actions.Described);
                Assert.Equal(@"C:\fixture\bravo.bat", described.Keycut.FilePath);
                Assert.Equal("The second row", described.Description);
                Assert.Equal("The second row", manager.Rows[1].Description);
                Assert.Equal("Saved the description of \u201Cbravo\u201D", manager.StatusMessage);
                Assert.DoesNotContain(Descendants<TextBox>(window), t => t.IsVisible && t != search && AutomationProperties.GetName(t).StartsWith("New description"));
            });
        }

        [Fact]
        public void Manager_RowKeys_StepAside_WhileTheDescriptionIsEdited()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.F2, ModifierKeys.Shift));
                Pump();

                Assert.False(Press(search, Key.Delete));
                Assert.False(Press(search, Key.F2));
                Assert.False(Press(search, Key.C, ModifierKeys.Control));
                Assert.False(Press(search, Key.F2, ModifierKeys.Shift));
                Assert.Empty(actions.Deleted);
                Assert.False(manager.IsRenaming);
                Assert.True(manager.IsEditingDescription);
            });
        }

        [Fact]
        public void Manager_EditDescription_IsInTheMenu_WithItsKey()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                var item = OpenRowMenuItem(window, "alpha", "Edit description", out var menu);

                Assert.Equal("Shift+F2", item.InputGestureText);
                Assert.True(item.IsEnabled);

                // Not through SafeItem_Click: the focus goes to the box, not back to the menu button
                item.Command.Execute(null);
                menu.IsOpen = false;
                Pump();

                Assert.True(manager.IsEditingDescription);
                Assert.Equal("alpha", VisibleBox(window, "New description for alpha").DataContext is KeycutRowViewModel row ? row.Name : null);
            });
        }

        #endregion Shift+F2

        #region Clicking

        // Explorer's rename: a click on the selected row edits, a click on another row only selects it
        [Fact]
        public void Manager_ClickOnTheNameOrDescriptionOfTheSelectedRow_EditsItInPlace()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var alpha = manager.Rows[0];
                var bravo = manager.Rows[1];

                // Nothing selected, so this click only selects (the table does that)
                Assert.False(Click(CellOf(window, alpha, 0)));
                Assert.False(manager.IsEditing);

                manager.SelectedRow = alpha;

                // Another row's cells only select it
                Assert.False(Click(CellOf(window, bravo, 0)));
                Assert.False(Click(CellOf(window, bravo, 2)));
                Assert.False(manager.IsEditing);

                Assert.True(Click(CellOf(window, alpha, 0)));
                Assert.True(manager.IsRenaming);
                Assert.True(alpha.IsRenaming);
                manager.CancelRename();

                Assert.True(Click(CellOf(window, alpha, 2)));
                Assert.True(manager.IsEditingDescription);
                Assert.True(alpha.IsEditingDescription);
                manager.CancelEditDescription();

                // The empty part of a description cell counts as the cell, so an empty description can be started the same way
                Assert.Equal(string.Empty, alpha.Description);
            });
        }

        [Fact]
        public void Manager_ClickOnOtherColumns_OrWithAnotherButton_EditsNothing()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                var alpha = manager.Rows[0];
                manager.SelectedRow = alpha;

                Assert.False(Click(CellOf(window, alpha, 1)));
                Assert.False(Click(CellOf(window, alpha, 3)));
                Assert.False(Click(CellOf(window, alpha, 5)));
                Assert.False(Click(CellOf(window, alpha, 0), MouseButton.Right));
                Assert.False(Click(CellOf(window, alpha, 2), MouseButton.Middle));
                Assert.False(manager.IsEditing);
            });
        }

        [Fact]
        public void Manager_ClickWhileAMenuIsOpen_OrJustAfterOneClosed_EditsNothing()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                var alpha = manager.Rows[0];
                manager.SelectedRow = alpha;

                OpenRowMenuItem(window, "alpha", "Edit", out var menu);
                Assert.False(Click(CellOf(window, alpha, 0)));
                Assert.False(manager.IsEditing);

                // The click that dismisses a menu is delivered once the menu has closed: it isn't an edit
                menu.IsOpen = false;
                Pump();
                Assert.False(Click(CellOf(window, alpha, 0)));
                Assert.False(manager.IsEditing);

                Thread.Sleep(350);
                Assert.True(Click(CellOf(window, alpha, 0)));
                Assert.True(manager.IsRenaming);
            });
        }

        // The box owns the keys and the clicks inside it; any other click saves what was typed, and only saves
        [Fact]
        public void Manager_ClickOutsideTheDescriptionBox_Saves_AndAClickInsideDoesNot()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var alpha = manager.Rows[0];
                manager.SelectedRow = alpha;
                Assert.True(Click(CellOf(window, alpha, 2)));
                Pump();
                var box = VisibleBox(window, "New description for alpha");
                box.Text = "typed";

                Assert.False(Click(box));
                Assert.True(manager.IsEditingDescription);
                Assert.Empty(actions.Described);

                // On the name of the same selected row: it saves, and does not also start a rename
                Assert.False(Click(CellOf(window, alpha, 0)));

                Assert.False(manager.IsEditingDescription);
                Assert.False(manager.IsRenaming);
                Assert.Equal("typed", actions.Described.Single().Description);
            });
        }

        [Fact]
        public void Manager_ClickOnTheSearchBox_SavesARename_AsItAlwaysDid()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                var alpha = manager.Rows[0];
                manager.SelectedRow = alpha;
                Assert.True(Click(CellOf(window, alpha, 0)));
                Pump();
                var box = VisibleBox(window, "New name for alpha");
                box.Text = "alpha2";

                Assert.False(Click(box));
                Assert.True(manager.IsRenaming);

                Assert.False(Click(search));

                Assert.False(manager.IsRenaming);
                Assert.Equal(new[] { "alpha2" }, actions.Renamed);
            });
        }

        #endregion Clicking
    }
}
