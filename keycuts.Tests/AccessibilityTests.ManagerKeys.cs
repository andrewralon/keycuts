#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    // Keys on the Manager's selected row (#202). Sent with Press, which gives the event its own keyboard, so nothing here
    // depends on a key held on the machine that runs the tests.
    public partial class AccessibilityTests
    {
        /// <summary>Hosts a Manager of keycuts named <paramref name="names"/> and hands the pieces to <paramref name="body"/>.</summary>
        static void WithManager(string[] names, Action<Window, ManagerViewModel, RecordingActions, TextBox> body)
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, names);
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    body(window, manager, actions, search);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        #region Delete

        [Fact]
        public void Manager_Delete_RemovesTheSelectedRow_FromAnEmptySearchBox()
        {
            WithManager(new[] { "alpha", "bravo", "charlie" }, (window, manager, actions, search) =>
            {
                // Nothing selected: nothing to delete, and the key is left alone
                Assert.False(Press(search, Key.Delete));
                Assert.Empty(actions.Deleted);

                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.Down));
                Assert.Equal("bravo", manager.SelectedRow?.Name);

                Assert.True(Press(search, Key.Delete));

                Assert.Equal(new[] { @"C:\fixture\bravo.bat" }, actions.Deleted.Select(d => d.Keycut.FilePath));
                Assert.Equal(new[] { "alpha", "charlie" }, manager.Rows.Select(r => r.Name));
                Assert.True(manager.CanUndoDelete);
            });
        }

        // Delete means "delete a character" while there are characters in the box
        [Fact]
        public void Manager_Delete_IsLeftToTheSearchBox_WhileItHoldsText()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.Down));
                manager.SearchText = "a";
                Pump();

                Assert.False(Press(search, Key.Delete));
                Assert.Empty(actions.Deleted);
                Assert.Equal(2, manager.Rows.Count);

                manager.SearchText = string.Empty;
                Pump();
                Assert.True(Press(search, Key.Delete));
                Assert.Single(actions.Deleted);
            });
        }

        // A delete with a modifier is someone else's shortcut
        [Theory]
        [InlineData(ModifierKeys.Control)]
        [InlineData(ModifierKeys.Shift)]
        [InlineData(ModifierKeys.Alt)]
        public void Manager_Delete_WithAModifier_IsIgnored(ModifierKeys modifiers)
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.Down));

                Assert.False(Press(search, Key.Delete, modifiers));
                Assert.Empty(actions.Deleted);
            });
        }

        #endregion Delete

        #region Copy, edit, notepad, location

        [Fact]
        public void Manager_CtrlC_CopiesTheTarget_UnlessTheSearchBoxHasSelectedText()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                // Nothing selected in the table: Ctrl+C is the box's
                Assert.False(Press(search, Key.C, ModifierKeys.Control));
                Assert.Empty(actions.Copied);

                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.C, ModifierKeys.Control));
                Assert.Equal(new[] { @"C:\temp\alpha.txt" }, actions.Copied);
                Assert.Equal("Copied", manager.StatusMessage);

                // Text selected in the box (a search that still matches the selected row): it copies that
                search.Text = "alp";
                search.SelectAll();
                Assert.Equal("alpha", manager.SelectedRow?.Name);
                Assert.False(Press(search, Key.C, ModifierKeys.Control));
                Assert.Single(actions.Copied);

                // Text in the box but none selected: the row's target
                search.Select(0, 0);
                Assert.True(Press(search, Key.C, ModifierKeys.Control));
                Assert.Equal(2, actions.Copied.Count);
            });
        }

        [Fact]
        public void Manager_CtrlE_OpensTheEditForm()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var shown = new List<string>();
                manager.ShowEditForm = form => { shown.Add(form.Name); return false; };

                Assert.False(Press(search, Key.E, ModifierKeys.Control));
                Assert.Empty(shown);

                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.E, ModifierKeys.Control));

                Assert.Equal(new[] { "bravo" }, shown);
            });
        }

        // On a keycut the form can't edit, a key press says why instead of doing nothing (the menu item is greyed out)
        [Fact]
        public void Manager_CtrlE_OnAKeycutTheFormCantEdit_SaysWhy()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var noop = new RelayCommand(() => { });
                var keycuts = new List<Keycut>
                {
                    new Keycut { Name = "handmade", Kind = KeycutKind.File, RawType = ShortcutType.File, Target = @"C:\temp\h.txt", FilePath = @"C:\fixture\handmade.bat", CanEditInForm = false }
                };
                var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null, new RecordingActions())
                {
                    StatusMessageDuration = TimeSpan.FromHours(1)
                };
                manager.LoadAsync().GetAwaiter().GetResult();
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    var shown = 0;
                    manager.ShowEditForm = form => { shown++; return false; };
                    Assert.True(Press(search, Key.Down));

                    Assert.True(Press(search, Key.E, ModifierKeys.Control));

                    Assert.Equal(0, shown);
                    Assert.True(manager.StatusIsError);
                    Assert.Equal(EditKeycutViewModel.NotEditableMessage, manager.StatusMessage);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_CtrlShiftE_AndCtrlShiftO_OpenNotepadAndTheFileLocation()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Assert.False(Press(search, Key.E, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.False(Press(search, Key.O, ModifierKeys.Control | ModifierKeys.Shift));

                Assert.True(Press(search, Key.Down));
                Assert.True(Press(search, Key.E, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.True(Press(search, Key.O, ModifierKeys.Control | ModifierKeys.Shift));

                Assert.Equal(new[] { @"C:\fixture\alpha.bat" }, actions.Edited);
                Assert.Equal(new[] { @"C:\fixture\alpha.bat" }, actions.Located);
            });
        }

        // The row whose menu button has the focus is the row the user is on, as it is for the arrow keys
        [Fact]
        public void Manager_RowKeys_ActOnTheRowWhoseMenuButtonHasTheFocus()
        {
            WithManager(new[] { "alpha", "bravo", "charlie" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.Down));
                Assert.Equal("alpha", manager.SelectedRow?.Name);

                var button = Descendants<MenuButton>(window).Single(b => AutomationProperties.GetName(b) == "Actions for charlie");
                button.Focus();
                Keyboard.Focus(button);
                Pump();
                Assert.True(Keyboard.FocusedElement == button, "the menu button should have the keyboard focus");

                Assert.True(Press(button, Key.O, ModifierKeys.Control | ModifierKeys.Shift));

                Assert.Equal(new[] { @"C:\fixture\charlie.bat" }, actions.Located);
                Assert.Equal("charlie", manager.SelectedRow?.Name);
            });
        }

        #endregion Copy, edit, notepad, location

        #region Moving further

        [Fact]
        public void Manager_HomeAndEnd_MoveTheSelectionToTheFirstAndLastRow()
        {
            WithManager(new[] { "a", "b", "c", "d", "e" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.End));
                Assert.Equal("e", manager.SelectedRow?.Name);

                Assert.True(Press(search, Key.Home));
                Assert.Equal("a", manager.SelectedRow?.Name);

                // The row comes into view
                var table = Descendants<DataGrid>(window).Single();
                Assert.Same(manager.SelectedRow, table.SelectedItem);
            });
        }

        // Home and End move the caret in the box while it holds text
        [Fact]
        public void Manager_HomeAndEnd_AreLeftToTheSearchBox_WhileItHoldsText()
        {
            WithManager(new[] { "alpha", "alpine", "bravo" }, (window, manager, actions, search) =>
            {
                manager.SearchText = "al";
                Pump();
                Assert.True(Press(search, Key.Down));
                var selected = manager.SelectedRow;

                Assert.False(Press(search, Key.End));
                Assert.False(Press(search, Key.Home));
                Assert.Same(selected, manager.SelectedRow);
            });
        }

        [Fact]
        public void Manager_PageDownAndPageUp_MoveTheSelectionByAPage_EvenFromTheSearchBox()
        {
            var names = Enumerable.Range(1, 60).Select(i => "k" + i.ToString("00")).ToArray();

            WithManager(names, (window, manager, actions, search) =>
            {
                manager.SearchText = "k";
                Pump();

                Assert.True(Press(search, Key.PageDown));
                Assert.Equal("k01", manager.SelectedRow?.Name);

                Assert.True(Press(search, Key.PageDown));
                var firstPage = manager.Rows.IndexOf(manager.SelectedRow!);
                Assert.InRange(firstPage, 2, 40);

                Assert.True(Press(search, Key.PageDown));
                Assert.Equal(firstPage * 2, manager.Rows.IndexOf(manager.SelectedRow!));

                Assert.True(Press(search, Key.PageUp));
                Assert.Equal(firstPage, manager.Rows.IndexOf(manager.SelectedRow!));

                // It stops at the ends (End and Home are the box's while it holds text)
                manager.SearchText = string.Empty;
                Pump();
                Assert.True(Press(search, Key.End));
                Assert.True(Press(search, Key.PageDown));
                Assert.Equal("k60", manager.SelectedRow?.Name);
                Assert.True(Press(search, Key.Home));
                Assert.True(Press(search, Key.PageUp));
                Assert.Equal("k01", manager.SelectedRow?.Name);
            });
        }

        [Fact]
        public void Manager_MovingKeys_DoNothing_WhenThereAreNoRowsToMoveThrough()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                manager.SearchText = "zzz";
                Pump();

                Assert.False(Press(search, Key.PageDown));
                Assert.False(Press(search, Key.End));
                Assert.Null(manager.SelectedRow);
            });
        }

        #endregion Moving further

        #region Ctrl+F

        [Fact]
        public void Manager_CtrlF_FocusesTheSearchBox_AndSelectsWhatIsThere()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                manager.SearchText = "br";
                Pump();
                var button = Descendants<MenuButton>(window).First();
                button.Focus();
                Keyboard.Focus(button);
                Pump();
                Assert.False(search.IsKeyboardFocused);

                Assert.True(Press(button, Key.F, ModifierKeys.Control));

                Assert.True(search.IsKeyboardFocused);
                Assert.Equal("br", search.SelectedText);
            });
        }

        #endregion Ctrl+F

        #region When the keys don't apply

        [Fact]
        public void Manager_RowKeys_AreLeftToTheNameBox_WhileRenaming()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                manager.BeginRename(manager.Rows[0]);
                Pump();

                Assert.False(Press(search, Key.Delete));
                Assert.False(Press(search, Key.C, ModifierKeys.Control));
                Assert.False(Press(search, Key.E, ModifierKeys.Control));
                Assert.False(Press(search, Key.O, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.False(Press(search, Key.End));
                Assert.Empty(actions.Deleted);
                Assert.Empty(actions.Copied);
                Assert.Empty(actions.Located);
            });
        }

        [Fact]
        public void Manager_RowKeys_AreIgnored_WhileARowMenuIsOpen()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.Down));
                manager.IsMenuOpen = true;

                Assert.False(Press(search, Key.Delete));
                Assert.False(Press(search, Key.C, ModifierKeys.Control));
                Assert.False(Press(search, Key.End));
                Assert.Empty(actions.Deleted);
                Assert.Empty(actions.Copied);
            });
        }

        // Ctrl+Shift+Delete, Ctrl+Alt+E and the like belong to someone else
        [Theory]
        [InlineData(Key.Delete, ModifierKeys.Control | ModifierKeys.Shift)]
        [InlineData(Key.E, ModifierKeys.Control | ModifierKeys.Alt)]
        [InlineData(Key.C, ModifierKeys.Control | ModifierKeys.Shift)]
        [InlineData(Key.E, ModifierKeys.Shift)]
        [InlineData(Key.End, ModifierKeys.Shift)]
        public void Manager_OtherModifierCombinations_AreIgnored(Key key, ModifierKeys modifiers)
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                Assert.True(Press(search, Key.Down));

                Assert.False(Press(search, key, modifiers));
                Assert.Empty(actions.Deleted);
                Assert.Empty(actions.Copied);
                Assert.Empty(actions.Edited);
            });
        }

        #endregion When the keys don't apply

        #region The menu shows the keys

        [Fact]
        public void Manager_RowMenu_ShowsEachItemsKey()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                OpenRowMenuItem(window, "alpha", "Open keycut", out var menu);

                var keys = menu.Items.OfType<MenuItem>().ToDictionary(item => (string)item.Header, item => item.InputGestureText);
                menu.IsOpen = false;

                Assert.Equal("Enter", keys["Open keycut"]);
                Assert.Equal("Ctrl+E", keys["Edit…"]);
                Assert.Equal("Ctrl+Shift+E", keys["Edit in Notepad"]);
                Assert.Equal("Ctrl+Shift+O", keys["Open file location"]);
                Assert.Equal("Ctrl+C", keys["Copy target"]);
                Assert.Equal("F2", keys["Rename"]);
                Assert.Equal("Shift+F2", keys["Edit description"]);
                Assert.Equal("Delete", keys["Delete"]);
            });
        }

        #endregion The menu shows the keys
    }
}
