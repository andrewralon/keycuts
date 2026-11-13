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
        public void Footer_OnBothScreens_HasSettingsAndTheCountAsJustANumber()
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
                    var creatorFooter = Descendants<FooterView>(creatorWindow).Single();
                    var settings = (Button)Named(creatorFooter, "BtnSettings");
                    var count = (TextBlock)Named(creatorFooter, "CountText");
                    Assert.Equal("Settings", AutomationProperties.GetName(settings));
                    Assert.Equal("Settings", settings.ToolTip);
                    Assert.Equal("355", count.Text);
                    Assert.Equal("355 keycuts", AutomationProperties.GetName(count));
                    Assert.Equal("355 keycuts", count.ToolTip);

                    var managerFooter = Descendants<FooterView>(managerWindow).Single();
                    Assert.Equal("Settings", AutomationProperties.GetName((Button)Named(managerFooter, "BtnSettings")));
                    var managerCount = (TextBlock)Named(managerFooter, "CountText");
                    Assert.Equal("3", managerCount.Text);
                    Assert.Equal("3 keycuts", AutomationProperties.GetName(managerCount));

                    // "X of Y shown" sits at the right of the search box only while a search hides some keycuts; the pill stays the total
                    var shown = (TextBlock)((FrameworkElement)managerWindow.Content).FindName("ShownTextBlock");
                    Assert.False(shown.IsVisible);

                    manager.SearchText = "al";
                    Pump();
                    Assert.True(shown.IsVisible);
                    Assert.Equal("2 of 3 shown", shown.Text);
                    Assert.Equal("3", managerCount.Text);
                    Assert.Equal("3 keycuts", AutomationProperties.GetName(managerCount));

                    var search = Descendants<TextBox>(managerWindow).Single(t => AutomationProperties.GetName(t) == "Search keycuts");
                    Assert.True(RightIn(shown, (FrameworkElement)managerWindow.Content) > RightIn(search, (FrameworkElement)managerWindow.Content));

                    manager.SearchText = string.Empty;
                    Pump();
                    Assert.False(shown.IsVisible);
                }
                finally
                {
                    creatorWindow.Close();
                    managerWindow.Close();
                }
            });
        }

        // The visible count changes while the focus is elsewhere (a create, a delete) or in the search box (a search), and screen
        // readers have to be told: LiveSetting alone raises nothing
        [Fact]
        public void Announces_TheShownCount_WhenASearchNarrowsIt_AndTheTotal_WhenItChanges()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = NewLoadedManager(null, null, "alpha", "alpine", "beta");
                var managerWindow = Host(new ManagerView(), manager);
                var creator = NewCreator();
                var creatorWindow = Host(new CreatorView(), creator);
                var announced = new List<UIElement>();
                Action<UIElement> record = announced.Add;
                LiveRegion.Announced += record;

                try
                {
                    var shown = (TextBlock)((FrameworkElement)managerWindow.Content).FindName("ShownTextBlock");
                    var managerCount = Named(Descendants<FooterView>(managerWindow).Single(), "CountText");

                    manager.SearchText = "al";
                    Assert.Same(shown, Assert.Single(announced));   // "2 of 3 shown"; the pill's total has not changed

                    manager.SearchText = "alp";
                    Assert.Equal(new UIElement[] { shown, shown }, announced);

                    manager.SearchText = string.Empty;   // everything is shown again: the text is gone, so there is nothing to say
                    Assert.Equal(2, announced.Count);

                    announced.Clear();
                    manager.KeycutCount = 4;   // a keycut was made elsewhere: the total changes, and so does what is shown of it
                    Assert.Contains(managerCount, announced);

                    announced.Clear();
                    creator.KeycutCount = 8;
                    Assert.Same(Named(Descendants<FooterView>(creatorWindow).Single(), "CountText"), Assert.Single(announced));
                }
                finally
                {
                    LiveRegion.Announced -= record;
                    managerWindow.Close();
                    creatorWindow.Close();
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
                    var status = (TextBlock)view.FindName("NameStatusTextBlock");
                    var helper = (TextBlock)view.FindName("HelperTextBlock");
                    var nameLabel = Descendants<TextBlock>(window).Single(t => t.Text == "Name");
                    var heightBefore = view.ActualHeight;
                    Assert.False(status.IsVisible);

                    creator.IsSaving = true;
                    Pump();
                    Assert.True(status.IsVisible);
                    Assert.Equal("Saving…", status.Text);
                    Assert.False(helper.IsVisible);
                    Assert.Same(status, announced.Last());

                    // At the right of the label, on its row
                    Assert.True(LeftIn(status, view) > RightIn(nameLabel, view));
                    Assert.InRange(RightIn(status, view), view.ActualWidth - 21, view.ActualWidth - 19);
                    Assert.InRange(status.TranslatePoint(new Point(0, status.ActualHeight / 2), view).Y,
                        nameLabel.TranslatePoint(new Point(0, 0), view).Y,
                        nameLabel.TranslatePoint(new Point(0, nameLabel.ActualHeight), view).Y);

                    creator.IsSaving = false;
                    creator.SetSuccess("ex");
                    Pump();
                    window.UpdateLayout();
                    Assert.Equal("Created ex", status.Text);
                    Assert.False(helper.IsVisible);
                    Assert.Equal(heightBefore, view.ActualHeight, 0.5);   // the window did not grow
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
                    Assert.Equal("Download keycuts 1.3.0", AutomationProperties.GetName(link));

                    // The dismiss button follows the text closely instead of sitting at the far right
                    var text = Named(footer, "UpdateNoticeText");
                    var dismiss = Named(footer, "BtnDismissUpdate");
                    Assert.InRange(LeftIn(dismiss, footer) - RightIn(text, footer), -0.5, 4);

                    // ...and the line is right-justified in the footer
                    Assert.InRange(footer.ActualWidth - 16 - RightIn(dismiss, footer), -0.5, 1);

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

        // Settings, then what the screen says, then Download, then dismiss: the order they are in on screen. Real focus traversal,
        // since the TabStops helper leaves out hyperlinks
        [Fact]
        public void Footer_FocusGoesSettings_Undo_Download_Dismiss()
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
                    var settings = (Button)Named(footer, "BtnSettings");
                    Assert.True(Descendants<Button>(window).Single(b => b.Name == "UndoButton").IsVisible);

                    settings.Focus();
                    Keyboard.Focus(settings);
                    var order = new List<string> { FocusedName() };

                    for (var step = 0; step < 3; step++)
                    {
                        // Download is a Hyperlink, which is a content element, not a UIElement
                        var request = new TraversalRequest(FocusNavigationDirection.Next);
                        if (Keyboard.FocusedElement is UIElement element) { element.MoveFocus(request); } else { ((FrameworkContentElement)Keyboard.FocusedElement).MoveFocus(request); }
                        order.Add(FocusedName());
                    }

                    Assert.Equal(new[] { "Settings", "Undo delete", "Download keycuts 1.3.0", "Dismiss update notice" }, order);
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

        // The Manager's narrowest width: a long message is trimmed to its cap, Undo stays, and the update line still has room
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

                    void CheckEverythingFits(string scenario, bool undoShows)
                    {
                        Pump();
                        window.UpdateLayout();
                        var width = footer.ActualWidth;
                        var undo = Descendants<Button>(window).Single(b => b.Name == "UndoButton");
                        var message = Descendants<TextBlock>(window).Single(t => t.Name == "StatusMessageText");
                        Assert.Equal(undoShows, undo.IsVisible);

                        var settings = Named(footer, "BtnSettings");
                        var count = Named(footer, "CountText");
                        var line = Named(footer, "UpdateNotice");
                        var link = Named(footer, "UpdateNoticeText");

                        foreach (var element in new FrameworkElement[] { settings, count, message, line }.Concat(undoShows ? new[] { undo } : Array.Empty<FrameworkElement>()))
                        {
                            Assert.True(LeftIn(element, footer) >= 0 && RightIn(element, footer) <= width + 0.5,
                                $"{scenario}: {element.GetType().Name} {element.Name} runs from {LeftIn(element, footer)} to {RightIn(element, footer)} in a footer {width} wide");
                        }

                        // In order, never over each other, and the update text has real room
                        Assert.True(RightIn(settings, footer) <= LeftIn(count, footer), scenario);
                        Assert.True(RightIn(count, footer) <= LeftIn(message, footer) + 0.5, scenario);
                        Assert.True((undoShows ? RightIn(undo, footer) : RightIn(message, footer)) <= LeftIn(line, footer) + 0.5, scenario);
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
