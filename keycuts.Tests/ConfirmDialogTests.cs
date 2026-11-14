#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #153: the confirmation before a replace or a name another command uses. Its text, and that the dialog is built
    /// like the Settings window (same size rules, surface, fonts, footer buttons), so it matches the rest of the app.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class ConfirmDialogTests
    {
        #region Text

        [Fact]
        public void ForCreate_AReplace_ShowsBothTargets_AndOffersReplace()
        {
            var vm = ConfirmViewModel.ForCreate(TestNameChecks.Existing("gmail", "https://old.example.com"), "https://new.example.com");

            Assert.Equal("Replace keycut", vm.Title);
            Assert.Equal("Replace \u201cgmail\u201d?", vm.Heading);
            Assert.Equal(ConfirmViewModel.ReplaceIntro, vm.Intro);
            Assert.Equal("Replace", vm.ConfirmText);
            Assert.True(vm.ShowTargets);
            Assert.Equal("https://old.example.com", vm.CurrentTarget);
            Assert.Equal("https://new.example.com", vm.NewTarget);
            Assert.False(vm.ShowWarnings);
        }

        [Fact]
        public void ForCreate_AnExistingKeycutWithAnUnreadableTarget_SaysSo()
        {
            var vm = ConfirmViewModel.ForCreate(TestNameChecks.Existing("junk", string.Empty), "https://new.example.com");

            Assert.Equal("(unknown)", vm.CurrentTarget);
        }

        [Fact]
        public void ForCreate_ANameConflict_ListsIt_AndOffersCreateAnyway()
        {
            var vm = ConfirmViewModel.ForCreate(TestNameChecks.Conflict("ex"), "https://new.example.com");

            Assert.Equal("Use \u201cex\u201d anyway?", vm.Heading);
            Assert.Equal("Create anyway", vm.ConfirmText);
            Assert.False(vm.ShowTargets);
            Assert.Equal(new[] { CreatorViewModel.GetConflictMessage("ex", TestNameChecks.Conflict("ex").Conflicts[0]) }, vm.Warnings);
        }

        [Fact]
        public void ForRename_OffersRenameAnyway()
        {
            var vm = ConfirmViewModel.ForRename(TestNameChecks.Conflict("ex"));

            Assert.Equal("Rename to \u201cex\u201d anyway?", vm.Heading);
            Assert.Equal("Rename anyway", vm.ConfirmText);
            Assert.False(vm.ShowTargets);
            Assert.True(vm.ShowWarnings);
        }

        [Fact]
        public void Warnings_PutHiddenFirst_AndSumUpTheRest()
        {
            var conflicts = Enumerable.Range(1, 6)
                .Select(i => new NameConflict(@"C:\p" + i + @"\x.exe", i == 6 ? NameConflictEffect.Hidden : NameConflictEffect.Shadows, NameConflictSource.PathAfter))
                .ToList();

            var vm = ConfirmViewModel.ForCreate(new NameCheck("x", null, conflicts), "d");

            Assert.Equal(ConfirmViewModel.MaxConflictLines + 1, vm.Warnings.Count);
            Assert.Contains(@"C:\p6\x.exe", vm.Warnings[0]);
            Assert.Equal("2 more programs use this name.", vm.Warnings[^1]);
        }

        [Fact]
        public void Summary_ReadsEverythingInOrder_ForScreenReaders()
        {
            var check = new NameCheck("gmail", new Keycut { Name = "gmail", Target = "https://old" }, TestNameChecks.Conflict("gmail").Conflicts);
            var vm = ConfirmViewModel.ForCreate(check, "https://new");

            Assert.Equal("Replace \u201cgmail\u201d? " + ConfirmViewModel.ReplaceIntro + " Opens now: https://old. Will open: https://new. " + vm.Warnings[0], vm.Summary);
        }

        #endregion Text

        #region Dialog

        [Fact]
        public void TheDialog_IsBuiltFromTheSharedStyles()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(ConfirmViewModel.ForCreate(TestNameChecks.Existing("gmail"), "https://new.example.com"));
                try
                {
                    // Settings was the dialog this one was built like; since #218 it is a screen, so the shared styles are the yardstick
                    Assert.Equal(SizeToContent.Height, window.SizeToContent);
                    Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                    Assert.Equal(Application.Current.FindResource("AppFontFamily"), window.FontFamily);
                    Assert.Equal(Application.Current.FindResource("FontSizeBody"), window.FontSize);
                    Assert.Same(Application.Current.FindResource("SurfaceBrush"), window.Background);
                    Assert.Same(Application.Current.FindResource("TextBrush"), window.Foreground);
                    Assert.False(window.ShowInTaskbar);

                    var confirm = (Button)window.FindName("BtnConfirm");
                    var cancel = (Button)window.FindName("BtnCancel");
                    Assert.True(confirm.IsDefault);
                    Assert.True(cancel.IsCancel);
                    Assert.Same(Application.Current.FindResource("PrimaryButton"), confirm.Style);
                    Assert.Same(Application.Current.FindResource("SecondaryButton"), cancel.Style);
                    Assert.Equal("Replace", AutomationProperties.GetName(confirm));
                    Assert.Contains("Opens now", AutomationProperties.GetHelpText(confirm));

                    // A capped dialog's body must be reachable from the keyboard, since it holds nothing focusable
                    var body = (ScrollViewer)window.FindName("BodyScroller");
                    Assert.True(body.Focusable);
                    Assert.True(body.IsTabStop);
                    Assert.Same(Application.Current.FindResource("AppFocusVisual"), body.FocusVisualStyle);
                    Assert.Equal("Replace “gmail”?", AutomationProperties.GetName(body));

                    var heading = (TextBlock)window.FindName("HeadingText");
                    Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(heading));
                    Assert.True(((TextBlock)window.FindName("CurrentTargetText")).IsVisible);
                    Assert.False(((ItemsControl)window.FindName("WarningList")).IsVisible);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void TheDialog_ForAConflict_ShowsTheWarnings_AndNoTargets()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(ConfirmViewModel.ForCreate(TestNameChecks.Conflict("ex"), "https://new.example.com"));
                try
                {
                    Assert.False(((TextBlock)window.FindName("CurrentTargetText")).IsVisible);
                    Assert.True(((ItemsControl)window.FindName("WarningList")).IsVisible);
                    Assert.Equal("Create anyway", AutomationProperties.GetName((Button)window.FindName("BtnConfirm")));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        #endregion Dialog

        #region Helpers

        static ConfirmWindow ShowOffscreen(ConfirmViewModel viewModel)
        {
            var window = new ConfirmWindow(viewModel)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false
            };
            window.Show();
            Pump();
            window.UpdateLayout();
            return window;
        }

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

        #endregion Helpers
    }
}
