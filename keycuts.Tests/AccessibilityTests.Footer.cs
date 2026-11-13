#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
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
        static UpdateNoticeViewModel NewUpdateNotice()
        {
            var state = new FakeUpdateState
            {
                LatestReleaseSeen = ("1.3.0", "https://github.com/andrewralon/keycuts/releases/tag/v1.3.0")
            };
            var notice = new UpdateNoticeViewModel(UpdateFakes.Checker(new FakeReleaseSource(), state), _ => { });
            notice.Refresh();
            return notice;
        }

        static ManagerViewModel NewLoadedManager(UpdateNoticeViewModel? notice, params string[] names)
        {
            var noop = new RelayCommand(() => { });
            var keycuts = names.Select(name => new Keycut
            {
                Name = name,
                Kind = KeycutKind.File,
                RawType = ShortcutType.File,
                Target = @"C:\temp\" + name + ".txt",
                FilePath = @"C:\Shortcuts\" + name + ".bat"
            }).ToList();
            var manager = new ManagerViewModel(noop, noop, ct => Task.FromResult<IReadOnlyList<Keycut>>(keycuts), null) { UpdateNotice = notice };
            manager.LoadAsync().GetAwaiter().GetResult();
            return manager;
        }

        static FrameworkElement Named(FooterView footer, string name)
        {
            return (FrameworkElement)footer.FindName(name);
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
                var manager = NewLoadedManager(null, "alpha", "alpine", "beta");
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

                    // A search that hides some rows leaves the number alone and says so in the name
                    manager.SearchText = "al";
                    Pump();
                    Assert.Equal("3", managerCount.Text);
                    Assert.Equal("2 of 3 keycuts", AutomationProperties.GetName(managerCount));
                }
                finally
                {
                    creatorWindow.Close();
                    managerWindow.Close();
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
                var manager = NewLoadedManager(notice, "alpha");
                var window = Host(new ManagerView(), manager);

                try
                {
                    var footer = Descendants<FooterView>(window).Single();
                    var line = Named(footer, "UpdateNotice");
                    Assert.True(line.IsVisible);

                    var link = (System.Windows.Documents.Hyperlink)footer.FindName("UpdateDownloadLink");
                    Assert.Equal("Download keycuts 1.3.0", AutomationProperties.GetName(link));

                    // The dismiss button follows the text closely instead of sitting at the far right
                    var text = Named(footer, "UpdateNoticeText");
                    var dismiss = Named(footer, "BtnDismissUpdate");
                    var textRight = text.TranslatePoint(new Point(text.ActualWidth, 0), footer).X;
                    var dismissLeft = dismiss.TranslatePoint(new Point(0, 0), footer).X;
                    Assert.InRange(dismissLeft - textRight, -0.5, 4);

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

        [Fact]
        public void Footer_NeverPushesSettingsOrTheCountOut_WhenTheMessageAndTheUpdateLineAreLong()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var manager = NewLoadedManager(NewUpdateNotice(), "alpha");
                var window = Host(new ManagerView(), manager);

                try
                {
                    window.Width = 480;
                    manager.ShowError(new string('x', 600));
                    typeof(ManagerViewModel).GetMethod("SetStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .Invoke(manager, new object[] { "Couldn\u2019t delete \u201Calpha\u201D: " + new string('y', 400), true });
                    Pump();
                    window.UpdateLayout();

                    var footer = Descendants<FooterView>(window).Single();
                    var width = footer.ActualWidth;

                    foreach (var name in new[] { "BtnSettings", "CountText", "UpdateNotice" })
                    {
                        var element = Named(footer, name);
                        Assert.True(element.IsVisible, name);
                        var left = element.TranslatePoint(new Point(0, 0), footer).X;
                        Assert.True(left >= 0 && left + element.ActualWidth <= width + 0.5, $"{name} runs from {left} to {left + element.ActualWidth} in a footer {width} wide");
                    }

                    // Settings, then the count, then the update line: in that order and never over each other
                    var settingsRight = Named(footer, "BtnSettings").TranslatePoint(new Point(Named(footer, "BtnSettings").ActualWidth, 0), footer).X;
                    var countLeft = Named(footer, "CountText").TranslatePoint(new Point(0, 0), footer).X;
                    var countRight = countLeft + Named(footer, "CountText").ActualWidth;
                    var lineLeft = Named(footer, "UpdateNotice").TranslatePoint(new Point(0, 0), footer).X;
                    Assert.True(settingsRight <= countLeft);
                    Assert.True(countRight <= lineLeft);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
