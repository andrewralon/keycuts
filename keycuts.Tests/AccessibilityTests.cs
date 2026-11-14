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
    [Collection(WpfCollection.Name)]
    public partial class AccessibilityTests
    {
        static CreatorViewModel NewCreator()
        {
            var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), checkName: TestNameChecks.NoWarnings);
            creator.Target = "https://example.com";
            creator.TargetKind = KeycutKind.Url;
            creator.ShortcutName = "ex";
            return creator;
        }

        #region View model text for screen readers

        [Fact]
        public void NameHelpText_ReflectsNameWarningsAndErrorsOnly()
        {
            var creator = NewCreator();
            Assert.Equal(string.Empty, creator.NameHelpText);

            creator.SetNameWarning(TestNameChecks.Existing("ex"));
            Assert.Equal(creator.HelperText, creator.NameHelpText);
            Assert.Contains("already exists", creator.NameHelpText);

            creator.ShortcutName = "ex2";
            Assert.Equal(string.Empty, creator.NameHelpText);
        }

        [Fact]
        public void TargetHelpText_ReflectsTheTargetError_AndWinsOverNameHelp()
        {
            var creator = NewCreator();
            Assert.Equal(string.Empty, creator.TargetHelpText);

            creator.SetNameWarning(TestNameChecks.Existing("ex"));
            creator.TargetError = TargetError.NotFound;

            Assert.Equal(CreatorViewModel.GetTargetErrorMessage(TargetError.NotFound), creator.TargetHelpText);
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
                    Assert.Equal(new[] { "Target", "Browse", "keycut name", "Add a description (optional)" }, stops);

                    creator.ShortcutName = "ex";
                    Pump();
                    stops = TabStops(window);
                    Assert.Equal(new[] { "Target", "Browse", "keycut name", "Create", "Add a description (optional)" }, stops);

                    creator.IsSaving = true;
                    Pump();
                    Assert.Empty(TabStops(window));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // #162, #205: the description label is a button. "Add a description (optional)" shows a named field that follows Create (Enter there
        // creates), and "Description (optional)", now named "Hide description", hides it again
        [Fact]
        public void Creator_TheDescriptionLabel_ShowsANamedField_AndHidesItAgain()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var creator = NewCreator();
                var window = Host(new CreatorView(), creator);
                try
                {
                    var toggle = Descendants<Button>(window).Single(b => b.Name == "BtnToggleDescription");
                    var box = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Description");
                    Assert.True(toggle.IsVisible);
                    Assert.False(box.IsVisible);
                    Assert.Equal("Add a description (optional)", toggle.Content);

                    toggle.Command.Execute(null);
                    Pump();

                    Assert.True(toggle.IsVisible);
                    Assert.True(box.IsVisible);
                    Assert.Equal("Description (optional)", toggle.Content);
                    Assert.Equal("Hide description", AutomationProperties.GetName(toggle));
                    Assert.Equal(
                        new[] { "Target", "Browse", "keycut name", "Create", "Hide description", "Description" },
                        TabStops(window));
                    Assert.Contains(box.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.Return && ReferenceEquals(b.Command, creator.CreateCommand));

                    toggle.Command.Execute(null);
                    Pump();

                    Assert.False(box.IsVisible);
                    Assert.Equal("Add a description (optional)", toggle.Content);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Creator_WelcomePanel_FollowsTheManagerButtonInTabOrder_WithNamedControls_AndHidesOnDismiss()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var welcome = new WelcomeViewModel(() => false, () => { });
                var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), checkName: TestNameChecks.NoWarnings) { Welcome = welcome };
                creator.Target = "https://example.com";
                creator.TargetKind = KeycutKind.Url;
                creator.ShortcutName = "ex";
                welcome.OnKeycutCountLoaded(0);

                var window = Host(new CreatorView(), creator);
                try
                {
                    Assert.Equal(
                        new[]
                        {
                            "got it",
                            "URL google.com, creates the keycut g", "Folder Downloads, creates the keycut dl", "File keycuts, creates the keycut kc",
                            "Target", "Browse", "keycut name", "Create", "Add a description (optional)"
                        },
                        TabStops(window));

                    welcome.Dismiss();
                    Pump();
                    Assert.Equal(new[] { "Target", "Browse", "keycut name", "Create", "Add a description (optional)" }, TabStops(window));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // #213: the bubbles and Got it change the form, so they are off while a keycut is being saved, like the fields themselves
        [Fact]
        public void Creator_WelcomePanel_IsOffWhileSaving()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var pending = new TaskCompletionSource<CreateResult>();
                var welcome = new WelcomeViewModel(() => false, () => { });
                var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }),
                    (target, name, description, useEnvironmentVariables, replace, ct) => pending.Task, TestNameChecks.NoWarnings)
                {
                    Welcome = welcome
                };
                welcome.OnKeycutCountLoaded(0);

                var window = Host(new CreatorView(), creator);
                try
                {
                    var panel = (Border)Descendants<CreatorView>(window).Single().FindName("WelcomePanel");
                    Assert.True(panel.IsEnabled);

                    _ = creator.CreateCommand.ExecuteAsync();
                    Pump();
                    Assert.True(creator.IsSaving);
                    Assert.False(panel.IsEnabled);

                    pending.SetResult(CreateResult.Success(null!));
                    // The save's continuation can take more than one pump on a slow runner (it failed once on CI with one)
                    for (var i = 0; i < 50 && creator.IsSaving; i++)
                    {
                        Pump();
                    }

                    Assert.False(creator.IsSaving);
                    Assert.True(panel.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // #171: the update line sits above the footer link; its dismiss button is named and takes the line away
        [Fact]
        public void Creator_UpdateNotice_HasANamedDismissButton_AndHidesOnDismiss()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var state = new FakeUpdateState
                {
                    LatestReleaseSeen = ("1.2.0", "https://github.com/andrewralon/keycuts/releases/tag/v1.2.0")
                };
                var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(new FakeReleaseSource(), state), _ => { });
                notice.Refresh();
                var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), checkName: TestNameChecks.NoWarnings) { UpdateNotice = notice };
                creator.Target = "https://example.com";
                creator.TargetKind = KeycutKind.Url;
                creator.ShortcutName = "ex";

                var window = Host(new CreatorView(), creator);
                try
                {
                    Assert.Equal(
                        new[] { "Target", "Browse", "keycut name", "Create", "Add a description (optional)", "Dismiss update notice" },
                        TabStops(window));
                    var link = (System.Windows.Documents.Hyperlink)Descendants<FooterView>(window).Single().FindName("UpdateDownloadLink");
                    Assert.Equal("download keycuts 1.2.0", AutomationProperties.GetName(link));

                    notice.DismissCommand.Execute(null);
                    Pump();
                    Assert.Equal(new[] { "Target", "Browse", "keycut name", "Create", "Add a description (optional)" }, TabStops(window));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Creator_WithoutAnUpdateNotice_ShowsNoUpdateLine()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = Host(new CreatorView(), NewCreator());
                try
                {
                    var line = (FrameworkElement)Descendants<FooterView>(window).Single().FindName("UpdateNotice");
                    Assert.False(line.IsVisible);
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
        public void Creator_LaunchFocus_GoesToTheTargetOrTheName()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var empty = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), checkName: TestNameChecks.NoWarnings);
                var window = Host(new CreatorView(), empty);
                try
                {
                    Assert.Equal("Target", FocusedName());
                }
                finally
                {
                    window.Close();
                }

                var withPath = Host(new CreatorView(), NewCreator());
                try
                {
                    Assert.Equal("keycut name", FocusedName());
                }
                finally
                {
                    withPath.Close();
                }
            });
        }

        [Fact]
        public void Creator_HasNoHeading_AndTheErrorLiveRegionIsExposed()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var creator = NewCreator();
                var window = Host(new CreatorView(), creator);
                try
                {
                    // The selected tab names the screen (#218), so the screen has no heading of its own
                    Assert.DoesNotContain(Descendants<TextBlock>(window), t => t.Text == "keycuts creator");

                    var helper = Descendants<HelperText>(window).Single(t => t.Name == "HelperTextBlock");
                    Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(helper));

                    creator.SetNameWarning(TestNameChecks.Existing("ex"));
                    Pump();
                    Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(helper));
                    Assert.Equal(HelperTextState.Warning, helper.State);

                    var name = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "keycut name");
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

            public List<string> Edited { get; } = new List<string>();

            public void Edit(string path)
            {
                Edited.Add(path);
            }

            public List<string> Located { get; } = new List<string>();

            public void OpenFileLocation(string path)
            {
                Located.Add(path);
            }

            public List<string> Copied { get; } = new List<string>();

            public void CopyText(string text)
            {
                Copied.Add(text);
            }

            public List<DeletedKeycut> Deleted { get; } = new List<DeletedKeycut>();

            public List<DeletedKeycut> Restored { get; } = new List<DeletedKeycut>();

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

            public List<string> Renamed { get; } = new List<string>();

            public Task<string> RenameAsync(Keycut keycut, string newName)
            {
                Renamed.Add(newName);
                return Task.FromResult(keycut.FilePath);
            }

            public List<(Keycut Keycut, string Description)> Described { get; } = new List<(Keycut, string)>();

            /// <summary>The keycuts "on disk" that the manager's loader reads (see <see cref="LoadedManager"/>); saving a description changes its entry.</summary>
            public List<Keycut>? Disk { get; set; }

            public Task<Keycut> SetDescriptionAsync(Keycut keycut, string description)
            {
                Described.Add((keycut, description));
                var saved = keycut with { Description = description.Trim(), Modified = new DateTime(2026, 6, 7, 8, 9, 10) };

                if (Disk != null)
                {
                    Disk[Disk.FindIndex(k => k.FilePath == keycut.FilePath)] = saved;
                }

                return Task.FromResult(saved);
            }

            public Task<bool> CanEditInFormAsync(Keycut keycut)
            {
                return Task.FromResult(true);
            }

            public Task<Keycut> UpdateAsync(Keycut keycut, KeycutEdit edit)
            {
                return Task.FromResult(keycut with { Name = edit.Name, Target = edit.Target, Description = edit.Description });
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
                Target = @"C:\temp\" + name + ".txt",
                FilePath = @"C:\fixture\" + name + ".bat"
            }).ToList();
            if (actions is RecordingActions recording)
            {
                recording.Disk = keycuts;
            }

            // Every load reads the list as it is now, as the real one reads the folder
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts.ToList()), null, actions);
            manager.LoadAsync().GetAwaiter().GetResult();
            return manager;
        }

        // The event carries its own keyboard, so a handler that reads e.KeyboardDevice.Modifiers sees exactly these modifiers and
        // not whatever is held on the machine while the tests run (that used to fail them at random)
        static bool Press(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            var args = new KeyEventArgs(new FakeKeyboardDevice(modifiers), PresentationSource.FromVisual(target), 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            target.RaiseEvent(args);
            Pump();
            return args.Handled;
        }

        // #179: the Undo button shows with the "Deleted" message, and Ctrl+Z does the same, even from the search box
        [Fact]
        public void Manager_Undo_ShowsAButton_AndCtrlZWorksFromTheSearchBoxWhileItShows()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");
                    Assert.False(undo.IsVisible);
                    Assert.Equal("undo delete", AutomationProperties.GetName(undo));

                    // Nothing to undo: Ctrl+Z is left to the search box's own undo
                    Assert.False(Press(search, Key.Z, ModifierKeys.Control));

                    manager.DeleteKeycutAsync(manager.Rows[0]).GetAwaiter().GetResult();
                    Pump();

                    Assert.True(undo.IsVisible);
                    Assert.Contains(undo, Descendants<Button>(window).Where(b => b.IsTabStop && b.IsEnabled && b.IsVisible));

                    // Z alone (typing) and Ctrl+Z with another modifier aren't an undo
                    Assert.False(Press(search, Key.Z));
                    Assert.False(Press(search, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
                    Assert.Empty(actions.Restored);

                    Assert.True(Press(search, Key.Z, ModifierKeys.Control));

                    Assert.Single(actions.Restored);
                    Assert.Equal(2, manager.Rows.Count);
                    Assert.False(undo.IsVisible);
                    Assert.False(Press(search, Key.Z, ModifierKeys.Control));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_Undo_TheButtonRestoresTheKeycut()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    manager.DeleteKeycutAsync(manager.Rows[1]).GetAwaiter().GetResult();
                    Pump();
                    var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");

                    undo.Command.Execute(null);
                    Pump();

                    Assert.Single(actions.Restored);
                    Assert.Equal(new[] { "alpha", "bravo" }, manager.Rows.Select(r => r.Name));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Manager_CtrlZ_IsLeftToTheNameBoxWhileRenaming()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = LoadedManager(actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    manager.DeleteKeycutAsync(manager.Rows[1]).GetAwaiter().GetResult();
                    manager.BeginRename(manager.Rows[0]);
                    Pump();

                    Assert.False(Press(search, Key.Z, ModifierKeys.Control));
                    Assert.Empty(actions.Restored);
                    Assert.True(manager.CanUndoDelete);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // Also proves the handler reads the event's keyboard: no modifier is held on this machine, yet Ctrl on the event is respected
        [Theory]
        [InlineData(ModifierKeys.Control)]
        [InlineData(ModifierKeys.Shift)]
        [InlineData(ModifierKeys.Alt)]
        public void Manager_IgnoresArrowsAndF2WithAModifier_ButHandlesThemWithout(ModifierKeys modifiers)
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = LoadedManager(new RecordingActions(), "alpha", "bravo");
                var window = Host(new ManagerView(), manager);
                try
                {
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");

                    Assert.False(Press(search, Key.Down, modifiers));
                    Assert.Null(manager.SelectedRow);

                    Assert.True(Press(search, Key.Down));
                    Assert.Equal("alpha", manager.SelectedRow?.Name);

                    // Shift+F2 is the description's (#202); with the other modifiers F2 isn't ours
                    var isEditDescription = modifiers == ModifierKeys.Shift;
                    Assert.Equal(isEditDescription, Press(search, Key.F2, modifiers));
                    Assert.False(manager.IsRenaming);
                    Assert.Equal(isEditDescription, manager.IsEditingDescription);
                    manager.CancelEditDescription();

                    Assert.True(Press(search, Key.F2));
                    Assert.True(manager.IsRenaming);
                }
                finally
                {
                    window.Close();
                }
            });
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
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
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
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
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

                    // A click on something that can't take focus (another row's name) saves too
                    Assert.True(Press(search, Key.F2));
                    Pump();
                    box = Descendants<TextBox>(window).Single(t => t.IsVisible && AutomationProperties.GetName(t) == "New name for bravo");
                    box.Text = "yankee";
                    var otherName = Descendants<TextBlock>(window).First(t => t.Text == "alpha" && t.IsVisible);
                    otherName.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
                    Pump();
                    Assert.Equal(new[] { "zulu", "yankee" }, actions.Renamed);
                    Assert.False(manager.IsRenaming);

                    // A click inside the box doesn't
                    Assert.True(Press(search, Key.F2));
                    Pump();
                    box = Descendants<TextBox>(window).Single(t => t.IsVisible && AutomationProperties.GetName(t) == "New name for bravo");
                    box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
                    Pump();
                    Assert.True(manager.IsRenaming);

                    // Neither does focus moving into the box's own context menu (Cut, Copy, Paste); focus elsewhere saves
                    var menu = new ContextMenu { PlacementTarget = box };
                    var paste = new MenuItem { Header = "Paste" };
                    menu.Items.Add(paste);
                    box.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, box, paste) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
                    Pump();
                    Assert.True(manager.IsRenaming);

                    box.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, box, search) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
                    Pump();
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
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
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
                    Target = @"C:\temp\" + name + ".txt",
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
                            "Search keycuts",
                            "SortHeaderButton:Name",
                            "SortHeaderButton:Type",
                            "SortHeaderButton:Description",
                            "SortHeaderButton:Target",
                            "Actions for alpha",
                            "Actions for bravo"
                        },
                        TabStops(window));
                    Assert.Equal("Search keycuts", FocusedName());

                    // The selected tab names the screen (#218), so the screen has no heading of its own
                    Assert.DoesNotContain(Descendants<TextBlock>(window), t => t.Text == "keycuts manager");

                    Assert.Equal(2, Descendants<TypeBadge>(window).Count());
                }
                finally
                {
                    window.Close();
                }
            });
        }

        static MenuItem OpenRowMenuItem(Window window, string rowName, string header, out ContextMenu menu)
        {
            var button = Descendants<MenuButton>(window).Single(b => AutomationProperties.GetName(b) == "Actions for " + rowName);
            menu = button.ContextMenu;
            menu.PlacementTarget = button;
            menu.IsOpen = true;
            Pump();
            return menu.Items.OfType<MenuItem>().Single(i => i.Header as string == header);
        }

        // #202: Edit looks unavailable on a keycut the form can't save, UI Automation says so, and its tooltip points to Notepad
        [Fact]
        public void Manager_EditIsDisabled_ForAKeycutTheFormCantEdit_AndSaysWhy()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var noop = new RelayCommand(() => { });
                var keycuts = new[] { ("alpha", true), ("bravo", false) }.Select(item => new Keycut
                {
                    Name = item.Item1,
                    Kind = KeycutKind.File,
                    RawType = ShortcutType.File,
                    Target = @"C:\temp\" + item.Item1 + ".txt",
                    FilePath = @"C:\Shortcuts\" + item.Item1 + ".bat",
                    CanEditInForm = item.Item2
                }).ToList();
                var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null, new RecordingActions());
                manager.LoadAsync().GetAwaiter().GetResult();
                var window = Host(new ManagerView(), manager);
                try
                {
                    var fine = OpenRowMenuItem(window, "alpha", "Edit…", out var fineMenu);
                    Assert.True(fine.IsEnabled);
                    Assert.True(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(fine).IsEnabled());
                    Assert.False(ToolTipService.GetIsEnabled(fine));
                    fineMenu.IsOpen = false;

                    var edit = OpenRowMenuItem(window, "bravo", "Edit…", out var menu);
                    Assert.False(edit.IsEnabled);
                    Assert.False(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(edit).IsEnabled());
                    Assert.Equal(EditKeycutViewModel.NotEditableMessage, edit.ToolTip);
                    Assert.True(ToolTipService.GetIsEnabled(edit));
                    Assert.True(ToolTipService.GetShowOnDisabled(edit));

                    // Edit in Notepad is the way to edit it, so it stays available
                    var notepad = menu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Edit in Notepad");
                    Assert.True(notepad.IsEnabled);
                    Assert.True(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(notepad).IsEnabled());
                    menu.IsOpen = false;
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // #167: the Modified column is sortable and shows how long ago with the exact time in a tooltip, but it makes way for
        // the other columns when the table is narrow
        [Fact]
        public void Manager_ModifiedColumn_ShowsWhenWide_AndMakesWayWhenNarrow()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var noop = new RelayCommand(() => { });
                var modified = DateTime.Now.AddDays(-3);
                var keycuts = new[]
                {
                    new Keycut
                    {
                        Name = "alpha",
                        Kind = KeycutKind.File,
                        RawType = ShortcutType.File,
                        Target = @"C:\temp\alpha.txt",
                        FilePath = @"C:\Shortcuts\alpha.bat",
                        Modified = modified
                    }
                };
                var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null);
                manager.LoadAsync().GetAwaiter().GetResult();

                var window = Host(new ManagerView(), manager);
                try
                {
                    var column = (DataGridColumn)((FrameworkElement)window.Content).FindName("ModifiedColumn");

                    // Host is 700 wide: its table is under the threshold
                    Assert.Equal(Visibility.Collapsed, column.Visibility);
                    Assert.DoesNotContain("SortHeaderButton:Modified", TabStops(window));

                    window.Width = 1000;
                    Pump();
                    window.UpdateLayout();
                    Pump();

                    Assert.Equal(Visibility.Visible, column.Visibility);
                    Assert.Equal(
                        new[] { "Search keycuts", "SortHeaderButton:Name", "SortHeaderButton:Type", "SortHeaderButton:Description", "SortHeaderButton:Target", "SortHeaderButton:Modified", "Actions for alpha" },
                        TabStops(window));

                    var text = Descendants<TextBlock>(window).Single(t => t.Text == "3 days ago");
                    Assert.Equal(modified.ToString("f"), text.ToolTip);

                    window.Width = 700;
                    Pump();
                    window.UpdateLayout();
                    Pump();

                    Assert.Equal(Visibility.Collapsed, column.Visibility);
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
