#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
            var creator = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }));
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
                var empty = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }));
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
