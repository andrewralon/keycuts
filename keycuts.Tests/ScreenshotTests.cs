#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                        RenderWalkthrough(folder, suffix);
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
            Render(HideCarets(Host(new CreatorView(), creator, 440, null)), folder, "creator-" + suffix);

            var manager = new ManagerViewModel(noop, noop);
            manager.ShowKeycuts(SampleKeycuts());
            Render(HideCarets(Host(new ManagerView(), manager, 960, 560)), folder, "manager-" + suffix);

            // Settings, with the update row the real app has (#171)
            // without the version row (the version and Check now), so the screenshot doesn't go stale at every release
            var updates = UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState());
            var settings = new SettingsViewModel(new SampleStore(), directoryExists: _ => true, updates: updates) { ShowVersionRow = false };
            Render(Show(new SettingsWindow(settings)), folder, "settings-" + suffix);
        }

        #region Walkthrough

        private const double CaptionHeight = 36;

        /// <summary>
        /// The README's animation: the real creator taken through its real states (a target filled in, a name typed letter by
        /// letter, Created), each with a caption under it, then a card for the step that happens outside the app. It is not a
        /// screen recording, so Win+R can't be shown: the card says it in words and key caps instead of drawing a Run dialog.
        /// </summary>
        private static void RenderWalkthrough(string folder, string suffix)
        {
            var noop = new RelayCommand(() => { });
            var creator = new CreatorViewModel(noop, noop, checkName: TestNameChecks.NoWarnings) { KeycutCount = 7, SuccessDuration = TimeSpan.FromMinutes(1) };
            var window = Host(new CreatorView(), creator, 440, null);
            var captionBar = NewCaptionBar(window);
            var frames = new List<GifFrame>();

            try
            {
                var view = (FrameworkElement)window.Content;
                var nameBox = (TextBox)view.FindName("TextboxShortcut");

                void Add(string caption, int delayCentiseconds)
                {
                    Pump();
                    window.UpdateLayout();
                    Pump();
                    frames.Add(Frame(Pixels(Capture(view)), Pixels(captionBar.Render(caption)), delayCentiseconds));
                }

                Add("Drop, paste or type what you want to open", 150);

                creator.Target = "https://mail.google.com/mail/u/0/#inbox";
                creator.TargetKind = KeycutKind.Url;
                nameBox.Focus();
                Keyboard.Focus(nameBox);
                Add("keycuts works out what it is", 130);

                const string name = "gmail";

                for (var length = 1; length <= name.Length; length++)
                {
                    creator.ShortcutName = name.Substring(0, length);
                    nameBox.CaretIndex = length;
                    Add(length < name.Length ? "Give it a short name" : "Give it a short name, then press Enter", length < name.Length ? 18 : 100);
                }

                // What a successful create leaves on screen (the beep that goes with it is the view's, so it's not played here)
                creator.SetSuccess(name);
                Add("Created", 150);

                var first = frames[0];
                var card = NewCard(first.Width / Scale, first.Height / Scale);

                try
                {
                    frames.Add(new GifFrame(first.Width, first.Height, Pixels(Capture((FrameworkElement)card.Content)), 380));
                }
                finally
                {
                    card.Close();
                }
            }
            finally
            {
                captionBar.Close();
                window.Close();
            }

            File.WriteAllBytes(Path.Combine(folder, "walkthrough-" + suffix + ".gif"), GifWriter.Write(frames));
        }

        /// <summary>The creator's rendering with its caption strip under it, as one frame.</summary>
        private static GifFrame Frame(PixelBuffer view, PixelBuffer caption, int delayCentiseconds)
        {
            if (view.Width != caption.Width)
            {
                throw new InvalidOperationException("The caption strip is " + caption.Width + " px wide and the creator " + view.Width + ".");
            }

            return new GifFrame(view.Width, view.Height + caption.Height, view.Bgra.Concat(caption.Bgra).ToArray(), delayCentiseconds);
        }

        private sealed class PixelBuffer
        {
            public PixelBuffer(int width, int height, byte[] bgra)
            {
                Width = width;
                Height = height;
                Bgra = bgra;
            }

            public int Width { get; }

            public int Height { get; }

            public byte[] Bgra { get; }

            public static implicit operator byte[](PixelBuffer buffer) => buffer.Bgra;
        }

        private static PixelBuffer Pixels(BitmapSource bitmap)
        {
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            return new PixelBuffer(bitmap.PixelWidth, bitmap.PixelHeight, pixels);
        }

        /// <summary>A strip the width of the creator that says what is happening, drawn in the app's own colors and font.</summary>
        private sealed class CaptionBar
        {
            private readonly Window window;

            private readonly TextBlock text;

            private readonly Border border;

            public CaptionBar(Window window, TextBlock text, Border border)
            {
                this.window = window;
                this.text = text;
                this.border = border;
            }

            public BitmapSource Render(string caption)
            {
                text.Text = caption;
                Pump();
                window.UpdateLayout();
                Pump();
                return Capture(border);
            }

            public void Close()
            {
                window.Close();
            }
        }

        private static CaptionBar NewCaptionBar(Window creatorWindow)
        {
            var text = new TextBlock
            {
                FontFamily = (FontFamily)Application.Current.FindResource("AppFontFamily"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border = new Border
            {
                Width = ((FrameworkElement)creatorWindow.Content).ActualWidth,
                Height = CaptionHeight,
                Background = (Brush)Application.Current.FindResource("SurfaceSubtleBrush"),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = text
            };
            var window = new Window { Content = border, SizeToContent = SizeToContent.WidthAndHeight };
            Show(window);
            return new CaptionBar(window, text, border);
        }

        /// <summary>The step outside the app, in words and key caps: press Win+R, type the name, press Enter.</summary>
        private static Window NewCard(double width, double height)
        {
            Brush Resource(string key) => (Brush)Application.Current.FindResource(key);
            var font = (FontFamily)Application.Current.FindResource("AppFontFamily");

            TextBlock Words(string text, double size, string brush, FontWeight? weight = null)
            {
                return new TextBlock
                {
                    Text = text,
                    FontFamily = font,
                    FontSize = size,
                    FontWeight = weight ?? FontWeights.Normal,
                    Foreground = Resource(brush),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            Border KeyCap(string label, bool accent = false)
            {
                return new Border
                {
                    Background = Resource("SurfaceSubtleBrush"),
                    BorderBrush = Resource("BorderStrongBrush"),
                    BorderThickness = new Thickness(1, 1, 1, 3),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(16, 6, 16, 6),
                    Margin = new Thickness(6, 0, 6, 0),
                    Child = Words(label, 20, accent ? "AccentBrush" : "TextBrush", FontWeights.Bold)
                };
            }

            StackPanel Row(params UIElement[] items)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };

                foreach (var item in items)
                {
                    row.Children.Add(item);
                }

                return row;
            }

            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var heading = Words("Then, from anywhere in Windows", 17, "TextSecondaryBrush", FontWeights.SemiBold);
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(heading);
            content.Children.Add(Row(KeyCap("Win"), Words("+", 20, "TextSecondaryBrush", FontWeights.Bold), KeyCap("R")));
            content.Children.Add(Row(Words("type", 17, "TextSecondaryBrush"), KeyCap("gmail", accent: true), Words("and press", 17, "TextSecondaryBrush"), KeyCap("Enter")));
            var result = Words("and it opens", 17, "TextSecondaryBrush", FontWeights.SemiBold);
            result.HorizontalAlignment = HorizontalAlignment.Center;
            result.Margin = new Thickness(0, 22, 0, 0);
            content.Children.Add(result);

            var card = new Border { Width = width, Height = height, Background = Resource("SurfaceBrush"), Child = content };
            var window = new Window { Content = card, SizeToContent = SizeToContent.WidthAndHeight };
            return Show(window);
        }

        #endregion Walkthrough

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

        /// <summary>
        /// A still picture shouldn't show a text caret caught mid-blink, as a blip in front of the typed text. The box keeps its
        /// focus look; only the caret is made invisible, which is the one thing a screenshot can't tell is a blink.
        /// </summary>
        private static Window HideCarets(Window window)
        {
            void Hide(DependencyObject node)
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);

                    if (child is TextBox box)
                    {
                        box.CaretBrush = Brushes.Transparent;
                    }

                    Hide(child);
                }
            }

            Hide(window);
            return window;
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
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(Capture((FrameworkElement)window.Content)));

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

        /// <summary>The element as pixels, <see cref="Scale"/> times its size.</summary>
        private static BitmapSource Capture(FrameworkElement element)
        {
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
            return bitmap;
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
