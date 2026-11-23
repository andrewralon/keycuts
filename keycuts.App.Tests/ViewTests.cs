using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Controls;
using keycuts.GUI.Mvvm;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The Creator, the two dialogs and the Settings screen, headless (WPF's AccessibilityTests' Creator half, ConfirmDialogTests,
    /// EditDialogTests and SettingsScreenTests, ported). The stores are fakes in memory: nothing reads HKCU or a real folder.
    /// </summary>
    public class ViewTests : IDisposable
    {
        readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-view-tests-" + Guid.NewGuid().ToString("N"));

        public ViewTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        #region Helpers

        static Window Host(Control view, object viewModel, double width = 600, double height = 520)
        {
            ThemeManager.Apply(Palette.Light);
            view.DataContext = viewModel;
            var window = new Window { Width = width, Height = height, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }

        static T Named<T>(Visual root, string name) where T : Control
        {
            return MainWindowTests.Named<T>(root, name);
        }

        static IInputElement? Focused(Window window)
        {
            return window.FocusManager!.GetFocusedElement();
        }

        static CreatorViewModel Creator(Func<string, string, string, bool, bool, System.Threading.CancellationToken, Task<CreateResult>>? create = null)
        {
            var noop = new RelayCommand(() => { });
            return new CreatorViewModel(noop, noop, create, name => NameCheck.None(name));
        }

        /// <summary>Runs the dispatcher's jobs until the condition holds (a continuation of work on the thread pool), for at most 5 seconds.</summary>
        static void PumpUntil(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition() && DateTime.UtcNow < until)
            {
                Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(5);
            }
        }

        static void Click(Window window, Control control)
        {
            var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        #endregion

        #region The Creator

        [AvaloniaFact]
        public void Creator_TheLabelsSitLeftOfTheirBoxes_AndTheDescriptionAlwaysShows()
        {
            var window = Host(new CreatorView(), Creator());
            var name = Named<TextBox>(window, "TextboxShortcut");
            var description = Named<TextBox>(window, "TextboxDescription");
            Assert.True(description.IsEffectivelyVisible);
            Assert.Equal("keycut name", AutomationProperties.GetName(name));
            Assert.Equal("Description", AutomationProperties.GetName(description));
            Assert.Equal(name.TranslatePoint(default, window)!.Value.X, description.TranslatePoint(default, window)!.Value.X, 0.5);
            window.Close();
        }

        [AvaloniaFact]
        public void Creator_LaunchFocus_GoesToTheTarget_OrToTheNameOnceATargetIsSet()
        {
            var window = Host(new CreatorView(), Creator());
            Assert.Same(Named<TextBox>(window, "TextboxTarget"), Focused(window));
            window.Close();

            var withTarget = Creator();
            withTarget.Target = @"C:\Windows";
            window = Host(new CreatorView(), withTarget);
            Assert.Same(Named<TextBox>(window, "TextboxShortcut"), Focused(window));
            window.Close();
        }

        [AvaloniaFact]
        public void Creator_TheHelperLines_StayInTheTree_TakeNoRoomWhileEmpty_AndAreLive()
        {
            var creator = Creator();
            var window = Host(new CreatorView(), creator);
            var helper = Named<HelperText>(window, "HelperTextBlock");
            var status = Named<HelperText>(window, "NameStatusTextBlock");
            Assert.True(helper.IsVisible);
            Assert.True(status.IsVisible);
            Assert.Equal(0, helper.Bounds.Height, 0.5);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));

            creator.TargetError = TargetError.NotFound;
            creator.ApplyResolved(TargetResult.Invalid(TargetError.NotFound, @"C:\missing"), showError: true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(helper.Bounds.Height > 0);
            Assert.Equal(HelperTextState.Error, helper.State);
            Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(helper));
            window.Close();
        }

        [AvaloniaFact]
        public void Creator_EnterInTheName_Creates_AndEnterInTheTarget_OnlyMovesOn()
        {
            var created = new List<string>();
            var creator = Creator((target, name, description, flip, replace, ct) =>
            {
                created.Add(name);
                return Task.FromResult(CreateResult.Success(new Keycut { Name = name, Target = target, FilePath = Path.Combine(Path.GetTempPath(), name + ".bat") }));
            });
            // A URL resolves the same on every platform (macOS paths are classified from Phase 4)
            creator.Target = "https://example.com/";
            creator.TargetKind = KeycutKind.Url;
            var window = Host(new CreatorView(), creator);

            var target = Named<TextBox>(window, "TextboxTarget");
            target.Focus();
            MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
            Assert.Empty(created);

            Named<TextBox>(window, "TextboxShortcut").Focus();
            window.KeyTextInput("tmp");
            Dispatcher.UIThread.RunJobs();
            MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
            // The name check runs on the thread pool, so the create follows a moment later: pump until it has happened
            PumpUntil(() => created.Count > 0);
            Assert.Equal(new[] { "tmp" }, created);
            window.Close();
        }

        [AvaloniaFact]
        public void Creator_WelcomePanel_ShowsWithAWelcome_AndHidesOnDismiss()
        {
            var dismissed = false;
            var noop = new RelayCommand(() => { });
            var creator = new CreatorViewModel(noop, noop) { Welcome = new WelcomeViewModel(() => dismissed, () => dismissed = true) };
            creator.Welcome!.Show();
            var window = Host(new CreatorView(), creator);
            var panel = Named<Border>(window, "WelcomePanel");
            Assert.True(panel.IsVisible);
            Assert.Equal(3, window.GetVisualDescendants().OfType<RadioButton>().Count());

            var gotIt = panel.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "got it");
            gotIt.Command!.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(panel.IsVisible);
            Assert.True(dismissed);
            window.Close();
        }

        [AvaloniaFact]
        public void Creator_WithoutAnUpdateNotice_ShowsNoFooter()
        {
            var window = Host(new CreatorView(), Creator());
            Assert.False(window.GetVisualDescendants().OfType<FooterView>().Single().GetVisualDescendants().OfType<Border>().First().IsVisible);
            window.Close();
        }

        #endregion

        #region The dialogs

        static NameCheck Replace(string name)
        {
            var existing = new Keycut { Name = name, Target = @"C:\old.txt", FilePath = @"C:\fixture\" + name + ".bat" };
            return new NameCheck(name, existing, Array.Empty<NameConflict>());
        }

        [AvaloniaFact]
        public void TheConfirmDialog_ForAReplace_ShowsBothTargets_AndAnswersWithItsButtons()
        {
            foreach (var (button, expected) in new[] { ("BtnConfirm", true), ("BtnCancel", false) })
            {
                ThemeManager.Apply(Palette.Light);
                var dialog = new ConfirmWindow(ConfirmViewModel.ForCreate(Replace("gmail"), @"C:\new.txt"));
                var owner = new Window();
                owner.Show();
                var answer = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("Replace “gmail”?", Named<TextBlock>(dialog, "HeadingText").Text);
                Assert.Equal(@"C:\old.txt", Named<TextBlock>(dialog, "CurrentTargetText").Text);
                Assert.Equal(@"C:\new.txt", Named<TextBlock>(dialog, "NewTargetText").Text);
                Assert.Same(Named<Button>(dialog, "BtnConfirm"), Focused(dialog));
                Assert.True(Named<Button>(dialog, "BtnConfirm").IsDefault);
                Assert.True(Named<Button>(dialog, "BtnCancel").IsCancel);

                Click(dialog, Named<Button>(dialog, button));
                Assert.True(answer.IsCompleted);
                Assert.Equal(expected, answer.Result);
                owner.Close();
            }
        }

        [AvaloniaFact]
        public void Modal_GivesTheDialogsAnswer_ToTheViewModelsAsking()
        {
            Modal.Answer = dialog => dialog is ConfirmWindow;
            try
            {
                Assert.True(ConfirmWindow.Ask(null, ConfirmViewModel.ForRestoreDefaults()));
            }
            finally
            {
                Modal.Answer = null;
            }
        }

        static EditKeycutViewModel EditForm(Func<KeycutEdit, Task<Keycut>> save)
        {
            var keycut = new Keycut { Name = "gmail", Kind = KeycutKind.Url, RawType = ShortcutType.Url, Target = "https://mail.google.com", FilePath = @"C:\fixture\gmail.bat" };
            return new EditKeycutViewModel(keycut, save, name => NameCheck.None(name));
        }

        [AvaloniaFact]
        public void TheEditForm_NamesItsFields_StartsInTheTarget_AndSaveIsOffWhileTheNameIsBlank()
        {
            ThemeManager.Apply(Palette.Light);
            var form = EditForm(edit => Task.FromResult(new Keycut { Name = edit.Name, Target = edit.Target }));
            var dialog = new EditWindow(form);
            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Target", AutomationProperties.GetName(Named<TextBox>(dialog, "TextboxTarget")));
            Assert.Equal("keycut name", AutomationProperties.GetName(Named<TextBox>(dialog, "TextboxName")));
            Assert.Same(Named<TextBox>(dialog, "TextboxTarget"), Focused(dialog));

            // The error lines are live lines: in the tree, taking no room while there is nothing to say
            var nameError = Named<TextBlock>(dialog, "NameErrorText");
            Assert.True(nameError.IsVisible);
            Assert.Equal(0, nameError.Bounds.Height, 0.5);

            form.Name = "  ";
            Dispatcher.UIThread.RunJobs();
            Assert.False(form.SaveCommand.CanExecute(null));
            dialog.Close();
        }

        [AvaloniaFact]
        public void TheEditForm_CantBeClosedWhileSaving_AndClosesWhenTheSaveIsDone()
        {
            ThemeManager.Apply(Palette.Light);
            var saving = new TaskCompletionSource<Keycut>();
            var form = EditForm(edit => saving.Task);
            var owner = new Window();
            owner.Show();
            var dialog = new EditWindow(form);
            var result = dialog.ShowDialog<bool>(owner);
            Dispatcher.UIThread.RunJobs();

            form.Target = "https://mail.google.com/mail";
            form.SaveCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(form.IsSaving);
            dialog.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(dialog.IsVisible);
            Assert.False(Named<Button>(dialog, "BtnCancel").IsEnabled);

            saving.SetResult(new Keycut { Name = "gmail", Target = "https://mail.google.com/mail", FilePath = @"C:\fixture\gmail.bat" });
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.IsCompleted);
            Assert.True(result.Result);
            owner.Close();
        }

        #endregion

        #region The Settings screen

        sealed class FakeStore : ISettingsStore
        {
            public Settings Stored { get; set; } = new Settings { OutputFolder = Path.GetTempPath(), Theme = ThemePreference.Dark };

            public List<Settings> Saves { get; } = new();

            public List<string> FolderSaves { get; } = new();

            public Settings Load() => Stored;

            public string? SaveOptions(Settings settings)
            {
                Saves.Add(settings);
                return null;
            }

            public FolderSaveResult SaveOutputFolder(string folder)
            {
                FolderSaves.Add(folder);
                return new FolderSaveResult(true, null);
            }
        }

        sealed class StillLoginItem : ILoginItem
        {
            public bool IsOn => false;

            public bool NeedsApproval => false;

            public string? Set(bool on) => null;
        }

        sealed class StillShortcut : IRunBoxShortcut
        {
            public RunBoxHotkey? Current => RunBoxHotkey.Default;

            public int Status => 0;

            public int Apply(RunBoxHotkey? hotkey) => 0;

            public void Pause(bool paused)
            {
            }
        }

        // Mockup macos-settings-v3: one Behavior section on the left (the output folder, Run Box, Open at login, Ask before replacing,
        // the Quick Action, $HOME, Terminal last); Appearance and Updates on the right; no Output heading
        [AvaloniaFact]
        public void Settings_OnTheMac_FollowsTheV3Mockup()
        {
            Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS rows");
            var settings = new SettingsViewModel(new FakeStore())
            {
                LoginItem = new StillLoginItem(),
                RunBoxShortcut = new StillShortcut()
            };
            var window = Host(new SettingsView(), settings, 768, 470);

            var labels = window.GetVisualDescendants().OfType<SectionLabel>().Select(l => l.Text).ToList();
            Assert.DoesNotContain("Output", labels);
            Assert.DoesNotContain("Run box", labels);
            Assert.Equal(new[] { "Behavior", "Appearance", "Updates" }, labels.Where(l => l is "Behavior" or "Appearance" or "Updates").ToArray());

            var left = Named<StackPanel>(window, "LeftColumn");
            var order = left.Children.Select(c => c.Name).Where(n => n != null).ToList();
            Assert.Equal(new[] { "RunBoxSection", "OpenAtLoginRow", "AskBeforeReplacingRow", "TerminalPathRow" }, order.Where(n => n is "RunBoxSection" or "OpenAtLoginRow" or "AskBeforeReplacingRow" or "TerminalPathRow").ToArray());
            Assert.Equal("TerminalPathRow", order.Last());
            Assert.Equal("Run Box", window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Run Box").Text);

            // No sentence under the folder, none under Torn corner, and the empty line takes no room: Run Box sits one macOS row gap (12) under the box
            Assert.Equal(string.Empty, settings.OutputFolderMessage);
            var message = Named<HelperText>(window, "OutputFolderMessage");
            Assert.Equal(0, message.Bounds.Height, 0.5);
            Assert.Equal(new Thickness(0), message.Margin);
            var box = Named<TextBox>(window, "TextboxOutputFolder");
            var runBox = Named<StackPanel>(window, "RunBoxSection");
            var gap = ((Control)runBox.Children[0]).TranslatePoint(new Point(0, 0), window)!.Value.Y - box.TranslatePoint(new Point(0, box.Bounds.Height), window)!.Value.Y;
            Assert.InRange(gap, 11, 13);
            Assert.Equal("keycuts waits in the menu bar.", SettingsViewModel.OpenAtLoginNote);
            window.Close();
        }

        [AvaloniaFact]
        public void Settings_StoresTheFolderOnEnter_AndEscapeDropsAnEdit()
        {
            var store = new FakeStore();
            var settings = new SettingsViewModel(store);
            var window = Host(new SettingsView(), settings, 768, 470);
            var box = Named<TextBox>(window, "TextboxOutputFolder");
            Assert.Same(box, Focused(window));

            settings.OutputFolder = root;
            MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);
            Assert.Equal(new[] { root }, store.FolderSaves);

            settings.OutputFolder = Path.Combine(root, "elsewhere");
            MainWindowTests.Press(window, Key.Escape, RawInputModifiers.None);
            Assert.Equal(root, settings.OutputFolder);
            Assert.Single(store.FolderSaves);
            window.Close();
        }

        [AvaloniaFact]
        public void Settings_AnInvalidFolder_ShowsItsErrorUnderTheBox_AndStoresNothing()
        {
            var store = new FakeStore();
            var settings = new SettingsViewModel(store);
            var window = Host(new SettingsView(), settings, 768, 470);

            settings.OutputFolder = "not a full path";
            MainWindowTests.Press(window, Key.Enter, RawInputModifiers.None);

            Assert.Empty(store.FolderSaves);
            Assert.Equal(HelperTextState.Error, Named<HelperText>(window, "OutputFolderMessage").State);
            Assert.Same(Named<TextBox>(window, "TextboxOutputFolder"), Focused(window));
            window.Close();
        }

        [AvaloniaFact]
        public void Settings_Opening_NeitherAppliesNorStoresATheme_AndAClickDoes()
        {
            var store = new FakeStore();
            var applied = new List<ThemePreference>();
            var settings = new SettingsViewModel(store, applyTheme: applied.Add);
            var window = Host(new SettingsView(), settings, 768, 470);
            var choices = Named<StackPanel>(window, "ThemeChoices").Children.OfType<RadioButton>().ToList();
            Assert.True(choices.Single(c => (ThemePreference)c.Tag! == ThemePreference.Dark).IsChecked);
            Assert.Empty(store.Saves);
            Assert.Empty(applied);

            Click(window, choices.Single(c => (ThemePreference)c.Tag! == ThemePreference.Light));
            Assert.Equal(new[] { ThemePreference.Light }, applied);
            Assert.Equal(ThemePreference.Light, store.Saves.Last().Theme);
            window.Close();
        }

        [AvaloniaFact]
        public void Settings_TheTogglesAreNamed_AndLaunchIsOffForAFolderThatDoesNotExist()
        {
            var store = new FakeStore { Stored = new Settings { OutputFolder = Path.Combine(root, "missing") } };
            var window = Host(new SettingsView(), new SettingsViewModel(store), 768, 470);
            foreach (var toggle in window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(t => t is not RadioButton))
            {
                Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(toggle)));
            }

            Assert.False(Named<Button>(window, "BtnLaunch").IsEffectivelyEnabled);
            window.Close();
        }

        [AvaloniaFact]
        public void Settings_TheIconPicker_ListsTheSevenCards_InTwoGroups_AndEscapeAndTheScrimCloseIt()
        {
            var settings = new SettingsViewModel(new FakeStore());
            var window = Host(new SettingsView(), settings, 768, 600);
            settings.IsIconPickerOpen = true;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            var lists = Named<ItemsControl>(window, "IconGroups").GetVisualDescendants().OfType<ListBox>().ToList();
            Assert.Equal(2, lists.Count);
            Assert.Equal(7, lists.Sum(l => l.ItemCount));
            Assert.Equal(new[] { AppIcons.FollowsThemeGroup, AppIcons.AlwaysTheSameGroup },
                Named<ItemsControl>(window, "IconGroups").GetVisualDescendants().OfType<SectionLabel>().Select(l => l.Text));

            // A pick in either list is the one choice, and the other list lets go of its own
            lists[1].SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AppIcons.Options[2], settings.SelectedIcon);
            Assert.Null(lists[0].SelectedItem);
            Assert.True(settings.IsIconPickerOpen);

            // The dimmed page, away from the sheet in the middle
            window.MouseDown(new Point(4, 4), MouseButton.Left);
            window.MouseUp(new Point(4, 4), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(settings.IsIconPickerOpen);
            window.Close();
        }

        #endregion
    }
}
