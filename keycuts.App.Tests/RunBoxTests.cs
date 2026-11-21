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
                new Keycut { Name = "gmail", Kind = KeycutKind.Url, Target = "https://mail.google.com", FilePath = "/k/gmail", Runs = 42, LastRun = now },
                new Keycut { Name = "gmaps", Kind = KeycutKind.Url, Target = "https://maps.google.com", FilePath = "/k/gmaps", Runs = 7, LastRun = now },
                new Keycut { Name = "g", Kind = KeycutKind.Url, Target = "https://www.google.com", FilePath = "/k/g", Runs = 1, LastRun = now },
                new Keycut { Name = "dl", Kind = KeycutKind.Folder, Target = "$HOME/Downloads", FilePath = "/k/dl" },
            };
        }

        private static void WithBox(Action<RunBoxWindow, RunBoxViewModel, Recorder> body)
        {
            ThemeManager.Apply(Palette.Light);
            var recorder = new Recorder();
            var viewModel = new RunBoxViewModel(k => recorder.Ran.Add(k.Name), name => recorder.Created.Add(name), () => recorder.Closed++);
            viewModel.Reset(Keycuts());
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

        [AvaloniaFact]
        public void NothingTyped_ShowsNoRows_AndEnterDoesNothing()
        {
            WithBox((window, box, recorder) =>
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
