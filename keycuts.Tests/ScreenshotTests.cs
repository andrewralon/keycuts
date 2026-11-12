#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The README's screenshots (#170), made from the real views. It writes nothing unless
    /// <see cref="OutputVariable"/> names a folder, so a normal test run only passes through it; run
    /// <c>tools/New-Screenshots.ps1</c> to refresh <c>docs/images</c>, and do that when preparing a release.
    ///
    /// Each view is shown off screen with sample keycuts (never a real folder, the registry or PATH), in the light and the
    /// dark palette, and rendered at twice its size. That is the window's contents without its frame, so nothing depends
    /// on the machine's theme, display or what is open. The dates are relative to now, so they read the same every time.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class ScreenshotTests
    {
        /// <summary>The environment variable naming the folder the screenshots are written to.</summary>
        public const string OutputVariable = "KEYCUTS_SCREENSHOTS_OUT";

        private const double Scale = 2;

        /// <summary>Sample keycuts: the README's examples, plus one whose target is missing.</summary>
        private static IReadOnlyList<Keycut> SampleKeycuts()
        {
            var now = DateTime.Now;

            return new List<Keycut>
            {
                Sample("npp", KeycutKind.File, ShortcutType.File, "Notepad++", @"C:\Program Files\Notepad++\notepad++.exe", now.AddDays(-3)),
                Sample("gmail", KeycutKind.Url, ShortcutType.Url, "Work email", "https://mail.google.com/mail/u/0/#inbox", now.AddHours(-5)),
                Sample("db", KeycutKind.Folder, ShortcutType.Folder, "Dropbox folder", @"%USERPROFILE%\Dropbox", now.AddDays(-40)),
                Sample("ss", KeycutKind.Folder, ShortcutType.Folder, "Screenshots", @"%USERPROFILE%\Pictures\Screenshots", now.AddDays(-200)),
                Sample("timesheet", KeycutKind.Url, ShortcutType.Url, "This week's timesheet", "https://timesheet.example.com/week", now.AddMinutes(-12)),
                Sample("rb", KeycutKind.Folder, ShortcutType.CLSIDKey, "Recycle Bin", "::{645FF040-5081-101B-9F08-00AA002F954E}", now.AddDays(-90)),
                Sample("old-report", KeycutKind.File, ShortcutType.File, "Quarterly report", @"D:\Reports\2019-q3.xlsx", now.AddDays(-1.5), TargetStatus.Missing),
            };
        }

        private static Keycut Sample(string name, KeycutKind kind, ShortcutType type, string description, string target, DateTime modified, TargetStatus status = TargetStatus.Ok)
        {
            return new Keycut
            {
                Name = name,
                Kind = kind,
                RawType = type,
                Description = description,
                Target = target,
                FilePath = @"C:\Users\sample\Shortcuts\" + name + ".bat",
                Modified = modified,
                TargetStatus = status
            };
        }

        [Fact]
        public void RenderTheReadmeScreenshots()
        {
            var folder = Environment.GetEnvironmentVariable(OutputVariable);

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);

            RunSta(() =>
            {
                EnsureApplication();

                try
                {
                    foreach (var (palette, suffix) in new[] { ("Light", "light"), ("Dark", "dark") })
                    {
                        SetPalette(palette);
                        RenderAll(folder, suffix);
                    }
                }
                finally
                {
                    SetPalette("Light");
                }
            });
        }

        private static void RenderAll(string folder, string suffix)
        {
            var noop = new RelayCommand(() => { });

            // The creator as it's used: a target dropped, a name typed, Enter next
            var creator = new CreatorViewModel(noop, noop, checkName: TestNameChecks.NoWarnings) { KeycutCount = 7 };
            creator.Target = "https://mail.google.com/mail/u/0/#inbox";
            creator.TargetKind = KeycutKind.Url;
            creator.ShortcutName = "gmail";
            Render(Host(new CreatorView(), creator, 440, null), folder, "creator-" + suffix);

            // The amber warning for a name something else answers to (#153)
            var warned = new CreatorViewModel(noop, noop, checkName: TestNameChecks.NoWarnings) { KeycutCount = 7 };
            warned.Target = @"C:\Program Files\Notepad++\notepad++.exe";
            warned.TargetKind = KeycutKind.File;
            warned.ShortcutName = "notepad";
            warned.SetNameWarning(TestNameChecks.Conflict("notepad", NameConflictEffect.Hidden, @"C:\Windows\notepad.exe"));
            Render(Host(new CreatorView(), warned, 440, null), folder, "name-warning-" + suffix);

            var manager = new ManagerViewModel(noop, noop);
            manager.ShowKeycuts(SampleKeycuts());
            Render(Host(new ManagerView(), manager, 960, 560), folder, "manager-" + suffix);

            // The Edit form (#180)
            var keycut = SampleKeycuts()[1];
            Render(Show(new EditWindow(new EditKeycutViewModel(keycut, edit => Task.FromResult(keycut)))), folder, "edit-" + suffix);

            // Settings, with the update row the real app has (#171)
            var updates = UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState(), Runner.Version);
            Render(Show(new SettingsWindow(new SettingsViewModel(new SampleStore(), directoryExists: _ => true, updates: updates))), folder, "settings-" + suffix);
        }

        /// <summary>Settings as a user typically has them, kept in memory: nothing reads or writes the registry.</summary>
        private sealed class SampleStore : ISettingsStore
        {
            public Settings Load()
            {
                return new Settings
                {
                    OutputFolder = @"C:\Users\sample\Shortcuts",
                    AskBeforeReplacing = true,
                    RightClickContextMenus = true,
                    UseEnvironmentVariables = true,
                    CheckForUpdates = true
                };
            }

            public string? Save(Settings settings)
            {
                return null;
            }
        }

        #region Rendering

        private static Window Host(FrameworkElement view, object viewModel, double width, double? height)
        {
            var window = new Window { Content = view, DataContext = viewModel, Width = width };

            if (height.HasValue)
            {
                window.Height = height.Value;
            }
            else
            {
                window.SizeToContent = SizeToContent.Height;
            }

            return Show(window);
        }

        private static Window Show(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Show();
            Pump();
            window.UpdateLayout();
            Pump();
            return window;
        }

        /// <summary>Writes the window's content as <paramref name="name"/>.png, twice its size, and closes the window.</summary>
        private static void Render(Window window, string folder, string name)
        {
            try
            {
                var element = (FrameworkElement)window.Content;

                // A dialog's surface is the window's own background, not part of its content: paint it under the content
                if (element is Panel { Background: null } panel)
                {
                    panel.Background = (Brush)Application.Current.FindResource("SurfaceBrush");
                }

                element.UpdateLayout();
                var width = element.ActualWidth;
                var height = element.ActualHeight;
                var bitmap = new RenderTargetBitmap((int)(width * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
                var visual = new DrawingVisual();

                using (var context = visual.RenderOpen())
                {
                    // An absolute viewbox: left to itself the brush stretches to the descendants' bounds and clips the top
                    var brush = new VisualBrush(element) { Viewbox = new Rect(0, 0, width, height), ViewboxUnits = BrushMappingMode.Absolute };
                    context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
                }

                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (var stream = File.Create(Path.Combine(folder, name + ".png")))
                {
                    encoder.Save(stream);
                }
            }
            finally
            {
                window.Close();
            }
        }

        private static void SetPalette(string name)
        {
            Application.Current.Resources.MergedDictionaries[0].Source =
                new Uri("pack://application:,,,/keycuts.GUI;component/Themes/" + name + ".xaml");
        }

        private static void RunSta(Action action)
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

        private static void EnsureApplication()
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

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        #endregion Rendering
    }
}
