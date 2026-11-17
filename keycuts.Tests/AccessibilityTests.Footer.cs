#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    // The footer both screens share (#205): Settings, the count as a number, the manager's message, and the update line
    public partial class AccessibilityTests
    {
        static UpdateNoticeViewModel NewUpdateNotice(Action<string>? openUrl = null)
        {
            var state = new FakeUpdateState
            {
                LatestReleaseSeen = ("1.3.0", "https://github.com/andrewralon/keycuts/releases/tag/v1.3.0")
            };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(new FakeReleaseSource(), state), openUrl ?? (_ => { }));
            notice.Refresh();
            return notice;
        }

        static ManagerViewModel NewLoadedManager(UpdateNoticeViewModel? notice, IKeycutActions? actions, params string[] names)
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

            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts.ToList()), null, actions) { UpdateNotice = notice };
            manager.LoadAsync().GetAwaiter().GetResult();
            return manager;
        }

        static FrameworkElement Named(FooterView footer, string name)
        {
            return (FrameworkElement)footer.FindName(name);
        }

        static double LeftIn(FrameworkElement element, FrameworkElement ancestor)
        {
            return element.TranslatePoint(new Point(0, 0), ancestor).X;
        }

        static double RightIn(FrameworkElement element, FrameworkElement ancestor)
        {
            return LeftIn(element, ancestor) + element.ActualWidth;
        }

        static void SetManagerStatus(ManagerViewModel manager, string message, bool isError)
        {
            typeof(ManagerViewModel).GetMethod("SetStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(manager, new object[] { message, isError });
        }

        [Fact]
        public void Footer_HasNoSettingsGearNorCount_TheCountIsBesideTheManagersSearch()
        {
            RunSta(() =>
            {
                EnsureApplication();

                var creator = NewCreator();
                creator.KeycutCount = 355;
                var creatorWindow = Host(new CreatorView(), creator);
                var manager = NewLoadedManager(null, null, "alpha", "alpine", "beta");
                var managerWindow = Host(new ManagerView(), manager);

                try
                {
                    // Settings is a tab in the bar above (#218), and the count is the Manager's, beside its search box
                    foreach (var footer in Descendants<FooterView>(creatorWindow).Concat(Descendants<FooterView>(managerWindow)))
                    {
                        Assert.Null(footer.FindName("BtnSettings"));
                        Assert.Null(footer.FindName("CountText"));
                    }

                    Assert.DoesNotContain(Descendants<TextBlock>(creatorWindow), t => t.Text.Contains("355"));

                    // "3 keycuts", and "2 of 3 keycuts" while a search hides some, in a pill at the right of the search box
                    var view = (FrameworkElement)managerWindow.Content;
                    var count = (TextBlock)view.FindName("CountText");
                    var pill = (FrameworkElement)view.FindName("CountPill");
                    Assert.Equal("3 keycuts", count.Text);
                    Assert.True(count.IsVisible);

                    manager.SearchText = "al";
                    Pump();
                    Assert.Equal("2 of 3 keycuts", count.Text);

                    var search = Descendants<TextBox>(managerWindow).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    Assert.True(LeftIn(pill, view) >= RightIn(search, view));
                    Assert.Equal(search.TranslatePoint(new Point(0, search.ActualHeight / 2), view).Y,
                        pill.TranslatePoint(new Point(0, pill.ActualHeight / 2), view).Y, 0.5);

                    manager.SearchText = string.Empty;
                    Pump();
                    Assert.Equal("3 keycuts", count.Text);
                }
                finally
                {
                    creatorWindow.Close();
                    managerWindow.Close();
                }
            });
        }

        // The count changes while the focus is in the search box (a search) or elsewhere (a create, a delete), and screen readers
        // have to be told: LiveSetting alone raises nothing
        [Fact]
        public void Announces_TheCount_WhenASearchNarrowsIt_AndWhenTheTotalChanges()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = NewLoadedManager(null, null, "alpha", "alpine", "beta");
                var managerWindow = Host(new ManagerView(), manager);
                var announced = new List<UIElement>();
                Action<UIElement> record = announced.Add;
                LiveRegion.Announced += record;

                try
                {
                    var count = (TextBlock)((FrameworkElement)managerWindow.Content).FindName("CountText");

                    manager.SearchText = "al";
                    Assert.Same(count, Assert.Single(announced));   // "2 of 3 keycuts"

                    manager.SearchText = string.Empty;   // "3 keycuts" again
                    Assert.Equal(new UIElement[] { count, count }, announced);

                    announced.Clear();
                    manager.KeycutCount = 4;   // a keycut was made elsewhere
                    Assert.Contains(count, announced);
                    Assert.Equal("4 keycuts", count.Text);
                }
                finally
                {
                    LiveRegion.Announced -= record;
                    managerWindow.Close();
                }
            });
        }

        // A status message (Deleted, a save error) appears with the focus elsewhere; LiveSetting alone raises nothing
        [Fact]
        public void Announces_TheManagerStatusMessage_WhenItAppears_ButNotWhenItGoes()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = NewLoadedManager(null, null, "alpha");
                var managerWindow = Host(new ManagerView(), manager);
                var announced = new List<UIElement>();
                Action<UIElement> record = announced.Add;
                LiveRegion.Announced += record;

                try
                {
                    var message = (TextBlock)Descendants<ManagerStatusView>(managerWindow).Single().FindName("StatusMessageText");

                    SetManagerStatus(manager, "Couldn’t restore “alpha”", true);
                    Assert.Same(message, Assert.Single(announced));
                    Assert.Equal("Couldn’t restore “alpha”", message.Text);

                    SetManagerStatus(manager, string.Empty, false);   // it timed out: nothing to say
                    Assert.Single(announced);
                }
                finally
                {
                    LiveRegion.Announced -= record;
                    managerWindow.Close();
                }
            });
        }

        // Saving and Created are on the Name label's row, not under the field, so the window keeps its height (#205)
        [Fact]
        public void Creator_SavingAndCreated_ShowByTheNameLabel_AndAreAnnounced()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var creator = NewCreator();
                var window = Host(new CreatorView(), creator);
                var announced = new List<UIElement>();
                Action<UIElement> record = announced.Add;
                LiveRegion.Announced += record;

                try
                {
                    var view = (FrameworkElement)window.Content;
                    var status = (HelperText)view.FindName("NameStatusTextBlock");
                    var helper = (HelperText)view.FindName("HelperTextBlock");
                    var nameLabel = Descendants<TextBlock>(window).Single(t => t.Text == "Name");
                    var heightBefore = view.ActualHeight;
                    Assert.False(status.IsVisible);

                    creator.IsSaving = true;
                    Pump();
                    Assert.True(status.IsVisible);
                    Assert.Equal("Saving…", status.Text);
                    Assert.False(helper.IsVisible);
                    Assert.Same(status, announced.Last());

                    // At the right of the label, on its row, at the page's 24 padding
                    Assert.True(LeftIn(status, view) > RightIn(nameLabel, view));
                    Assert.InRange(RightIn(status, view), view.ActualWidth - 25, view.ActualWidth - 23);
                    Assert.InRange(status.TranslatePoint(new Point(0, status.ActualHeight / 2), view).Y,
                        nameLabel.TranslatePoint(new Point(0, 0), view).Y,
                        nameLabel.TranslatePoint(new Point(0, nameLabel.ActualHeight), view).Y);

                    // A very long name changes nothing: the message is just "Created", and the name is in the box
                    creator.IsSaving = false;
                    creator.ShortcutName = new string('n', 200);
                    creator.SetSuccess(creator.ShortcutName);
                    Pump();
                    window.UpdateLayout();
                    Assert.Equal(CreatorViewModel.CreatedText, status.Text);
                    Assert.Equal(HelperTextState.Success, status.State);
                    Assert.False(helper.IsVisible);
                    Assert.Equal(heightBefore, view.ActualHeight, 0.5);   // the window did not grow
                    Assert.True(LeftIn(status, view) > RightIn(nameLabel, view), "Created runs over the Name label");
                    Assert.True(RightIn(status, view) <= view.ActualWidth, "Created runs off the right edge");
                }
                finally
                {
                    LiveRegion.Announced -= record;
                    window.Close();
                }
            });
        }

        [Fact]
        public void Footer_ShowsTheUpdateLineOnTheManagerToo_WithTheDismissButtonRightAfterIt()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var notice = NewUpdateNotice();
                var manager = NewLoadedManager(notice, null, "alpha");
                var window = Host(new ManagerView(), manager);

                try
                {
                    var footer = Descendants<FooterView>(window).Single();
                    var line = Named(footer, "UpdateNotice");
                    Assert.True(line.IsVisible);

                    var link = (Hyperlink)footer.FindName("UpdateDownloadLink");
                    Assert.Equal("download keycuts 1.3.0", AutomationProperties.GetName(link));

                    // The dismiss button follows the text closely instead of sitting at the far right
                    var text = Named(footer, "UpdateNoticeText");
                    var dismiss = Named(footer, "BtnDismissUpdate");
                    Assert.InRange(LeftIn(dismiss, footer) - RightIn(text, footer), -0.5, 4);

                    // ...and the line is right-justified in the footer, short of the torn corner (FooterBarPadding)
                    Assert.InRange(footer.ActualWidth - ((Thickness)footer.FindResource("FooterBarPadding")).Right - RightIn(dismiss, footer), -0.5, 1);

                    notice.DismissCommand.Execute(null);
                    Pump();
                    Assert.False(line.IsVisible);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // Undo right after the search box, and the bar's Download, then dismiss: the order they are in on screen. Real focus
        // traversal, since the TabStops helper leaves out hyperlinks
        [Fact]
        public void Footer_FocusGoesSearch_Undo_AndDownload_Dismiss()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = NewLoadedManager(NewUpdateNotice(), actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);

                try
                {
                    manager.DeleteKeycutAsync(manager.Rows[0]).GetAwaiter().GetResult();
                    Pump();
                    window.UpdateLayout();

                    // Undo is in the search row, right after the search box
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");
                    Assert.True(undo.IsVisible);
                    search.Focus();
                    Keyboard.Focus(search);
                    search.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    Assert.Equal("undo delete", FocusedName());

                    // The bar's link, then its dismiss button. Download is a Hyperlink, a content element, not a UIElement.
                    var footer = Descendants<FooterView>(window).Single();
                    var download = (System.Windows.Documents.Hyperlink)footer.FindName("UpdateDownloadLink");
                    download.Focus();
                    Keyboard.Focus(download);
                    Assert.Equal("download keycuts 1.3.0", FocusedName());
                    download.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    Assert.Equal("Dismiss update notice", FocusedName());
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // Enter on the Download link must follow the link, not open the selected row (the Manager's Enter key opens the row)
        [Fact]
        public void Footer_EnterOnDownload_FollowsTheLink_AndDoesNotOpenTheSelectedRow()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var downloaded = new List<string>();
                var manager = NewLoadedManager(NewUpdateNotice(downloaded.Add), actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);

                try
                {
                    manager.SelectedRow = manager.Rows[0];
                    var footer = Descendants<FooterView>(window).Single();
                    var link = (Hyperlink)footer.FindName("UpdateDownloadLink");

                    var args = new KeyEventArgs(new FakeKeyboardDevice(ModifierKeys.None), PresentationSource.FromVisual(window), 0, Key.Enter)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent
                    };
                    link.RaiseEvent(args);
                    Pump();

                    Assert.False(args.Handled);
                    Assert.Empty(actions.Launched);

                    // The same key in the search box still opens the selected row
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    Assert.True(Press(search, Key.Enter));
                    Assert.Equal(new[] { @"C:\fixture\alpha.bat" }, actions.Launched);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // The bar at the bottom is there only to say a newer version is out: not without one, and not once it is dismissed
        [Fact]
        public void TheBar_ShowsOnlyWhileANewerVersionIsOut_UntilDismissed()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var notice = NewUpdateNotice();
                var withNotice = new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }), checkName: TestNameChecks.NoWarnings)
                {
                    UpdateNotice = notice
                };
                var creatorWindow = Host(new CreatorView(), withNotice);
                var plainWindow = Host(new CreatorView(), NewCreator());
                var managerWindow = Host(new ManagerView(), NewLoadedManager(notice, null, "alpha"));

                try
                {
                    FrameworkElement Bar(Window window) => (FrameworkElement)Descendants<FooterView>(window).Single().FindName("Bar");

                    Assert.True(notice.IsVisible);
                    Assert.True(Bar(creatorWindow).IsVisible);
                    Assert.True(Bar(managerWindow).IsVisible);
                    Assert.False(Bar(plainWindow).IsVisible);
                    Assert.Equal(0, Descendants<FooterView>(plainWindow).Single().ActualHeight);

                    // Dismissed: gone from every screen, and it takes no room
                    notice.DismissCommand.Execute(null);
                    Pump();
                    creatorWindow.UpdateLayout();
                    managerWindow.UpdateLayout();
                    Assert.False(Bar(creatorWindow).IsVisible);
                    Assert.False(Bar(managerWindow).IsVisible);
                    Assert.Equal(0, Descendants<FooterView>(managerWindow).Single().ActualHeight);
                }
                finally
                {
                    creatorWindow.Close();
                    plainWindow.Close();
                    managerWindow.Close();
                }
            });
        }

        // The Manager's message takes no room in the search row while there is none
        [Fact]
        public void SearchRow_TheMessage_TakesNoRoom_WhileThereIsNone()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = NewLoadedManager(null, new RecordingActions(), "alpha", "bravo");
                var window = Host(new ManagerView(), manager);

                try
                {
                    var view = (FrameworkElement)window.Content;
                    var status = Descendants<ManagerStatusView>(window).Single();
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    var pill = (FrameworkElement)view.FindName("CountPill");
                    Assert.Equal(0, status.ActualWidth);
                    Assert.Equal(12, LeftIn(pill, view) - RightIn(search, view), 0.5);

                    manager.DeleteKeycutAsync(manager.Rows[0]).GetAwaiter().GetResult();
                    Pump();
                    window.UpdateLayout();
                    Assert.True(Descendants<Button>(status).Single(b => b.Name == "UndoButton").IsVisible);
                    Assert.True(LeftIn(pill, view) - RightIn(search, view) > 12);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // The Manager's narrowest width: in the search row a long message is trimmed to its cap, Undo stays put, the search box keeps
        // room and the count stays whole; the bar's update line has room of its own
        [Fact]
        public void SearchRow_AtTheManagersNarrowestWidth_KeepsEverythingVisible_WhateverTheMessageSays()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var actions = new RecordingActions();
                var manager = NewLoadedManager(NewUpdateNotice(), actions, "alpha", "bravo");
                var window = Host(new ManagerView(), manager);

                try
                {
                    window.Width = GUI.WindowLayout.ManagerMinWidth;
                    var view = (FrameworkElement)window.Content;
                    var footer = Descendants<FooterView>(window).Single();
                    var search = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    var pill = (FrameworkElement)view.FindName("CountPill");
                    var row = (FrameworkElement)System.Windows.Media.VisualTreeHelper.GetParent(pill);
                    double? undoLeft = null;

                    void CheckEverythingFits(string scenario, bool undoShows)
                    {
                        Pump();
                        window.UpdateLayout();
                        var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");
                        var message = Descendants<TextBlock>(window).Single(t => t.Name == "StatusMessageText");
                        Assert.Equal(undoShows, undo.IsVisible);

                        // Left to right in the row, never over each other: the search box, the message, Undo, the count
                        var order = new List<FrameworkElement> { search, message };
                        if (undoShows)
                        {
                            order.Add(undo);
                        }

                        order.Add(pill);
                        for (var i = 1; i < order.Count; i++)
                        {
                            Assert.True(RightIn(order[i - 1], row) <= LeftIn(order[i], row) + 0.5, $"{scenario}: {order[i - 1].Name} runs into {order[i].Name}");
                        }

                        Assert.True(RightIn(pill, row) <= row.ActualWidth + 0.5, $"{scenario}: the count runs out of the row");
                        Assert.True(message.ActualWidth <= 300, $"{scenario}: the message is {message.ActualWidth} wide");
                        Assert.True(search.ActualWidth >= 200, $"{scenario}: the search box is only {search.ActualWidth} wide");

                        // Undo always in the same place, whatever the message says
                        if (undoShows)
                        {
                            undoLeft ??= LeftIn(undo, row);
                            Assert.Equal(undoLeft.Value, LeftIn(undo, row), 0.5);
                        }

                        // The bar holds only the update line now, which has real room
                        Assert.True(((FrameworkElement)footer.FindName("UpdateNoticeText")).ActualWidth >= 150, scenario);
                    }

                    manager.DeleteKeycutAsync(manager.Rows[0]).GetAwaiter().GetResult();
                    CheckEverythingFits("deleted, with Undo", undoShows: true);

                    SetManagerStatus(manager, "Couldn\u2019t delete \u201Calpha\u201D: " + new string('y', 400), true);
                    CheckEverythingFits("a long error (Undo is still on offer: a message set from outside doesn't end it)", undoShows: true);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
