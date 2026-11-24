#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Run box (Phase 6.4): its keys pressed headless in its real window, with what it runs, creates and closes recorded.</summary>
    public class RunBoxTests
    {
        private sealed class Recorder
        {
            public List<string> Ran { get; } = new();

            public List<string> Created { get; } = new();

            public int Closed { get; set; }
        }

        private static IReadOnlyList<Keycut> Keycuts()
        {
            var now = DateTime.Now;
            return new[]
            {
                new Keycut { Name = "gmail", Kind = KeycutKind.Url, Target = "https://mail.google.com", FilePath = "/k/gmail", CountsRuns = true, Runs = 42, LastRun = now.AddDays(-2) },
                new Keycut { Name = "gmaps", Kind = KeycutKind.Url, Target = "https://maps.google.com", FilePath = "/k/gmaps", CountsRuns = true, Runs = 7, LastRun = now.AddDays(-1) },
                new Keycut { Name = "g", Kind = KeycutKind.Url, Target = "https://www.google.com", FilePath = "/k/g", CountsRuns = true, Runs = 1, LastRun = now.AddDays(-3) },
                new Keycut { Name = "dl", Kind = KeycutKind.Folder, Target = "$HOME/Downloads", FilePath = "/k/dl" },
            };
        }

        private static void WithBox(Action<RunBoxWindow, RunBoxViewModel, Recorder> body)
        {
            WithBox(Keycuts(), body);
        }

        private static void WithBox(IReadOnlyList<Keycut> keycuts, Action<RunBoxWindow, RunBoxViewModel, Recorder> body)
        {
            ThemeManager.Apply(Palette.Light);
            var recorder = new Recorder();
            var viewModel = new RunBoxViewModel(k => recorder.Ran.Add(k.Name), name => recorder.Created.Add(name), () => recorder.Closed++);
            viewModel.Keycuts = keycuts;
            viewModel.Reset();
            var window = new RunBoxWindow(viewModel);
            window.Show();
            window.FocusText();
            Dispatcher.UIThread.RunJobs();
            try
            {
                body(window, viewModel, recorder);
            }
            finally
            {
                window.Close();
            }
        }

        private static void Type(Window window, string text)
        {
            window.KeyTextInput(text);
            Dispatcher.UIThread.RunJobs();
        }

        private static IEnumerable<string> Names(RunBoxViewModel box)
        {
            return box.Rows.Select(r => r.Name);
        }

        [AvaloniaFact]
        public void NothingTyped_ShowsTheRecentKeycuts_LatestFirst_AndEnterRunsTheFirst()
        {
            WithBox((window, box, recorder) =>
            {
                Assert.Equal(new[] { "gmaps", "gmail", "g", "dl" }, Names(box));
                Assert.Equal(0, MainWindowTests.Named<ListBox>(window, "RunRows").SelectedIndex);
                Assert.Equal(string.Empty, box.Completion);
                Assert.Equal(" run", box.ReturnHint);

                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);

                Assert.Equal(new[] { "gmaps" }, recorder.Ran);
                Assert.Equal(1, recorder.Closed);
            });
        }

        [AvaloniaFact]
        public void NothingTyped_DownAndTab_WorkOnTheRecentKeycuts()
        {
            WithBox((window, box, recorder) =>
            {
                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                MainWindowTests.Press(window, Key.Tab, RawInputModifiers.None);

                Assert.Equal("gmail", box.Text);
                Assert.Equal(new[] { "gmail" }, Names(box));
                Assert.Empty(recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void OnlySpaces_AreNothingTyped_AndClearingTheTextBringsTheRecentKeycutsBack()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, " ");
                Assert.Equal(new[] { "gmaps", "gmail", "g", "dl" }, Names(box));
                MainWindowTests.Press(window, Key.Back, RawInputModifiers.None);

                Type(window, "d");
                Assert.Equal(new[] { "dl" }, Names(box));
                MainWindowTests.Press(window, Key.Back, RawInputModifiers.None);

                Assert.Equal(string.Empty, box.Text);
                Assert.Equal(new[] { "gmaps", "gmail", "g", "dl" }, Names(box));
            });
        }

        private static List<Keycut> Many(int count, int run)
        {
            return Enumerable.Range(1, count)
                .Select(n => new Keycut { Name = "k" + n.ToString("0000"), Target = "/t", FilePath = "/k/k" + n, CountsRuns = true, Runs = n <= run ? 1 : 0, LastRun = n <= run ? DateTime.Now.AddDays(-n) : default })
                .ToList();
        }

        private static ListBox List(RunBoxWindow window)
        {
            return MainWindowTests.Named<ListBox>(window, "RunRows");
        }

        private static ScrollViewer Scroller(RunBoxWindow window)
        {
            return List(window).GetVisualDescendants().OfType<ScrollViewer>().First();
        }

        private static ScrollBar Bar(RunBoxWindow window)
        {
            return List(window).GetVisualDescendants().OfType<ScrollBar>().Single(bar => bar.Orientation == Avalonia.Layout.Orientation.Vertical);
        }

        private static void ScrollTo(RunBoxWindow window, double offset)
        {
            Scroller(window).Offset = new Vector(0, offset);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(offset, Scroller(window).Offset.Y);
        }

        private static bool IsInView(RunBoxWindow window, int index)
        {
            return List(window).ContainerFromIndex(index) is { } row &&
                row.TranslatePoint(default, Scroller(window)) is { } at &&
                at.Y >= -0.5 && at.Y + row.Bounds.Height <= Scroller(window).Bounds.Height + 0.5;
        }

        private static void Click(RunBoxWindow window, Visual at, Point point)
        {
            var where = at.TranslatePoint(point, window)!.Value;
            window.MouseDown(where, MouseButton.Left);
            window.MouseUp(where, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        [AvaloniaFact]
        public void FiveRowsShow_AndTheBoxIsAsHighWithFiveHundred()
        {
            var heights = new List<double>();
            foreach (var count in new[] { 3, 5, 6, 500 })
            {
                WithBox(Many(count, count), (window, box, recorder) =>
                {
                    Assert.Equal(count, box.Rows.Count);
                    Assert.Equal(Math.Min(count, RunBoxViewModel.VisibleRows) * 40, List(window).Bounds.Height);
                    Assert.Equal(count > RunBoxViewModel.VisibleRows, Bar(window).IsEffectivelyVisible);
                    Assert.Equal(Math.Min(count, RunBoxViewModel.VisibleRows), Enumerable.Range(0, count).Count(n => IsInView(window, n)));
                    heights.Add(window.Bounds.Height);
                });
            }

            Assert.Equal(heights[1], heights[2]);
            Assert.Equal(heights[1], heights[3]);
            Assert.True(heights[0] < heights[1]);
        }

        [AvaloniaFact]
        public void WithAThousandKeycuts_OnlyTheRowsNearTheViewAreMade()
        {
            WithBox(Many(1000, 500), (window, box, recorder) =>
            {
                Assert.InRange(List(window).GetRealizedContainers().Count(), RunBoxViewModel.VisibleRows, 30);
                Assert.Equal(28, Bar(window).GetVisualDescendants().OfType<Thumb>().Single().Bounds.Height);

                ScrollTo(window, 20000);
                Assert.InRange(List(window).GetRealizedContainers().Count(), RunBoxViewModel.VisibleRows, 30);

                Type(window, "k");
                Assert.Equal(1000, box.Rows.Count);
                Assert.InRange(List(window).GetRealizedContainers().Count(), RunBoxViewModel.VisibleRows, 30);
            });
        }

        [AvaloniaFact]
        public void Down_GoesPastTheFifthRow_ToTheLast_InView_AndUpComesBack()
        {
            WithBox(Many(8, 8), (window, box, recorder) =>
            {
                for (var n = 1; n < 8; n++)
                {
                    MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(n, box.SelectedIndex);
                    Assert.True(IsInView(window, n), "row " + n);
                }

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                Assert.Equal(7, box.SelectedIndex);

                for (var n = 6; n >= 0; n--)
                {
                    MainWindowTests.Press(window, Key.Up, RawInputModifiers.None);
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(IsInView(window, n), "row " + n);
                }

                MainWindowTests.Press(window, Key.PageDown, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(5, box.SelectedIndex);
                Assert.True(IsInView(window, 5));
                MainWindowTests.Press(window, Key.PageUp, RawInputModifiers.None);
                Assert.Equal(0, box.SelectedIndex);
            });
        }

        [AvaloniaFact]
        public void ScrollingLeavesTheSelectionAlone_EnterRunsItOutOfView_AndDownBringsItBack()
        {
            WithBox(Many(100, 100), (window, box, recorder) =>
            {
                ScrollTo(window, 2000);
                Assert.Equal(0, box.SelectedIndex);
                Assert.False(IsInView(window, 0));

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.True(IsInView(window, 1));

                ScrollTo(window, 2000);
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
                Assert.Equal(new[] { "k0002" }, recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void TheWheelScrolls_WithoutRunningOrSelecting()
        {
            WithBox(Many(100, 100), (window, box, recorder) =>
            {
                window.MouseWheel(List(window).TranslatePoint(new Point(100, 100), window)!.Value, new Vector(0, -3));
                Dispatcher.UIThread.RunJobs();

                Assert.True(Scroller(window).Offset.Y > 0);
                Assert.Equal(0, box.SelectedIndex);
                Assert.Empty(recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void ANewList_KeepsTheScrolling_AndTypingOrOpeningAgainGoesToTheTop()
        {
            WithBox(Many(100, 100), (window, box, recorder) =>
            {
                ScrollTo(window, 1000);
                box.Keycuts = Many(100, 100);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1000, Scroller(window).Offset.Y);
                Assert.Equal(0, List(window).SelectedIndex);

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                ScrollTo(window, 1000);
                box.Keycuts = Many(100, 100);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1000, Scroller(window).Offset.Y);
                Assert.Equal("k0002", box.Selected!.Name);

                Type(window, "k");
                Assert.Equal(0, Scroller(window).Offset.Y);
                Assert.True(IsInView(window, 0));

                ScrollTo(window, 1000);
                box.Reset();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, Scroller(window).Offset.Y);
                Assert.Equal(0, List(window).SelectedIndex);
            });
        }

        [AvaloniaFact]
        public void TheScrollBar_ScrollsWithoutRunning_AndTheTextBoxKeepsTheFocus()
        {
            WithBox(Many(100, 100), (window, box, recorder) =>
            {
                var bar = Bar(window);
                Click(window, bar, new Point(bar.Bounds.Width / 2, bar.Bounds.Height - 10));
                Assert.True(Scroller(window).Offset.Y > 0);

                ScrollTo(window, 0);
                var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
                var from = thumb.TranslatePoint(new Point(6, thumb.Bounds.Height / 2), window)!.Value;
                window.MouseDown(from, MouseButton.Left);
                window.MouseMove(from + new Vector(0, 60));
                window.MouseMove(from + new Vector(-100, 80));
                window.MouseUp(from + new Vector(-100, 80), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                Assert.True(Scroller(window).Offset.Y > 0);
                Assert.Empty(recorder.Ran);
                Assert.Equal(0, recorder.Closed);
                Assert.True(MainWindowTests.Named<TextBox>(window, "RunText").IsFocused);
            });
        }

        [AvaloniaFact]
        public void AClickOnARow_RunsIt_ButNotARightClickOrADragOffIt()
        {
            WithBox(Many(100, 100), (window, box, recorder) =>
            {
                ScrollTo(window, 400);
                var row = List(window).ContainerFromIndex(12)!;
                var at = row.TranslatePoint(new Point(200, 20), window)!.Value;

                window.MouseDown(at, MouseButton.Right);
                window.MouseUp(at, MouseButton.Right);
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at + new Vector(0, 45), MouseButton.Left);
                Assert.Empty(recorder.Ran);
                Assert.True(MainWindowTests.Named<TextBox>(window, "RunText").IsFocused);

                Click(window, row, new Point(200, 20));
                Assert.Equal(new[] { "k0013" }, recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void TheLine_IsAboveTheFirstKeycutNeverRun_OnlyWithNothingTyped_AndOnlyBetweenTheTwo()
        {
            WithBox(Many(4, 2), (window, box, recorder) =>
            {
                Assert.Equal(new[] { false, false, true, false }, box.Rows.Select(r => r.StartsNotRun));
                var line = List(window).ContainerFromIndex(2)!.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("notRunLine"));
                Assert.True(line.IsEffectivelyVisible);
                Assert.Equal(1, line.Bounds.Height);
                Assert.Equal(40, List(window).ContainerFromIndex(2)!.Bounds.Height);

                Type(window, "k");
                Assert.DoesNotContain(box.Rows, r => r.StartsNotRun);
            });

            WithBox(Many(4, 4), (window, box, recorder) => Assert.DoesNotContain(box.Rows, r => r.StartsNotRun));
            WithBox(Many(4, 0), (window, box, recorder) => Assert.DoesNotContain(box.Rows, r => r.StartsNotRun));
        }

        [AvaloniaFact]
        public void ANewList_KeepsTheRowChosenWithDown_AndTheFirstRowOtherwise()
        {
            WithBox((window, box, recorder) =>
            {
                var loaded = Keycuts().Select(k => k.Name == "g" ? k with { LastRun = DateTime.Now } : k).ToList();

                box.Keycuts = loaded;
                Assert.Equal(new[] { "g", "gmaps", "gmail", "dl" }, Names(box));
                Assert.Equal("g", box.Selected!.Name);

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                box.Keycuts = Keycuts();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("gmaps", box.Selected!.Name);

                box.Keycuts = loaded;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("gmaps", box.Selected!.Name);
                Assert.Equal(1, MainWindowTests.Named<ListBox>(window, "RunRows").SelectedIndex);

                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
                Assert.Equal(new[] { "gmaps" }, recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void ANewList_WithoutTheChosenRow_SelectsTheFirst()
        {
            WithBox((window, box, recorder) =>
            {
                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                Assert.Equal("gmail", box.Selected!.Name);

                box.Keycuts = Keycuts().Where(k => k.Name != "gmail").ToList();

                Assert.Equal("gmaps", box.Selected!.Name);
            });
        }

        [AvaloniaFact]
        public void OpeningItAgain_KeepsTheKeycuts_WithTheOneJustRunFirst()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "gmail");
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);

                box.Reset();

                Assert.Equal(string.Empty, box.Text);
                Assert.Equal(new[] { "gmail", "gmaps", "g", "dl" }, Names(box));
                Assert.Equal("43 runs", box.Rows[0].RunsText);
            });
        }

        [AvaloniaFact]
        public void RunningAKeycutThatDoesntCountItsRuns_LeavesTheRecentOnesAlone()
        {
            var keycuts = Keycuts().Append(new Keycut { Name = "old", Target = "/t", FilePath = "/k/old" }).ToList();

            WithBox(keycuts, (window, box, recorder) =>
            {
                Type(window, "old");
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
                Assert.Equal(new[] { "old" }, recorder.Ran);

                box.Reset();

                Assert.Equal(new[] { "gmaps", "gmail", "g", "dl", "old" }, Names(box));
            });
        }

        [AvaloniaFact]
        public void NothingTyped_AndNothingEverRun_SelectsNoRowUntilDown()
        {
            WithBox(Many(8, 0), (window, box, recorder) =>
            {
                Assert.Equal(8, box.Rows.Count);
                foreach (var key in new[] { Key.Enter, Key.Tab, Key.Up, Key.PageUp })
                {
                    MainWindowTests.Press(window, key, RawInputModifiers.None);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(-1, box.SelectedIndex);
                    Assert.Equal(-1, List(window).SelectedIndex);
                }

                box.Keycuts = Many(8, 0);
                Assert.Equal(-1, box.SelectedIndex);
                Assert.Equal(string.Empty, box.Text);
                Assert.Empty(recorder.Ran);
                Assert.Equal(0, recorder.Closed);

                Type(window, "k");
                Assert.Equal(0, box.SelectedIndex);
                MainWindowTests.Press(window, Key.Back, RawInputModifiers.None);
                Assert.Equal(-1, box.SelectedIndex);

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, List(window).SelectedIndex);
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
                Assert.Equal(new[] { "k0001" }, recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void TheHintLine_EndsWithHowManyKeycutsThereAre_WhateverIsTyped()
        {
            WithBox(Many(8, 2), (window, box, recorder) =>
            {
                var count = MainWindowTests.Named<TextBlock>(window, "CountText");
                Assert.Equal("8 keycuts", count.Text);

                Type(window, "k0001");
                Assert.Single(box.Rows);
                Assert.Equal("8 keycuts", count.Text);

                box.Keycuts = Many(1, 1);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("1 keycut", count.Text);

                box.Keycuts = Array.Empty<Keycut>();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("0 keycuts", count.Text);
            });
        }

        [AvaloniaFact]
        public void TheHintLine_IsOnOneLine_CenteredInItsBar()
        {
            WithBox((window, box, recorder) =>
            {
                var line = MainWindowTests.Named<Grid>(window, "HintLine");
                var hints = line.GetVisualDescendants().OfType<TextBlock>().ToList();
                Assert.Equal(6, hints.Count);

                foreach (var hint in hints)
                {
                    var middle = hint.TranslatePoint(new Point(0, hint.Bounds.Height / 2), line)!.Value.Y;
                    Assert.InRange(middle, line.Bounds.Height / 2 - 0.5, line.Bounds.Height / 2 + 0.5);
                }

                // The words share a baseline; the arrows, from a fallback font, have their own line
                var baselines = hints.Where(h => h.Text != "↑ ↓").Select(h => h.TranslatePoint(new Point(0, h.TextLayout.Baseline), line)!.Value.Y).ToList();
                Assert.InRange(baselines.Max() - baselines.Min(), 0, 0.5);
            });
        }

        [AvaloniaFact]
        public void NoKeycuts_ShowsOnlyTheTextBox_AndEnterDoesNothing()
        {
            WithBox(Array.Empty<Keycut>(), (window, box, recorder) =>
            {
                Assert.False(box.HasRows);
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                Assert.Empty(recorder.Ran);
                Assert.Equal(0, recorder.Closed);
            });
        }

        [AvaloniaFact]
        public void Enter_RunsTheExactName_First_AndClosesTheBox()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "g");
                Assert.Equal(new[] { "g", "gmail", "gmaps" }, box.Rows.Select(r => r.Name));

                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);

                Assert.Equal(new[] { "g" }, recorder.Ran);
                Assert.Equal(1, recorder.Closed);
            });
        }

        [AvaloniaFact]
        public void UpAndDown_Choose_AndStopAtTheEnds_ThenEnterRunsTheChoice()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "gma");
                Assert.Equal("gmail", box.Selected!.Name);

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                Assert.Equal("gmaps", box.Selected!.Name);
                MainWindowTests.Press(window, Key.Up, RawInputModifiers.None);
                MainWindowTests.Press(window, Key.Up, RawInputModifiers.None);
                Assert.Equal("gmail", box.Selected!.Name);

                MainWindowTests.Press(window, Key.Down, RawInputModifiers.None);
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
                Assert.Equal(new[] { "gmaps" }, recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void Tab_CompletesTheName_AndTheGrayRestShowsWhatItWouldAdd()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "gm");
                Assert.Equal("ail", box.Completion);

                MainWindowTests.Press(window, Key.Tab, RawInputModifiers.None);

                Assert.Equal("gmail", box.Text);
                Assert.Equal("gmail", MainWindowTests.Named<TextBox>(window, "RunText").Text);
                Assert.Equal(string.Empty, box.Completion);
                Assert.Empty(recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void Esc_Closes_WithoutRunning()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "gmail");
                MainWindowTests.Press(window, Key.Escape, RawInputModifiers.None);

                Assert.Equal(1, recorder.Closed);
                Assert.Empty(recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void ANameThatMatchesNothing_OffersToCreateIt_AndEnterOpensTheCreatorWithIt()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "gmial");
                var row = Assert.Single(box.Rows);
                Assert.True(row.IsCreate);
                Assert.Equal("Create “gmial”…", row.Name);
                Assert.Equal(" create", box.ReturnHint);

                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);

                Assert.Equal(new[] { "gmial" }, recorder.Created);
                Assert.Equal(1, recorder.Closed);
                Assert.Empty(recorder.Ran);
            });
        }

        [AvaloniaFact]
        public void ARow_ReadsAsItsNameTypeTargetAndRuns()
        {
            WithBox((window, box, recorder) =>
            {
                Type(window, "gmail");
                Assert.Equal("gmail, URL, https://mail.google.com, 42 runs", box.Rows[0].AutomationName);
                Assert.Equal("Run a keycut", Avalonia.Automation.AutomationProperties.GetName(MainWindowTests.Named<TextBox>(window, "RunText")));
            });
        }
    }
}
