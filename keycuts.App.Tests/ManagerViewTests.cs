using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Mvvm;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The Manager's keys, in-place edits, row menu and Undo (WPF's AccessibilityTests.ManagerKeys, ManagerEditing, Footer and the
    /// Manager half of AccessibilityTests, ported). Keys go through Avalonia.Headless to whatever has the focus, the search box
    /// unless a test moves it, so a key the view leaves alone reaches the box as it would for a user.
    /// </summary>
    public class ManagerViewTests
    {
        #region Helpers

        internal sealed class RecordingActions : IKeycutActions
        {
            public List<string> Launched { get; } = new();

            public List<string> Edited { get; } = new();

            public List<string> Located { get; } = new();

            public List<string> Copied { get; } = new();

            public List<DeletedKeycut> Deleted { get; } = new();

            public List<DeletedKeycut> Restored { get; } = new();

            public List<string> Renamed { get; } = new();

            public List<(Keycut Keycut, string Description)> Described { get; } = new();

            public List<Keycut>? Disk { get; set; }

            public bool CanEdit { get; set; } = true;

            public void Launch(string path) => Launched.Add(path);

            public void Edit(string path) => Edited.Add(path);

            public void OpenFileLocation(string path) => Located.Add(path);

            public void CopyText(string text) => Copied.Add(text);

            public Task<DeletedKeycut> DeleteAsync(Keycut keycut)
            {
                var deleted = new DeletedKeycut(keycut, new byte[] { 1 }, default, true);
                Deleted.Add(deleted);
                return Task.FromResult(deleted);
            }

            public Task RestoreAsync(DeletedKeycut deleted)
            {
                Restored.Add(deleted);
                return Task.CompletedTask;
            }

            public Task<string> RenameAsync(Keycut keycut, string newName)
            {
                Renamed.Add(newName);
                return Task.FromResult(keycut.FilePath);
            }

            public Task<Keycut> SetDescriptionAsync(Keycut keycut, string description)
            {
                Described.Add((keycut, description));
                var saved = keycut with { Description = description.Trim() };
                if (Disk != null)
                {
                    Disk[Disk.FindIndex(k => k.FilePath == keycut.FilePath)] = saved;
                }

                return Task.FromResult(saved);
            }

            public Task<bool> CanEditInFormAsync(Keycut keycut) => Task.FromResult(CanEdit);

            public Task<Keycut> UpdateAsync(Keycut keycut, KeycutEdit edit)
            {
                return Task.FromResult(keycut with { Name = edit.Name, Target = edit.Target, Description = edit.Description });
            }
        }

        internal static ManagerViewModel LoadedManager(RecordingActions actions, params string[] names)
        {
            var noop = new RelayCommand(() => { });
            var keycuts = names.Select(name => new Keycut
            {
                Name = name,
                Kind = KeycutKind.File,
                RawType = ShortcutType.File,
                Target = @"C:\temp\" + name + ".txt",
                FilePath = @"C:\fixture\" + name + ".bat"
            }).ToList();
            actions.Disk = keycuts;

            // Every load reads the list as it is now, as the real one reads the folder
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts.ToList()), null, actions);
            manager.LoadAsync().GetAwaiter().GetResult();
            return manager;
        }

        static void WithManager(string[] names, Action<Window, ManagerViewModel, RecordingActions, TextBox> body, double width = 900, double height = 600)
        {
            ThemeManager.Apply(Palette.Light);
            var actions = new RecordingActions();
            var manager = LoadedManager(actions, names);
            var window = new Window { Width = width, Height = height, Content = new ManagerView { DataContext = manager } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var search = MainWindowTests.Named<TextBox>(window, "SearchBox");
            search.Focus();
            Dispatcher.UIThread.RunJobs();
            try
            {
                body(window, manager, actions, search);
            }
            finally
            {
                window.Close();
            }
        }

        static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            MainWindowTests.Press(window, key, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        static void Type(Window window, string text)
        {
            window.KeyTextInput(text);
            Dispatcher.UIThread.RunJobs();
        }

        static IInputElement? Focused(Window window)
        {
            return window.FocusManager!.GetFocusedElement();
        }

        static DataGridRow RowOf(Window window, string name)
        {
            return window.GetVisualDescendants().OfType<DataGridRow>().First(r => (r.DataContext as KeycutRowViewModel)?.Name == name);
        }

        static Point Center(Window window, Control control, double x = -1)
        {
            var at = control.TranslatePoint(new Point(x >= 0 ? x : control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            return at;
        }

        #endregion

        #region Selection keys

        [AvaloniaFact]
        public void UpAndDown_SelectRows_FromTheSearchBox_AndEnterOpensTheSelection()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Press(window, Key.Down);
                Press(window, Key.Down);
                Assert.Equal("bravo", manager.SelectedRow?.Name);
                Press(window, Key.Up);
                Assert.Equal("alpha", manager.SelectedRow?.Name);
                Assert.Same(search, Focused(window));
                Assert.Same(manager.SelectedRow, MainWindowTests.Named<DataGrid>(window, "Table").SelectedItem);

                Press(window, Key.Enter);
                Assert.Equal(new[] { @"C:\fixture\alpha.bat" }, actions.Launched);
            });
        }

        [AvaloniaFact]
        public void HomeAndEnd_MoveTheSelection_ButAreLeftToTheSearchBox_WhileItHoldsText()
        {
            WithManager(new[] { "a", "b", "c", "d", "e" }, (window, manager, actions, search) =>
            {
                Press(window, Key.End);
                Assert.Equal("e", manager.SelectedRow?.Name);
                Press(window, Key.Home);
                Assert.Equal("a", manager.SelectedRow?.Name);

                Type(window, "e");
                Assert.Equal("e", manager.SearchText);
                Press(window, Key.Down);
                var selected = manager.SelectedRow;
                Press(window, Key.Home);
                Assert.Same(selected, manager.SelectedRow);
            });
        }

        [AvaloniaFact]
        public void PageDownAndPageUp_MoveTheSelectionByAPage()
        {
            var names = Enumerable.Range(1, 60).Select(i => "k" + i.ToString("00")).ToArray();
            WithManager(names, (window, manager, actions, search) =>
            {
                Press(window, Key.PageDown);
                Assert.Equal("k01", manager.SelectedRow?.Name);
                Press(window, Key.PageDown);
                var page = manager.Rows.IndexOf(manager.SelectedRow!);
                Assert.InRange(page, 2, 40);
                Press(window, Key.PageDown);
                Assert.Equal(page * 2, manager.Rows.IndexOf(manager.SelectedRow!));
                Press(window, Key.PageUp);
                Assert.Equal(page, manager.Rows.IndexOf(manager.SelectedRow!));
            });
        }

        [AvaloniaFact]
        public void CtrlF_FocusesTheSearchBox_AndSelectsWhatIsThere()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                manager.SearchText = "br";
                Dispatcher.UIThread.RunJobs();
                search.SelectionStart = search.SelectionEnd = 0;
                Press(window, Key.F, PlatformKeys.RawCommand);
                Assert.Same(search, Focused(window));
                Assert.Equal("br", search.SelectedText);
            });
        }

        #endregion

        #region Row keys

        [AvaloniaFact]
        public void Delete_RemovesTheSelectedRow_FromAnEmptySearchBox_AndCtrlZUndoesIt()
        {
            WithManager(new[] { "alpha", "bravo", "charlie" }, (window, manager, actions, search) =>
            {
                // Nothing selected: nothing to delete
                Press(window, Key.Delete);
                Assert.Empty(actions.Deleted);

                Press(window, Key.Down);
                Press(window, Key.Down);
                Press(window, Key.Delete);
                Assert.Equal(new[] { @"C:\fixture\bravo.bat" }, actions.Deleted.Select(d => d.Keycut.FilePath));
                Assert.Equal(new[] { "alpha", "charlie" }, manager.Rows.Select(r => r.Name));

                // #179: Undo shows with the message, and Ctrl+Z does the same from the search box
                var undo = MainWindowTests.Named<Button>(window, "UndoButton");
                Assert.True(undo.IsVisible);
                Assert.Equal("undo delete", AutomationProperties.GetName(undo));
                Press(window, Key.Z, PlatformKeys.RawCommand);
                Assert.Single(actions.Restored);
            });
        }

        [AvaloniaFact]
        public void Delete_IsLeftToTheSearchBox_WhileItHoldsText_AndIgnoredWithAModifier()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Press(window, Key.Down);
                Press(window, Key.Delete, RawInputModifiers.Shift);
                Press(window, Key.Delete, PlatformKeys.RawCommand);
                Assert.Empty(actions.Deleted);

                Type(window, "a");
                search.CaretIndex = 0;
                Press(window, Key.Delete);
                Assert.Empty(actions.Deleted);
                Assert.Equal(string.Empty, manager.SearchText);
            });
        }

        // macOS (docs/styleguide.md, Keys): ⌘⌫ moves to the Trash, as Finder does; a plain ⌫ never deletes; ⌘↑ and ⌘↓ jump
        [AvaloniaFact]
        public void OnTheMac_CommandBackspaceDeletes_ABackspaceDoesnt_AndCommandArrowsJump()
        {
            Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS keys");
            WithManager(new[] { "alpha", "bravo", "charlie" }, (window, manager, actions, search) =>
            {
                Press(window, Key.Down, RawInputModifiers.Meta);
                Assert.Equal("charlie", manager.SelectedRow!.Name);
                Press(window, Key.Up, RawInputModifiers.Meta);
                Assert.Equal("alpha", manager.SelectedRow!.Name);

                Press(window, Key.Back);
                Assert.Empty(actions.Deleted);
                Press(window, Key.Back, RawInputModifiers.Meta);
                Assert.Equal(new[] { "bravo", "charlie" }, manager.Rows.Select(r => r.Name));
            });
        }

        [AvaloniaFact]
        public void CtrlC_CopiesTheTarget_UnlessTheSearchBoxHasSelectedText()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Press(window, Key.C, PlatformKeys.RawCommand);
                Assert.Empty(actions.Copied);

                Press(window, Key.Down);
                Press(window, Key.C, PlatformKeys.RawCommand);
                Assert.Equal(new[] { @"C:\temp\alpha.txt" }, actions.Copied);
                Assert.Equal("Copied", manager.StatusMessage);

                Type(window, "al");
                search.SelectAll();
                Press(window, Key.C, PlatformKeys.RawCommand);
                Assert.Single(actions.Copied);
            });
        }

        [AvaloniaFact]
        public void CtrlE_OpensTheEditForm_AndCtrlShiftEAndO_OpenNotepadAndTheFileLocation()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var shown = new List<string>();
                manager.ShowEditForm = form =>
                {
                    shown.Add(form.Name);
                    return false;
                };

                Press(window, Key.E, PlatformKeys.RawCommand);
                Assert.Empty(shown);

                Press(window, Key.Down);
                Press(window, Key.Down);
                Press(window, Key.E, PlatformKeys.RawCommand);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new[] { "bravo" }, shown);

                Press(window, Key.E, PlatformKeys.RawCommand | RawInputModifiers.Shift);
                Press(window, Key.O, PlatformKeys.RawCommand | RawInputModifiers.Shift);
                Assert.Equal(new[] { @"C:\fixture\bravo.bat" }, actions.Edited);
                Assert.Equal(new[] { @"C:\fixture\bravo.bat" }, actions.Located);
            });
        }

        [AvaloniaFact]
        public void CtrlE_OnAKeycutTheFormCantEdit_SaysWhy()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                actions.CanEdit = false;
                var shown = 0;
                manager.ShowEditForm = form =>
                {
                    shown++;
                    return false;
                };

                Press(window, Key.Down);
                Press(window, Key.E, PlatformKeys.RawCommand);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(0, shown);
                Assert.True(manager.StatusIsError);
                Assert.Equal(EditKeycutViewModel.NotEditableMessage, manager.StatusMessage);
            });
        }

        #endregion

        #region In-place edits

        [AvaloniaFact]
        public void F2_RenamesTheSelectedRowInPlace_EscapeCancels_AndEnterSaves()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                Press(window, Key.Down);
                Press(window, Key.F2);
                Dispatcher.UIThread.RunJobs();
                Assert.True(manager.IsRenaming);
                var box = Assert.IsType<TextBox>(Focused(window));
                Assert.Contains("RenameBox", box.Classes);
                Assert.Equal("alpha", box.SelectedText);

                // The row keys step aside while the name is edited
                Press(window, Key.Down);
                Assert.Equal("alpha", manager.SelectedRow?.Name);

                Press(window, Key.Escape);
                Assert.False(manager.IsRenaming);
                Assert.Empty(actions.Renamed);

                Press(window, Key.F2);
                Dispatcher.UIThread.RunJobs();
                Type(window, "apple");
                Press(window, Key.Enter);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new[] { "apple" }, actions.Renamed);
                Assert.False(manager.IsRenaming);
            });
        }

        [AvaloniaFact]
        public void ShiftF2_EditsTheDescriptionInPlace_EnterSaves_EscapeCancels()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                Press(window, Key.Down);
                Press(window, Key.F2, RawInputModifiers.Shift);
                Dispatcher.UIThread.RunJobs();
                Assert.True(manager.IsEditingDescription);
                var box = Assert.IsType<TextBox>(Focused(window));
                Assert.Contains("DescriptionBox", box.Classes);

                Press(window, Key.Escape);
                Assert.False(manager.IsEditingDescription);
                Assert.Empty(actions.Described);

                Press(window, Key.F2, RawInputModifiers.Shift);
                Dispatcher.UIThread.RunJobs();
                Type(window, "notes");
                Press(window, Key.Enter);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("notes", Assert.Single(actions.Described).Description);
            });
        }

        // #202: a click on the name of the row that is already selected edits it; a click on another row only selects it
        [AvaloniaFact]
        public void AClickOnTheNameOfTheSelectedRow_EditsItInPlace_AndAClickElsewhereSaves()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var alpha = RowOf(window, "alpha");
                window.MouseDown(Center(window, alpha, 40), MouseButton.Left);
                window.MouseUp(Center(window, alpha, 40), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("alpha", manager.SelectedRow?.Name);
                Assert.False(manager.IsRenaming);

                window.MouseDown(Center(window, alpha, 40), MouseButton.Left);
                window.MouseUp(Center(window, alpha, 40), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.True(manager.IsRenaming);

                Type(window, "apple");
                var bravo = RowOf(window, "bravo");
                window.MouseDown(Center(window, bravo, 40), MouseButton.Left);
                window.MouseUp(Center(window, bravo, 40), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new[] { "apple" }, actions.Renamed);
            });
        }

        #endregion

        #region Row menu

        [AvaloniaFact]
        public void TheRowMenu_ShowsEachItemsKey_AndIsNamedAfterItsKeycut()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                // Named once it opens, which is when a screen reader reads it
                var row = RowOf(window, "alpha");
                var menu = row.ContextMenu!;
                menu.Open(row);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(manager.Rows[0].ActionsLabel, AutomationProperties.GetName(menu));
                menu.Close();
                var expected = new[]
                {
                    ("Open keycut", "Enter"), ("Edit…", "Ctrl+E"), ("Edit in Notepad", "Ctrl+Shift+E"), ("Open file location", "Ctrl+Shift+O"),
                    ("Copy target", "Ctrl+C"), ("Rename", "F2"), ("Edit description", "Shift+F2"), ("Delete", "Delete")
                };
                Assert.Equal(expected.Select(e => (e.Item1, KeyGesture.Parse(e.Item2))), menu.Items.OfType<MenuItem>().Select(i => ((string)i.Header!, i.InputGesture!)));
            });
        }

        [AvaloniaFact]
        public void RightClickingARow_OpensItsOwnMenu_AndSelectsIt_AndTheRowKeysWaitMeanwhile()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var bravo = RowOf(window, "bravo");
                window.MouseDown(Center(window, bravo), MouseButton.Right);
                window.MouseUp(Center(window, bravo), MouseButton.Right);
                Dispatcher.UIThread.RunJobs();

                Assert.True(bravo.ContextMenu!.IsOpen);
                Assert.True(manager.IsMenuOpen);
                Assert.Equal("bravo", manager.SelectedRow?.Name);
                Assert.NotSame(RowOf(window, "alpha").ContextMenu, bravo.ContextMenu);

                bravo.ContextMenu.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.False(manager.IsMenuOpen);
            });
        }

        #endregion

        #region The search row

        [AvaloniaFact]
        public void TheCount_IsALiveLine_BesideTheSearch_AndFollowsTheSearch()
        {
            WithManager(new[] { "alpha", "bravo" }, (window, manager, actions, search) =>
            {
                var count = MainWindowTests.Named<TextBlock>(window, "CountText");
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(count));
                Assert.Equal("2 keycuts", count.Text);
                Type(window, "al");
                Assert.Equal("1 of 2 keycuts", count.Text);
            });
        }

        // The message is a live line: there while it is empty, but it takes no room then
        [AvaloniaFact]
        public void TheStatusMessage_TakesNoRoom_WhileThereIsNone()
        {
            WithManager(new[] { "alpha" }, (window, manager, actions, search) =>
            {
                var status = window.GetVisualDescendants().OfType<ManagerStatusView>().Single();
                var message = MainWindowTests.Named<TextBlock>(window, "StatusMessageText");
                Assert.True(message.IsVisible);
                Assert.Equal(0, status.Bounds.Width, 0.5);

                Press(window, Key.Down);
                Press(window, Key.C, PlatformKeys.RawCommand);
                Dispatcher.UIThread.RunJobs();
                Assert.True(status.Bounds.Width > 0);
            });
        }

        #endregion
    }
}
