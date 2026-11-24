#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
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
                Assert.Equal(new[] { "gmaps", "gmail", "g" }, Names(box));
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
                Assert.Equal(new[] { "gmaps", "gmail", "g" }, Names(box));
                MainWindowTests.Press(window, Key.Back, RawInputModifiers.None);

                Type(window, "d");
                Assert.Equal(new[] { "dl" }, Names(box));
                MainWindowTests.Press(window, Key.Back, RawInputModifiers.None);

                Assert.Equal(string.Empty, box.Text);
                Assert.Equal(new[] { "gmaps", "gmail", "g" }, Names(box));
            });
        }

        [AvaloniaFact]
        public void TheRecentKeycuts_AreAtMostMaxRows()
        {
            var keycuts = Enumerable.Range(1, RunBoxViewModel.MaxRows + 2)
                .Select(n => new Keycut { Name = "k" + n, Target = "/t", FilePath = "/k/k" + n, CountsRuns = true, Runs = 1, LastRun = DateTime.Now.AddDays(-n) })
                .ToList();

            WithBox(keycuts, (window, box, recorder) => Assert.Equal(keycuts.Take(RunBoxViewModel.MaxRows).Select(k => k.Name), Names(box)));
        }

        [AvaloniaFact]
        public void ANewList_KeepsTheRowChosenWithDown_AndTheFirstRowOtherwise()
        {
            WithBox((window, box, recorder) =>
            {
                var loaded = Keycuts().Select(k => k.Name == "g" ? k with { LastRun = DateTime.Now } : k).ToList();

                box.Keycuts = loaded;
                Assert.Equal(new[] { "g", "gmaps", "gmail" }, Names(box));
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
                Assert.Equal(new[] { "gmail", "gmaps", "g" }, Names(box));
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

                Assert.Equal(new[] { "gmaps", "gmail", "g" }, Names(box));
            });
        }

        [AvaloniaFact]
        public void NothingTyped_AndNothingEverRun_ShowsNoRows_AndEnterDoesNothing()
        {
            WithBox(new[] { new Keycut { Name = "dl", Target = "/t", FilePath = "/k/dl", CountsRuns = true, Runs = 0 } }, (window, box, recorder) =>
            {
                Assert.False(box.HasRows);
                MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
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
