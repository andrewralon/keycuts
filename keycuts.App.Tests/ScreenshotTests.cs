using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The README's screenshots and walkthrough GIFs (tools/New-Screenshots.ps1; WPF's ScreenshotTests, ported): the real main
    /// window over sample keycuts, drawn by Skia at twice its size, transparent behind the torn corner. Inert unless
    /// <see cref="OutputVariable"/> names a folder. Nothing reads the registry, PATH or a keycuts folder: the stores and the name
    /// check are fakes.
    /// </summary>
    public class ScreenshotTests
    {
        public const string OutputVariable = "KEYCUTS_SCREENSHOTS_OUT";

        const double Scale = 2;

        const double CaptionHeight = 36;

        static IReadOnlyList<Keycut> SampleKeycuts()
        {
            var now = DateTime.Now;

            // Phase 5.6: a Mac's keycuts, the same rows in its own paths (no CLSID keycuts there)
            if (OperatingSystem.IsMacOS())
            {
                return new List<Keycut>
                {
                    Sample("code", KeycutKind.File, ShortcutType.File, "Visual Studio Code", "/Applications/Visual Studio Code.app", now.AddDays(-3)) with { Runs = 86, LastRun = now.AddHours(-2) },
                    Sample("gmail", KeycutKind.Url, ShortcutType.Url, "Work email", "https://mail.google.com/mail/u/0/#inbox", now.AddHours(-5)) with { Runs = 412, LastRun = now.AddMinutes(-20) },
                    Sample("db", KeycutKind.Folder, ShortcutType.Folder, "Dropbox folder", "$HOME/Dropbox", now.AddDays(-40)) with { Runs = 9, LastRun = now.AddDays(-4) },
                    Sample("ss", KeycutKind.Folder, ShortcutType.Folder, "Screenshots", "$HOME/Pictures/Screenshots", now.AddDays(-200)) with { Runs = 0 },
                    Sample("timesheet", KeycutKind.Url, ShortcutType.Url, "This week's timesheet", "https://timesheet.example.com/week", now.AddMinutes(-12)) with { Runs = 31, LastRun = now.AddDays(-1.2) },
                    Sample("dl", KeycutKind.Folder, ShortcutType.Folder, "Downloads", "$HOME/Downloads", now.AddDays(-90)) with { Runs = 3, LastRun = now.AddDays(-62) },
                    Sample("old-report", KeycutKind.File, ShortcutType.File, "Quarterly report", "/Volumes/Archive/Reports/2019-q3.xlsx", now.AddDays(-1.5), TargetStatus.Missing),
                };
            }

            return new List<Keycut>
            {
                Sample("npp", KeycutKind.File, ShortcutType.File, "Notepad++", @"C:\Program Files\Notepad++\notepad++.exe", now.AddDays(-3)) with { Runs = 86, LastRun = now.AddHours(-2) },
                Sample("gmail", KeycutKind.Url, ShortcutType.Url, "Work email", "https://mail.google.com/mail/u/0/#inbox", now.AddHours(-5)) with { Runs = 412, LastRun = now.AddMinutes(-20) },
                Sample("db", KeycutKind.Folder, ShortcutType.Folder, "Dropbox folder", @"%USERPROFILE%\Dropbox", now.AddDays(-40)) with { Runs = 9, LastRun = now.AddDays(-4) },
                Sample("ss", KeycutKind.Folder, ShortcutType.Folder, "Screenshots", @"%USERPROFILE%\Pictures\Screenshots", now.AddDays(-200)) with { Runs = 0 },
                Sample("timesheet", KeycutKind.Url, ShortcutType.Url, "This week's timesheet", "https://timesheet.example.com/week", now.AddMinutes(-12)) with { Runs = 31, LastRun = now.AddDays(-1.2) },
                Sample("rb", KeycutKind.Folder, ShortcutType.CLSIDKey, "Recycle Bin", "::{645FF040-5081-101B-9F08-00AA002F954E}", now.AddDays(-90)) with { Runs = 3, LastRun = now.AddDays(-62) },
                // Made before counting, with the lines taken out by hand: no record, which isn't 0
                Sample("old-report", KeycutKind.File, ShortcutType.File, "Quarterly report", @"D:\Reports\2019-q3.xlsx", now.AddDays(-1.5), TargetStatus.Missing),
            };
        }

        static readonly string SampleFolder = OperatingSystem.IsMacOS() ? "/Users/sample/Shortcuts" : @"C:\Users\sample\Shortcuts";

        static Keycut Sample(string name, KeycutKind kind, ShortcutType type, string description, string target, DateTime modified, TargetStatus status = TargetStatus.Ok)
        {
            return new Keycut
            {
                Name = name,
                Kind = kind,
                RawType = type,
                Description = description,
                Target = target,
                FilePath = Path.Combine(SampleFolder, name + ShortcutFile.Extension),
                Modified = modified,
                TargetStatus = status
            };
        }

        /// <summary>Settings as a user typically has them, kept in memory.</summary>
        sealed class SampleStore : ISettingsStore
        {
            public Settings Load()
            {
                return new Settings
                {
                    OutputFolder = SampleFolder,
                    AskBeforeReplacing = true,
                    RightClickContextMenus = true,
                    UseEnvironmentVariables = true,
                    CheckForUpdates = true
                };
            }

            public string? SaveOptions(Settings settings) => null;

            public FolderSaveResult SaveOutputFolder(string folder) => new(true, null);
        }

        /// <summary>The Run box shortcut at its default, so Settings shows the row; nothing is registered or stored.</summary>
        sealed class SampleRunBoxShortcut : IRunBoxShortcut
        {
            public RunBoxHotkey? Current => RunBoxHotkey.DefaultForThisSystem;

            public int Status => 0;

            public bool UsesWindowsKeys => OperatingSystem.IsWindows();

            public int Apply(RunBoxHotkey? hotkey) => 0;

            public void Pause(bool paused)
            {
            }
        }

        /// <summary>Open at login, off, so Settings shows the row; nothing is registered.</summary>
        sealed class SampleLoginItem : ILoginItem
        {
            public bool IsOn => false;

            public bool NeedsApproval => false;

            public string? Set(bool on) => null;
        }

        /// <summary>
        /// Settings as the real app shows it: the Run box, Open at login and (macOS) Run keycuts in Terminal rows are each offered only
        /// when their seam is set (#318). The shell path reads an empty temp home, so it never touches the real profile.
        /// </summary>
        static SettingsViewModel NewSettings(UpdateChecker updates)
        {
            var home = Directory.CreateTempSubdirectory("keycuts-screenshot-home").FullName;

            return new SettingsViewModel(
                new SampleStore(),
                directoryExists: _ => true,
                updates: updates,
                shellPath: OperatingSystem.IsMacOS() ? new MacShellPath(home, "/bin/zsh", "/usr/bin:/bin", Array.Empty<string>()) : null)
            {
                ShowVersionRow = false,
                RunBoxShortcut = new SampleRunBoxShortcut(),
                LoginItem = new SampleLoginItem(),
                // macOS 26 and later with a signed build: the Spotlight row
                SpotlightShortcutFile = OperatingSystem.IsMacOS() ? "/Applications/keycuts.app/Contents/Resources/Spotlight/Run keycut.shortcut" : null
            };
        }

        [AvaloniaFact]
        public void RenderTheReadmeScreenshots()
        {
            var folder = Environment.GetEnvironmentVariable(OutputVariable);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            try
            {
                foreach (var (palette, suffix) in new[] { (Palette.Light, "light"), (Palette.Dark, "dark") })
                {
                    ThemeManager.Apply(palette);
                    AppIconManager.Initialize(Settings.DefaultAppIcon, false);

                    // Phase 5.6: on macOS its own pictures (creator-light-mac.png…), so the Windows ones stay as they are. No
                    // walkthrough there: its last card is the Win+R step.
                    if (OperatingSystem.IsMacOS())
                    {
                        RenderScreens(folder, suffix + "-mac");
                    }
                    else
                    {
                        RenderScreens(folder, suffix);
                        RenderWalkthrough(folder, suffix);
                    }
                }
            }
            finally
            {
                ThemeManager.Apply(Palette.Light);
            }
        }

        static (MainWindow Window, ShellViewModel Shell) NewWindow()
        {
            var updates = UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState());
            var shell = new ShellViewModel(
                () => Task.FromResult(7),
                settingsFactory: () => NewSettings(updates),
                checkName: TestNameChecks.NoWarnings);
            var window = new MainWindow(shell);
            window.Show();
            Settle(window);
            return (window, shell);
        }

        static void RenderScreens(string folder, string suffix)
        {
            var (window, shell) = NewWindow();
            try
            {
                // The creator as it's used: a target dropped, a name typed, Enter next
                shell.Creator.KeycutCount = 7;
                shell.Creator.Target = "https://mail.google.com/mail/u/0/#inbox";
                shell.Creator.TargetKind = KeycutKind.Url;
                shell.Creator.ShortcutName = "gmail";
                Save(window, folder, "creator-" + suffix);

                // The Manager and Settings at the large size the window opens them at. The create row is empty, ready for a keycut.
                shell.ShowManager();
                shell.Manager.ShowKeycuts(SampleKeycuts());
                Save(window, folder, "manager-" + suffix);

                // Without the version row, so the picture doesn't go stale at every release
                shell.ShowSettings();
                Save(window, folder, "settings-" + suffix);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>
        /// The README's animation: the real creator taken through its real states, each with a caption under it, then a card for
        /// the step that happens outside the app (Win+R can't be shown, so the card says it in words and key caps).
        /// </summary>
        static void RenderWalkthrough(string folder, string suffix)
        {
            var (window, shell) = NewWindow();
            var creator = shell.Creator;
            creator.KeycutCount = 7;
            creator.SuccessDuration = TimeSpan.FromMinutes(1);
            var frames = new List<GifFrame>();
            try
            {
                var nameBox = MainWindowTests.Named<TextBox>(window, "TextboxShortcut");

                // "Saving…" and "Created" take a line under the name box. A GIF's frames share one size, so every frame keeps that
                // room: measured with the line showing
                creator.IsSaving = true;
                Settle(window);
                var height = window.Bounds.Height;
                creator.IsSaving = false;
                window.SizeToContent = SizeToContent.Manual;
                window.Height = height;

                void Add(string caption, int delay)
                {
                    Settle(window);
                    var view = Pixels(Render((Control)window.Content!, window.Bounds.Size));
                    var strip = Pixels(Render(CaptionStrip(caption, window.Bounds.Width), new Size(window.Bounds.Width, CaptionHeight)));
                    frames.Add(new GifFrame(view.Width, view.Height + strip.Height, view.Bgra.Concat(strip.Bgra).ToArray(), delay));
                }

                Add("Drop, paste or type what you want to open", 150);

                creator.Target = "https://mail.google.com/mail/u/0/#inbox";
                creator.TargetKind = KeycutKind.Url;
                nameBox.Focus();
                Add("keycuts works out what it is", 130);

                const string name = "gmail";
                for (var length = 1; length <= name.Length; length++)
                {
                    creator.ShortcutName = name.Substring(0, length);
                    nameBox.CaretIndex = length;
                    Add(length < name.Length ? "Give it a short name" : "Give it a short name, then press Enter", length < name.Length ? 18 : 100);
                }

                creator.SetSuccess(name);
                Add("Created", 150);

                var first = frames[0];
                var cardSize = new Size(first.Width / Scale, first.Height / Scale);
                frames.Add(new GifFrame(first.Width, first.Height, Pixels(Render(Card(cardSize), cardSize)).Bgra, 380));
            }
            finally
            {
                window.Close();
            }

            File.WriteAllBytes(Path.Combine(folder, "walkthrough-" + suffix + ".gif"), GifWriter.Write(frames));
        }

        #region Drawing

        static IBrush Resource(string key)
        {
            var app = Application.Current!;
            app.TryGetResource(key, app.ActualThemeVariant, out var value);
            return (IBrush)value!;
        }

        static FontFamily AppFont()
        {
            Application.Current!.TryGetResource("AppFontFamily", null, out var value);
            return (FontFamily)value!;
        }

        static TextBlock Words(string text, double size, string brush, FontWeight weight = FontWeight.Normal)
        {
            return new TextBlock { Text = text, FontFamily = AppFont(), FontSize = size, FontWeight = weight, Foreground = Resource(brush), VerticalAlignment = VerticalAlignment.Center };
        }

        /// <summary>A strip the width of the creator that says what is happening, in the app's own colors and font.</summary>
        static Control CaptionStrip(string caption, double width)
        {
            var text = Words(caption, 14, "TextSecondaryBrush", FontWeight.SemiBold);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            return new Border
            {
                Width = width,
                Height = CaptionHeight,
                Background = Resource("SurfaceSubtleBrush"),
                BorderBrush = Resource("BorderBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = text
            };
        }

        /// <summary>The step outside the app, in words and key caps: press Win+R, type the name, press Enter.</summary>
        static Control Card(Size size)
        {
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
                    Child = Words(label, 20, accent ? "AccentBrush" : "TextBrush", FontWeight.Bold)
                };
            }

            StackPanel Row(params Control[] items)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
                row.Children.AddRange(items);
                return row;
            }

            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var heading = Words("Then, from anywhere in Windows", 17, "TextSecondaryBrush", FontWeight.SemiBold);
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(heading);
            content.Children.Add(Row(KeyCap("Win"), Words("+", 20, "TextSecondaryBrush", FontWeight.Bold), KeyCap("R")));
            content.Children.Add(Row(Words("type", 17, "TextSecondaryBrush"), KeyCap("gmail", accent: true), Words("and press", 17, "TextSecondaryBrush"), KeyCap("Enter")));
            var result = Words("and it opens", 17, "TextSecondaryBrush", FontWeight.SemiBold);
            result.HorizontalAlignment = HorizontalAlignment.Center;
            result.Margin = new Thickness(0, 22, 0, 0);
            content.Children.Add(result);
            return new Border { Width = size.Width, Height = size.Height, Background = Resource("SurfaceBrush"), Child = content };
        }

        static void Settle(Window window)
        {
            Layout(window);

            // A still picture shouldn't show a caret caught mid-blink
            foreach (var box in window.GetVisualDescendants().OfType<TextBox>())
            {
                box.CaretBrush = Brushes.Transparent;
                // Nor a selection the screen's first focus made (the folder box selects its path)
                box.SelectionStart = box.SelectionEnd = box.CaretIndex;
            }

            foreach (var presenter in window.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>())
            {
                presenter.CaretBrush = Brushes.Transparent;
            }

            Layout(window);
        }

        static void Layout(Window window)
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }
        }

        /// <summary>The control drawn at <see cref="Scale"/> times its size, transparent where it draws nothing (behind the tear).</summary>
        static RenderTargetBitmap Render(Control control, Size size)
        {
            if (TopLevel.GetTopLevel(control) == null)
            {
                control.Measure(size);
                control.Arrange(new Rect(size));
            }

            var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Round(size.Width * Scale), (int)Math.Round(size.Height * Scale)), new Vector(96 * Scale, 96 * Scale));
            bitmap.Render(control);
            return bitmap;
        }

        static void Save(Window window, string folder, string name)
        {
            Settle(window);
            using var bitmap = Render((Control)window.Content!, window.Bounds.Size);
            if (OperatingSystem.IsMacOS())
            {
                DrawTrafficLights(bitmap);
            }

            bitmap.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
        }

        /// <summary>
        /// The picture has no window frame, and on macOS the traffic lights are the frame's: drawn where macOS puts them
        /// (docs/styleguide.md, Tabbed window), close and minimize in their colors and zoom off, so the bar reads as it does on screen.
        /// </summary>
        static void DrawTrafficLights(RenderTargetBitmap bitmap)
        {
            var zoomOff = ThemeManager.Current == Palette.Dark ? Color.Parse("#4b4e55") : Color.Parse("#d6d8dc");
            // The bitmap's DPI already scales the drawing to its pixels
            using var context = bitmap.CreateDrawingContext(false);
            foreach (var (index, color) in new[] { (0, Color.Parse("#ff5f57")), (1, Color.Parse("#febc2e")), (2, zoomOff) })
            {
                var center = new Point(MacTrafficLights.Left + 6 + index * 20, 24);
                context.DrawEllipse(new SolidColorBrush(color), new Pen(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), 0.5), center, 6, 6);
            }
        }

        sealed record PixelBuffer(int Width, int Height, byte[] Bgra);

        /// <summary>The pixels as straight-alpha BGRA (a render is premultiplied), so a half-covered edge keeps its color.</summary>
        static PixelBuffer Pixels(RenderTargetBitmap bitmap)
        {
            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                handle.Free();
                bitmap.Dispose();
            }

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                if (alpha is > 0 and < 255)
                {
                    pixels[i] = (byte)Math.Min(255, pixels[i] * 255 / alpha);
                    pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / alpha);
                    pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / alpha);
                }
            }

            return new PixelBuffer(width, height, pixels);
        }

        #endregion Drawing
    }
}
