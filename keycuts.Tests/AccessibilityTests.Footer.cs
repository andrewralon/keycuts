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

                    // ...and the line is right-justified in the footer
                    Assert.InRange(footer.ActualWidth - 24 - RightIn(dismiss, footer), -0.5, 1);

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

        // Undo, then Download, then dismiss: the order they are in on screen (the gear is a tab now, #218). Real focus traversal,
        // since the TabStops helper leaves out hyperlinks
        [Fact]
        public void Footer_FocusGoesUndo_Download_Dismiss()
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

                    var footer = Descendants<FooterView>(window).Single();
                    var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");
                    Assert.True(undo.IsVisible);

                    undo.Focus();
                    Keyboard.Focus(undo);
                    var order = new List<string> { FocusedName() };

                    for (var step = 0; step < 2; step++)
                    {
                        // Download is a Hyperlink, which is a content element, not a UIElement
                        var request = new TraversalRequest(FocusNavigationDirection.Next);
                        if (Keyboard.FocusedElement is UIElement element) { element.MoveFocus(request); } else { ((FrameworkContentElement)Keyboard.FocusedElement).MoveFocus(request); }
                        order.Add(FocusedName());
                    }

                    Assert.Equal(new[] { "undo delete", "download keycuts 1.3.0", "Dismiss update notice" }, order);
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

        // The Manager's narrowest width: a long message is trimmed to its cap, Undo stays put, and the update line still has room
        [Fact]
        public void Footer_AtTheManagersNarrowestWidth_KeepsEverythingVisible_WhateverTheMessageSays()
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
                    var footer = Descendants<FooterView>(window).Single();
                    double? undoLeft = null;

                    void CheckEverythingFits(string scenario, bool undoShows)
                    {
                        Pump();
                        window.UpdateLayout();
                        var width = footer.ActualWidth;
                        var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");
                        var message = Descendants<TextBlock>(window).Single(t => t.Name == "StatusMessageText");
                        Assert.Equal(undoShows, undo.IsVisible);

                        var line = Named(footer, "UpdateNotice");
                        var link = Named(footer, "UpdateNoticeText");

                        foreach (var element in new FrameworkElement[] { message, line }.Concat(undoShows ? new[] { undo } : Array.Empty<FrameworkElement>()))
                        {
                            Assert.True(LeftIn(element, footer) >= 0 && RightIn(element, footer) <= width + 0.5,
                                $"{scenario}: {element.GetType().Name} {element.Name} runs from {LeftIn(element, footer)} to {RightIn(element, footer)} in a footer {width} wide");
                        }

                        // In order, never over each other, and the update text has real room
                        // Undo first, then the message, then the update line
                        if (undoShows)
                        {
                            Assert.True(RightIn(undo, footer) <= LeftIn(message, footer) + 0.5, scenario);

                            // ...and always in the same place, whatever the message says
                            undoLeft ??= LeftIn(undo, footer);
                            Assert.Equal(undoLeft.Value, LeftIn(undo, footer), 0.5);
                        }

                        Assert.True(RightIn(message, footer) <= LeftIn(line, footer) + 0.5, scenario);
                        Assert.True(link.ActualWidth >= 150, $"{scenario}: the update text is only {link.ActualWidth} wide");
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
