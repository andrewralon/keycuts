#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    // The Manager's create row: the Creator's fields in one row above the search (docs/styleguide.md). Keys are sent with Press, which
    // gives the event its own keyboard.
    public partial class AccessibilityTests
    {
        sealed class RowCreates
        {
            public List<(string Target, string Name, string Description)> Calls { get; } = new List<(string, string, string)>();

            public Task<CreateResult> Create(string target, string name, string description, bool useEnvironmentVariables, bool replace, CancellationToken ct)
            {
                Calls.Add((target, name, description));
                return Task.FromResult(CreateResult.Success(null!));
            }
        }

        sealed class CreateRowParts
        {
            public CreateRowParts(Window window, ShellViewModel shell)
            {
                Window = window;
                Shell = shell;
                Row = Descendants<CreateRowView>(window).Single();
                Target = Named<TextBox>(Row, "Target");
                Name = Named<TextBox>(Row, "keycut name");
                Description = Named<TextBox>(Row, "Description");
                Browse = Named<Button>(Row, "Browse");
                Create = Named<Button>(Row, "Create");
                Search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
            }

            public Window Window { get; }

            public ShellViewModel Shell { get; }

            public CreatorViewModel Form
            {
                get { return Shell.Manager.CreateRow!; }
            }

            public CreateRowView Row { get; }

            public TextBox Target { get; }

            public TextBox Name { get; }

            public TextBox Description { get; }

            public Button Browse { get; }

            public Button Create { get; }

            public TextBox Search { get; }

            private static T Named<T>(DependencyObject root, string name)
                where T : FrameworkElement
            {
                return Descendants<T>(root).Single(e => AutomationProperties.GetName(e) == name);
            }
        }

        /// <summary>A shell's Manager, with one keycut and a create row that creates through a fake, hosted in a window.</summary>
        static void WithCreateRow(Action<CreateRowParts, RecordingActions, RowCreates, Func<int>> body)
        {
            RunSta(() =>
            {
                EnsureApplication();
                // As in the app, an await resumes on this thread: resolving the target and creating run off it
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                var actions = new RecordingActions();
                var creates = new RowCreates();
                var loads = 0;
                var keycuts = new List<Keycut>
                {
                    new Keycut { Name = "alpha", Kind = KeycutKind.File, RawType = ShortcutType.File, Target = @"C:\temp\alpha.txt", FilePath = @"C:\fixture\alpha.bat" }
                };
                actions.Disk = keycuts;
                var shell = new ShellViewModel(
                    () => Task.FromResult(keycuts.Count),
                    keycutLoader: ct =>
                    {
                        loads++;
                        return Task.FromResult<IReadOnlyList<Keycut>>(keycuts.ToList());
                    },
                    actions: actions,
                    createAsync: creates.Create,
                    checkName: TestNameChecks.NoWarnings);
                shell.ShowManager();
                Pump();
                var window = Host(new ManagerView(), shell.Manager);
                try
                {
                    body(new CreateRowParts(window, shell), actions, creates, () => loads);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void ManagerCreateRow_Browse_OpensItsMenuUnderTheButton_RightEdgesLinedUp()
        {
            WithCreateRow((parts, actions, creates, loads) =>
            {
                parts.Browse.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump();

                // A custom placement, which Windows' menu side (right-handed on touch PCs) doesn't move a menu's width to the left
                var menu = parts.Browse.ContextMenu;
                try
                {
                    Assert.True(menu.IsOpen);
                    Assert.Same(parts.Browse, menu.PlacementTarget);
                    Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.Custom, menu.Placement);
                    Assert.Same(keycuts.GUI.Controls.MenuPlacement.UnderRightEdge, menu.CustomPopupPlacementCallback);
                    Assert.Equal(0, menu.HorizontalOffset);
                }
                finally
                {
                    menu.IsOpen = false;
                }
            });
        }

        [Fact]
        public void ManagerCreateRow_IsTargetNameDescriptionCreate_LeftToRight_AboveTheSearch()
        {
            WithCreateRow((parts, actions, creates, loads) =>
            {
                Point At(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), parts.Window);

                var order = new FrameworkElement[] { parts.Target, parts.Browse, parts.Name, parts.Description, parts.Create };
                for (var i = 1; i < order.Length; i++)
                {
                    Assert.True(At(order[i]).X > At(order[i - 1]).X, $"{AutomationProperties.GetName(order[i])} is right of the one before it");
                }

                // One line: the controls share a row, 36 high
                Assert.All(new FrameworkElement[] { parts.Name, parts.Description, parts.Create, parts.Browse }, e => Assert.Equal(36, e.ActualHeight, 0.5));
                Assert.Equal(At(parts.Name).Y, At(parts.Description).Y, 0.5);
                Assert.Equal(At(parts.Name).Y, At(parts.Create).Y, 0.5);

                // The row sits above the search box, 16 apart, and the search box still has the focus when the Manager opens
                var rowBottom = At(parts.Row).Y + parts.Row.ActualHeight;
                Assert.Equal(rowBottom + 16, At(parts.Search).Y, 0.5);
                Assert.True(parts.Search.IsKeyboardFocused);

                // The description is always there, with no link to show it
                Assert.Equal(Visibility.Visible, parts.Description.Visibility);
                Assert.True(parts.Form.IsDescriptionVisible);
            });
        }

        [Fact]
        public void ManagerCreateRow_EnterInTheName_IsTheRows_AndCreatesThere()
        {
            WithCreateRow((parts, actions, creates, loads) =>
            {
                parts.Shell.Manager.MoveSelection(1);
                Pump();
                Assert.NotNull(parts.Shell.Manager.SelectedRow);

                parts.Form.Target = "https://example.com";
                parts.Form.TargetKind = KeycutKind.Url;
                parts.Form.ShortcutName = "ex";
                parts.Form.Description = "Example";
                Keyboard.Focus(parts.Name);
                Pump();

                // The Manager doesn't take Enter to open its selected keycut; the name box's own Enter is Create
                Assert.False(Press(parts.Name, Key.Enter));
                Assert.Empty(actions.Launched);
                Assert.Contains(parts.Name.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.Return && ReferenceEquals(b.Command, parts.Form.CreateCommand));
                Assert.Contains(parts.Description.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.Return && ReferenceEquals(b.Command, parts.Form.CreateCommand));

                var before = loads();
                parts.Create.Command.Execute(null);
                PumpUntil(() => creates.Calls.Count == 1 && !parts.Form.IsSaving);
                Pump();

                Assert.Equal(new[] { ("https://example.com", "ex", "Example") }, creates.Calls);
                // The table reloads to show it, and the row is empty for the next one, with Created by the name
                Assert.True(loads() > before);
                Assert.Equal(string.Empty, parts.Target.Text);
                Assert.Equal(string.Empty, parts.Name.Text);
                Assert.Equal(string.Empty, parts.Description.Text);
                Assert.True(parts.Form.IsSuccess);
                // Logical focus: keyboard focus also needs the window to be active, which a parallel test run can take away
                PumpUntil(() => parts.Target.IsFocused);
                Assert.True(parts.Target.IsFocused);
            });
        }

        [Fact]
        public void ManagerCreateRow_EnterInTheTarget_CreatesWhenTheNameIsFilled_ElseMovesToTheName()
        {
            WithCreateRow((parts, actions, creates, loads) =>
            {
                // No name yet: Enter resolves the target and moves on to the name
                parts.Form.Target = "https://example.com";
                Keyboard.Focus(parts.Target);
                Pump();
                Assert.True(Press(parts.Target, Key.Enter));
                PumpUntil(() => parts.Name.IsFocused);
                Assert.True(parts.Name.IsFocused);
                Assert.Empty(creates.Calls);

                // With one, the same Enter makes the keycut
                parts.Form.ShortcutName = "ex";
                Keyboard.Focus(parts.Target);
                Pump();
                Assert.True(Press(parts.Target, Key.Enter));
                PumpUntil(() => creates.Calls.Count == 1);
                Assert.Single(creates.Calls);
                Assert.StartsWith("https://example.com", creates.Calls[0].Target);
                Assert.Equal("ex", creates.Calls[0].Name);
                Assert.Empty(actions.Launched);
            });
        }

        [Fact]
        public void ManagerCreateRow_DeleteArrowsAndHome_EditItsText_NotTheTable()
        {
            WithCreateRow((parts, actions, creates, loads) =>
            {
                parts.Shell.Manager.MoveSelection(1);
                Pump();
                var selected = parts.Shell.Manager.SelectedRow;
                Assert.NotNull(selected);
                Keyboard.Focus(parts.Name);
                Pump();

                // An empty name box would otherwise let Delete delete the selected keycut
                foreach (var key in new[] { Key.Delete, Key.Down, Key.Up, Key.Home, Key.End, Key.F2 })
                {
                    Assert.False(Press(parts.Name, key), key + " is the text box's");
                }

                Assert.Empty(actions.Deleted);
                Assert.Same(selected, parts.Shell.Manager.SelectedRow);
                Assert.False(parts.Shell.Manager.IsEditing);

                // Ctrl+F still goes to the search
                Assert.True(Press(parts.Name, Key.F, ModifierKeys.Control));
                Assert.True(parts.Search.IsKeyboardFocused);
            });
        }

        [Fact]
        public void ManagerCreateRow_IsAFormOfItsOwn_ApartFromTheCreators()
        {
            WithCreateRow((parts, actions, creates, loads) =>
            {
                parts.Form.ShortcutName = "row";
                Assert.Equal(string.Empty, parts.Shell.Creator.ShortcutName);
                Assert.NotSame(parts.Shell.Creator, parts.Form);
                Assert.Equal(new[] { parts.Shell.Creator, parts.Form }, parts.Shell.Forms);
                Assert.False(parts.Shell.Creator.ClearsAfterCreate);
                Assert.False(parts.Shell.Creator.DescriptionAlwaysShown);
            });
        }

        [Fact]
        public void AManagerWithoutACreateRow_BuildsNone()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = LoadedManager(new RecordingActions(), "alpha");
                var window = Host(new ManagerView(), manager);
                try
                {
                    Assert.Empty(Descendants<CreateRowView>(window));
                    Assert.Equal(Visibility.Collapsed, Descendants<ContentControl>(window).Single(c => c.Name == "CreateRowHost").Visibility);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
