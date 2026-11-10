#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    public class AccessibilityTests
    {
        static CreatorViewModel NewCreator()
        {
            var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), nameExists: _ => false);
            creator.Destination = "https://example.com";
            creator.DestinationKind = KeycutKind.Url;
            creator.ShortcutName = "ex";
            return creator;
        }

        #region View model text for screen readers

        [Fact]
        public void NameHelpText_ReflectsNameAndSubmitErrorsOnly()
        {
            var creator = NewCreator();
            Assert.Equal(string.Empty, creator.NameHelpText);

            creator.SetDuplicateName("ex");
            Assert.Equal(creator.HelperText, creator.NameHelpText);
            Assert.Contains("already exists", creator.NameHelpText);

            creator.ShortcutName = "ex2";
            Assert.Equal(string.Empty, creator.NameHelpText);
        }

        [Fact]
        public void DestinationHelpText_ReflectsTheDestinationError_AndWinsOverNameHelp()
        {
            var creator = NewCreator();
            Assert.Equal(string.Empty, creator.DestinationHelpText);

            creator.SetDuplicateName("ex");
            creator.DestinationError = DestinationError.NotFound;

            Assert.Equal(CreatorViewModel.GetDestinationErrorMessage(DestinationError.NotFound), creator.DestinationHelpText);
            Assert.Equal(string.Empty, creator.NameHelpText);
        }

        [Fact]
        public void Saving_IsAnnouncedThroughTheHelperLineAndTheCreateButtonName()
        {
            var creator = NewCreator();
            Assert.Equal("Create", creator.CreateButtonName);

            var changed = new List<string?>();
            creator.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);
            creator.IsSaving = true;

            Assert.Equal("Saving", creator.CreateButtonName);
            Assert.Equal(CreatorViewModel.SavingHelperText, creator.HelperText);
            Assert.False(creator.HelperIsError);
            Assert.Equal(string.Empty, creator.NameHelpText);
            Assert.Contains(nameof(CreatorViewModel.CreateButtonName), changed);
            Assert.Contains(nameof(CreatorViewModel.HelperText), changed);

            creator.IsSaving = false;
            Assert.Equal("Create", creator.CreateButtonName);
        }

        #endregion

        #region Loaded views (STA)

        static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw new Exception("STA test failed: " + failure, failure);
            }
        }

        static void EnsureApplication()
        {
            if (Application.Current != null)
            {
                return;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var name in new[] { "Light", "Icons", "Controls" })
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/keycuts.GUI;component/Themes/" + name + ".xaml")
                });
            }
        }

        static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        static Window Host(FrameworkElement view, object viewModel)
        {
            var window = new Window
            {
                Content = view,
                DataContext = viewModel,
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            window.Show();
            Pump();
            window.UpdateLayout();
            return window;
        }

        static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        // Tab visits controls in document order (no TabIndex is used), so the visual tree order is the tab order.
        static List<string> TabStops(DependencyObject root)
        {
            return Descendants<Control>(root)
                .Where(c => c.IsTabStop && c.Focusable && c.IsEnabled && c.IsVisible && c is not ScrollViewer)
                .Select(c => AutomationProperties.GetName(c) is { Length: > 0 } name ? name : c.GetType().Name + ":" + DescribeContent(c))
                .ToList();
        }

        static string DescribeContent(Control c)
        {
            return c is ContentControl { Content: string s } ? s : string.Empty;
        }

        [Fact]
        public void Creator_TabOrderFollowsTheVisualOrder_AndSkipsDisabledCreate()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var creator = NewCreator();
                creator.ShortcutName = string.Empty;
                var window = Host(new CreatorView(), creator);
                try
                {
                    var stops = TabStops(window);
                    Assert.Equal(new[] { "Destination", "Browse", "Keycut name", "View all keycuts (0)", "Settings" }, stops);

                    creator.ShortcutName = "ex";
                    Pump();
                    stops = TabStops(window);
                    Assert.Equal(new[] { "Destination", "Browse", "Keycut name", "Create", "View all keycuts (0)", "Settings" }, stops);

                    creator.IsSaving = true;
                    Pump();
                    Assert.Equal(new[] { "View all keycuts (0)", "Settings" }, TabStops(window));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Creator_WelcomePanel_ComesFirstInTabOrder_WithNamedControls_AndHidesOnDismiss()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var welcome = new WelcomeViewModel(@"C:\keycuts\keycuts.GUI.exe", () => false, () => { },
                    (destination, name, ct) => Task.FromResult(CreateResult.Success(null!)));
                var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), nameExists: _ => false) { Welcome = welcome };
                creator.Destination = "https://example.com";
                creator.DestinationKind = KeycutKind.Url;
                creator.ShortcutName = "ex";
                welcome.OnKeycutCountLoaded(0);

                var window = Host(new CreatorView(), creator);
                try
                {
                    Assert.Equal(
                        new[] { "Name for the keycuts keycut", "Create keycut to keycuts", "Dismiss welcome", "Destination", "Browse", "Keycut name", "Create", "View all keycuts (0)", "Settings" },
                        TabStops(window));

                    welcome.Dismiss();
                    Pump();
                    Assert.Equal(new[] { "Destination", "Browse", "Keycut name", "Create", "View all keycuts (0)", "Settings" }, TabStops(window));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // WPF's default Label and CheckBox styles set the system control-text color, so they don't inherit the window's Foreground.
        [Fact]
        public void LabelAndCheckBox_UseThePaletteTextColor()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var label = new Label { Content = "Output Folder" };
                var checkBox = new CheckBox { Content = "Force Overwrite" };
                var panel = new StackPanel();
                panel.Children.Add(label);
                panel.Children.Add(checkBox);
                var window = Host(panel, new object());
                try
                {
                    var expected = ((SolidColorBrush)Application.Current.FindResource("TextBrush")).Color;
                    Assert.Equal(expected, ((SolidColorBrush)label.Foreground).Color);
                    Assert.Equal(expected, ((SolidColorBrush)checkBox.Foreground).Color);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void CheckBox_DrawsItsStateFromThePalette()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var checkBox = new CheckBox { Content = "Force Overwrite" };
                var window = Host(checkBox, new object());
                try
                {
                    var box = (Border)checkBox.Template.FindName("Box", checkBox);
                    var mark = (Icon)checkBox.Template.FindName("Mark", checkBox);
                    var surface = ((SolidColorBrush)Application.Current.FindResource("SurfaceBrush")).Color;
                    var accent = ((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color;

                    Assert.Equal(surface, ((SolidColorBrush)box.Background).Color);
                    Assert.Equal(Visibility.Collapsed, mark.Visibility);

                    checkBox.IsChecked = true;
                    Pump();
                    Assert.Equal(accent, ((SolidColorBrush)box.Background).Color);
                    Assert.Equal(Visibility.Visible, mark.Visibility);

                    Assert.Same(Application.Current.FindResource("AppFocusVisual"), checkBox.FocusVisualStyle);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void RadioButton_DrawsItsStateFromThePalette()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var radio = new RadioButton { Content = "Dark" };
                var window = Host(radio, new object());
                try
                {
                    var box = (Border)radio.Template.FindName("Box", radio);
                    var mark = (System.Windows.Shapes.Ellipse)radio.Template.FindName("Mark", radio);
                    var text = ((SolidColorBrush)Application.Current.FindResource("TextBrush")).Color;
                    var accent = ((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color;

                    Assert.Equal(text, ((SolidColorBrush)radio.Foreground).Color);
                    Assert.Equal(Visibility.Collapsed, mark.Visibility);

                    radio.IsChecked = true;
                    Pump();
                    Assert.Equal(accent, ((SolidColorBrush)box.Background).Color);
                    Assert.Equal(Visibility.Visible, mark.Visibility);

                    Assert.Same(Application.Current.FindResource("AppFocusVisual"), radio.FocusVisualStyle);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        static string FocusedName()
        {
            var focused = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
            return focused == null ? "(nothing focused)" : AutomationProperties.GetName(focused);
        }

        [Fact]
        public void Creator_LaunchFocus_GoesToTheDestinationOrTheName()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var empty = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), nameExists: _ => false);
                var window = Host(new CreatorView(), empty);
                try
                {
                    Assert.Equal("Destination", FocusedName());
                }
                finally
                {
                    window.Close();
                }

                var withPath = Host(new CreatorView(), NewCreator());
                try
                {
                    Assert.Equal("Keycut name", FocusedName());
                }
                finally
                {
                    withPath.Close();
                }
            });
        }

        [Fact]
        public void Creator_HeadingAndErrorLiveRegionAreExposed()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var creator = NewCreator();
                var window = Host(new CreatorView(), creator);
                try
                {
                    var heading = Descendants<TextBlock>(window).Single(t => t.Text == "Launch anything instantly");
                    Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(heading));

                    var helper = Descendants<TextBlock>(window).Single(t => t.Name == "HelperTextBlock");
                    Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(helper));

                    creator.SetDuplicateName("ex");
                    Pump();
                    Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(helper));

                    var name = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Keycut name");
                    Assert.Contains("already exists", AutomationProperties.GetHelpText(name));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        class RecordingActions : IKeycutActions
        {
            public List<string> Launched { get; } = new List<string>();

            public void Launch(string path)
            {
                Launched.Add(path);
            }

            public void Edit(string path)
            {
            }

            public void OpenFileLocation(string path)
            {
            }

            public void CopyText(string text)
            {
            }

            public Task DeleteAsync(Keycut keycut)
            {
                return Task.CompletedTask;
            }

            public List<string> Renamed { get; } = new List<string>();

            public Task<string> RenameAsync(Keycut keycut, string newName)
            {
                Renamed.Add(newName);
                return Task.FromResult(keycut.FilePath);
            }
        }

        static ManagerViewModel LoadedManager(IKeycutActions actions, params string[] names)
        {
            var noop = new RelayCommand(() => { });
            var keycuts = names.Select(name => new Keycut
            {
                Name = name,
                Kind = KeycutKind.File,
                RawType = ShortcutType.File,
                Destination = @"C:\temp\" + name + ".txt",
                FilePath = @"C:\fixture\" + name + ".bat"
            }).ToList();
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null, actions);
            manager.LoadAsync().GetAwaiter().GetResult();
            return manager;
        }

        static bool Press(UIElement target, Key key)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            target.RaiseEvent(args);
            Pump();
            return args.Handled;
        }

        [Fact]
        public void Manager_UpAndDownSelectRowsFromTheSearchBox_AndEnterOpensTheSelection()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, "alpha", "bravo", "charlie");
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search shortcuts");
                    var table = Descendants<DataGrid>(window).Single();

                    Assert.False(Press(search, Key.Enter));
                    Assert.Empty(actions.Launched);

                    Assert.True(Press(search, Key.Down));
                    Assert.True(Press(search, Key.Down));
                    Assert.Equal("bravo", manager.SelectedRow?.Name);
                    Assert.Same(manager.SelectedRow, table.SelectedItem);

                    Assert.True(Press(search, Key.Up));
                    Assert.Equal("alpha", manager.SelectedRow?.Name);

                    Assert.True(Press(search, Key.Enter));
                    Assert.Equal(new[] { @"C:\fixture\alpha.bat" }, actions.Launched);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_F2RenamesTheSelectedRowInPlace_EscapeCancels_AndEnterSaves()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search shortcuts");
                    Assert.True(Press(search, Key.Down));
                    Assert.True(Press(search, Key.Down));

                    Assert.True(Press(search, Key.F2));
                    Pump();
                    var box = Descendants<TextBox>(window).Single(t => t.IsVisible && AutomationProperties.GetName(t) == "New name for bravo");
                    Assert.Equal("bravo", box.Text);
                    Assert.True(box.IsKeyboardFocusWithin);

                    // The arrows stay in the box instead of moving the selection, and Escape cancels without leaving the manager
                    Assert.True(Press(box, Key.Up));
                    Assert.Equal("bravo", manager.SelectedRow?.Name);
                    Assert.True(Press(box, Key.Escape));
                    Assert.False(manager.IsRenaming);
                    Assert.Empty(actions.Renamed);

                    Assert.True(Press(search, Key.F2));
                    Pump();
                    box = Descendants<TextBox>(window).Single(t => t.IsVisible && AutomationProperties.GetName(t) == "New name for bravo");
                    box.Text = "zulu";
                    Assert.True(Press(box, Key.Enter));
                    Assert.Equal(new[] { "zulu" }, actions.Renamed);
                    Assert.False(manager.IsRenaming);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_SelectedRowIsMarkedWithMoreThanColor()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = LoadedManager(new RecordingActions(), "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    manager.MoveSelection(1);
                    Pump();

                    var table = Descendants<DataGrid>(window).Single();
                    var selected = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0);
                    var other = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(1);

                    Assert.Equal(Visibility.Visible, ((FrameworkElement)selected.Template.FindName("SelectionBar", selected)).Visibility);
                    Assert.Equal(Visibility.Collapsed, ((FrameworkElement)other.Template.FindName("SelectionBar", other)).Visibility);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // The table drops its selection when the rows are rebuilt, and that reaches the view model through the binding.
        [Fact]
        public void Manager_TableKeepsTheSelectionWhenSearchRebuildsTheRows()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = LoadedManager(new RecordingActions(), "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    manager.MoveSelection(1);
                    Pump();

                    manager.SearchText = "alp";
                    Pump();

                    var table = Descendants<DataGrid>(window).Single();
                    Assert.Equal("alpha", manager.SelectedRow?.Name);
                    Assert.Same(manager.SelectedRow, table.SelectedItem);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_EnterOnAButtonStaysWithTheButton()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, "alpha");
                var window = Host(new ManagerView(), manager);
                try
                {
                    manager.MoveSelection(1);
                    Pump();

                    var menuButton = Descendants<MenuButton>(window).Single();

                    Assert.False(Press(menuButton, Key.Enter));
                    Assert.Empty(actions.Launched);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_ArrowsAreIgnoredWhileARowMenuIsOpen()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = LoadedManager(new RecordingActions(), "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search shortcuts");
                    manager.IsMenuOpen = true;

                    Assert.False(Press(search, Key.Down));
                    Assert.Null(manager.SelectedRow);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_TabOrderAndAccessibleNames()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var noop = new RelayCommand(() => { });
                var keycuts = new[] { "alpha", "bravo" }.Select(name => new Keycut
                {
                    Name = name,
                    Kind = KeycutKind.File,
                    RawType = ShortcutType.File,
                    Destination = @"C:\temp\" + name + ".txt",
                    FilePath = @"C:\Shortcuts\" + name + ".bat"
                }).ToList();
                var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null);
                manager.LoadAsync().GetAwaiter().GetResult();

                var window = Host(new ManagerView(), manager);
                try
                {
                    Pump();
                    window.UpdateLayout();

                    Assert.Equal(
                        new[]
                        {
                            "Button:",
                            "Search shortcuts",
                            "SortHeaderButton:Shortcut",
                            "SortHeaderButton:Type",
                            "SortHeaderButton:Description",
                            "Actions for alpha",
                            "Actions for bravo",
                            "Open settings"
                        },
                        TabStops(window));
                    Assert.Equal("Search shortcuts", FocusedName());

                    var heading = Descendants<TextBlock>(window).Single(t => t.Text == "Your keycuts");
                    Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(heading));

                    Assert.Equal(2, Descendants<TypeBadge>(window).Count());
                }
                finally
                {
                    window.Close();
                }
            });
        }

        #endregion
    }
}
