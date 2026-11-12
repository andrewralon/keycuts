#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #180: the Edit form as a window: built like the Settings window and the confirmation, its fields named for screen
    /// readers, each error beside its field, and closed by a save.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class EditDialogTests
    {
        static Keycut NewKeycut()
        {
            return new Keycut
            {
                Name = "gmail",
                Kind = KeycutKind.Url,
                RawType = ShortcutType.Url,
                Description = "Work email",
                Target = "https://mail.google.com",
                FilePath = @"C:\fixture\gmail.bat"
            };
        }

        static EditKeycutViewModel NewForm(Func<KeycutEdit, Task<Keycut>>? save = null)
        {
            return new EditKeycutViewModel(
                NewKeycut(),
                save ?? (edit => Task.FromResult(NewKeycut() with { Name = edit.Name, Target = edit.Target, Description = edit.Description })));
        }

        static Func<KeycutEdit, Task<Keycut>> Fails(KeycutRepositoryError error, TargetError targetError = TargetError.None)
        {
            return edit => Task.FromException<Keycut>(new KeycutRepositoryException(error, "no", null, targetError));
        }

        [Fact]
        public void TheDialog_IsBuiltLikeTheSettingsWindow_AndNamesItsFields()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var settings = new SettingsWindow(new SettingsViewModel(new NoStore()));
                var window = ShowOffscreen(NewForm());
                try
                {
                    Assert.Equal(SizeToContent.Height, window.SizeToContent);
                    Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                    Assert.Equal(settings.FontFamily, window.FontFamily);
                    Assert.Equal(settings.FontSize, window.FontSize);
                    Assert.Same(settings.Background, window.Background);
                    Assert.Same(settings.Foreground, window.Foreground);
                    Assert.False(window.ShowInTaskbar);
                    Assert.Equal("Edit keycut", window.Title);

                    var save = (Button)window.FindName("BtnSave");
                    var cancel = (Button)window.FindName("BtnCancel");
                    Assert.True(save.IsDefault);
                    Assert.True(cancel.IsCancel);
                    Assert.Same(((Button)settings.FindName("BtnSave")).Style, save.Style);
                    Assert.Same(((Button)settings.FindName("BtnShowWelcome")).Style, cancel.Style);
                    Assert.Equal("Save", AutomationProperties.GetName(save));

                    var fields = Descendants<TextBox>(window).ToList();
                    Assert.Equal(new[] { "Target", "Keycut name", "Description" }, fields.Select(AutomationProperties.GetName));
                    Assert.Equal(new[] { "https://mail.google.com", "gmail", "Work email" }, fields.Select(f => f.Text));

                    var heading = Descendants<TextBlock>(window).Single(t => t.Text == "Edit keycut");
                    Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(heading));

                    // The target is the usual reason to open the form
                    Assert.Same(fields[0], FocusManager.GetFocusedElement(window));

                    // Cancel's text is its accessible name; the helper prints a nameless button as Button:<text>
                    Assert.Equal(new[] { "Target", "Keycut name", "Description", "Button:Cancel", "Save" }, TabStops(window));
                }
                finally
                {
                    window.Close();
                    settings.Close();
                }
            });
        }

        [Fact]
        public void ATargetError_ShowsBesideTheTarget_AndNowhereElse()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var form = NewForm(Fails(KeycutRepositoryError.InvalidTarget, TargetError.NotFound));
                var window = ShowOffscreen(form);
                try
                {
                    form.Target = @"C:\nowhere";
                    form.SaveAsync().GetAwaiter().GetResult();
                    Pump();

                    var target = Field(window, "Target");
                    Assert.True(((TextBlock)window.FindName("TargetErrorText")).IsVisible);
                    Assert.Equal(CreatorViewModel.GetTargetErrorMessage(TargetError.NotFound), ((TextBlock)window.FindName("TargetErrorText")).Text);
                    Assert.True(TextBoxChrome.GetHasError(target));
                    Assert.Equal(CreatorViewModel.GetTargetErrorMessage(TargetError.NotFound), AutomationProperties.GetHelpText(target));
                    Assert.False(((TextBlock)window.FindName("NameErrorText")).IsVisible);
                    Assert.False(((TextBlock)window.FindName("SaveErrorText")).IsVisible);
                    Assert.False(TextBoxChrome.GetHasError(Field(window, "Keycut name")));

                    // Typing again takes it away
                    target.Text = @"C:\somewhere";
                    Pump();
                    Assert.False(((TextBlock)window.FindName("TargetErrorText")).IsVisible);
                    Assert.False(TextBoxChrome.GetHasError(target));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void ANameError_ShowsBesideTheName_AndAGeneralOneAboveTheButtons()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var form = NewForm(Fails(KeycutRepositoryError.AlreadyExists));
                var window = ShowOffscreen(form);
                try
                {
                    form.Name = "other";
                    form.SaveAsync().GetAwaiter().GetResult();
                    Pump();

                    Assert.True(((TextBlock)window.FindName("NameErrorText")).IsVisible);
                    Assert.True(TextBoxChrome.GetHasError(Field(window, "Keycut name")));
                    Assert.False(((TextBlock)window.FindName("TargetErrorText")).IsVisible);

                    var other = NewForm(Fails(KeycutRepositoryError.NotEditable));
                    var second = ShowOffscreen(other);
                    try
                    {
                        other.Description = "x";
                        other.SaveAsync().GetAwaiter().GetResult();
                        Pump();

                        var message = (TextBlock)second.FindName("SaveErrorText");
                        Assert.True(message.IsVisible);
                        Assert.Equal(EditKeycutViewModel.NotEditableMessage, message.Text);
                        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(message));
                    }
                    finally
                    {
                        second.Close();
                    }
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void SaveIsOffWhileTheTargetOrTheNameIsBlank()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var form = NewForm();
                var window = ShowOffscreen(form);
                try
                {
                    var save = (Button)window.FindName("BtnSave");
                    Assert.True(save.IsEnabled);

                    form.Name = "  ";
                    Pump();
                    Assert.False(save.IsEnabled);
                    Assert.DoesNotContain("Save", TabStops(window));

                    form.Name = "gmail";
                    form.Target = "";
                    Pump();
                    Assert.False(save.IsEnabled);

                    form.Target = "https://example.com";
                    Pump();
                    Assert.True(save.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void WhileSaving_TheFieldsAndCancelAreOff_AndASuccessfulSaveClosesTheWindow()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var gate = new TaskCompletionSource<Keycut>();
                var form = NewForm(edit => gate.Task);
                var window = ShowOffscreen(form);
                var closed = false;
                window.Closed += (s, e) => closed = true;
                try
                {
                    form.Description = "changed";
                    var saving = form.SaveAsync();
                    Pump();

                    Assert.All(Descendants<TextBox>(window), box => Assert.False(box.IsEnabled));
                    Assert.False(((Button)window.FindName("BtnCancel")).IsEnabled);
                    Assert.False(((Button)window.FindName("BtnSave")).IsEnabled);
                    Assert.False(closed);

                    gate.SetResult(NewKeycut());
                    saving.GetAwaiter().GetResult();
                    Pump();

                    Assert.True(closed);
                    Assert.Equal("gmail", form.Saved!.Name);
                }
                finally
                {
                    if (!closed)
                    {
                        window.Close();
                    }
                }
            });
        }

        [Fact]
        public void Cancel_ClosesTheWindowWithoutSaving()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var saves = 0;
                var form = NewForm(edit => { saves++; return Task.FromResult(NewKeycut()); });
                var window = ShowOffscreen(form);
                var closed = false;
                window.Closed += (s, e) => closed = true;
                try
                {
                    form.Description = "changed";

                    form.CancelCommand.Execute(null);
                    Pump();

                    Assert.True(closed);
                    Assert.Equal(0, saves);
                    Assert.Null(form.Saved);
                }
                finally
                {
                    if (!closed)
                    {
                        window.Close();
                    }
                }
            });
        }

        [Fact]
        public void AFailedSave_PutsTheFocusOnTheFieldToFix()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var form = NewForm(Fails(KeycutRepositoryError.InvalidTarget, TargetError.NotFound));
                var window = ShowOffscreen(form);
                try
                {
                    Field(window, "Keycut name").Focus();
                    form.Description = "x";
                    form.SaveAsync().GetAwaiter().GetResult();
                    Pump();

                    Assert.Same(Field(window, "Target"), FocusManager.GetFocusedElement(window));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void TheWindowCantBeClosedWhileSaving_ButClosesOnceTheSaveIsDone()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var gate = new TaskCompletionSource<Keycut>();
                var form = NewForm(edit => gate.Task);
                var window = ShowOffscreen(form);
                var closed = false;
                window.Closed += (s, e) => closed = true;
                try
                {
                    form.Description = "changed";
                    var saving = form.SaveAsync();
                    Pump();

                    // The title-bar close button and Alt+F4 are a Close() as far as the window can tell
                    window.Close();
                    Pump();

                    Assert.False(closed);
                    Assert.True(window.IsVisible);

                    gate.SetResult(NewKeycut());
                    saving.GetAwaiter().GetResult();
                    Pump();

                    Assert.True(closed);
                    Assert.Equal("gmail", form.Saved!.Name);
                }
                finally
                {
                    gate.TrySetResult(NewKeycut());

                    if (!closed)
                    {
                        window.Close();
                    }
                }
            });
        }

        #region Helpers

        private sealed class NoStore : ISettingsStore
        {
            public Settings Load()
            {
                return new Settings { OutputFolder = @"C:\Shortcuts" };
            }

            public string? Save(Settings settings)
            {
                return null;
            }
        }

        static TextBox Field(Window window, string automationName)
        {
            return Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == automationName);
        }

        static EditWindow ShowOffscreen(EditKeycutViewModel viewModel)
        {
            var window = new EditWindow(viewModel)
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

        static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);

                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        // Tab visits controls in document order (no TabIndex is used), so the visual tree order is the tab order
        static List<string> TabStops(DependencyObject root)
        {
            return Descendants<Control>(root)
                .Where(c => c.IsTabStop && c.Focusable && c.IsEnabled && c.IsVisible && c is not ScrollViewer)
                .Select(c => AutomationProperties.GetName(c) is { Length: > 0 } name ? name : c.GetType().Name + ":" + (c is ContentControl { Content: string s } ? s : string.Empty))
                .ToList();
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
