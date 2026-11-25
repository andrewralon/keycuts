#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Settings screen's rules (#218). The store is a fake and folders live under a temp root: nothing touches HKCU or the real output folder.</summary>
    public class SettingsViewModelTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-settings-tests-" + Guid.NewGuid().ToString("N"));

        public SettingsViewModelTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private class FakeStore : ISettingsStore
        {
            public Settings Stored { get; set; } = new Settings
            {
                OutputFolder = @"C:\Shortcuts",
                RightClickContextMenus = false,
                UseEnvironmentVariables = true,
                Theme = ThemePreference.Dark
            };

            /// <summary>Every save of the settings but the folder.</summary>
            public List<Settings> Saves { get; } = new List<Settings>();

            /// <summary>Every save of the output folder.</summary>
            public List<string> FolderSaves { get; } = new List<string>();

            /// <summary>What a settings save says.</summary>
            public string? SaveMessage { get; set; }

            /// <summary>What a folder save says; stored with no message when null.</summary>
            public FolderSaveResult? FolderResult { get; set; }

            public Settings Load()
            {
                return Stored;
            }

            public string? SaveOptions(Settings settings)
            {
                Saves.Add(settings);
                return SaveMessage;
            }

            public FolderSaveResult SaveOutputFolder(string folder)
            {
                FolderSaves.Add(folder);
                return FolderResult ?? new FolderSaveResult(true, null);
            }
        }

        private SettingsViewModel NewViewModel(FakeStore store, Func<string, string?>? pick = null, Action<string>? open = null)
        {
            return new SettingsViewModel(store, pick, open);
        }

        #region Load, and storing each change (#218)

        [Fact]
        public void Load_MapsEveryStoredValue()
        {
            var vm = NewViewModel(new FakeStore());

            Assert.Equal(@"C:\Shortcuts", vm.OutputFolder);
            Assert.False(vm.RightClickContextMenus);
            Assert.True(vm.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.Dark, vm.Theme);
            Assert.Equal(SettingsViewModel.DefaultHelperText, vm.OutputFolderMessage);
            Assert.False(vm.HasOutputFolderError);
        }

        [Fact]
        public void Opening_StoresNothing()
        {
            var store = new FakeStore();
            var themes = new List<ThemePreference>();

            _ = new SettingsViewModel(store, applyTheme: themes.Add);

            Assert.Empty(store.Saves);
            Assert.Empty(store.FolderSaves);
            Assert.Empty(themes);
        }

        [Fact]
        public void EachToggle_IsStoredTheMomentItChanges_AndNeverWritesTheFolder()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var stored = 0;
            vm.Stored += (sender, e) => stored++;

            vm.RightClickContextMenus = true;
            Assert.True(Assert.Single(store.Saves).RightClickContextMenus);

            vm.UseEnvironmentVariables = false;
            vm.CheckForUpdates = false;

            Assert.Equal(3, store.Saves.Count);
            var last = store.Saves[^1];
            Assert.True(last.RightClickContextMenus);
            Assert.False(last.UseEnvironmentVariables);
            Assert.False(last.CheckForUpdates);
            // The folder is another window's or the CLI's to change too: a toggle never writes it back (PR #225 review)
            Assert.Empty(store.FolderSaves);
            Assert.Equal(3, stored);
        }

        [Fact]
        public void SettingAToggleToTheValueItHas_StoresNothing()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);

            vm.CheckForUpdates = vm.CheckForUpdates;
            vm.Theme = vm.Theme;

            Assert.Empty(store.Saves);
        }

        [Fact]
        public void TheTheme_IsAppliedAndStoredAtOnce()
        {
            var store = new FakeStore();
            var themes = new List<ThemePreference>();
            var vm = new SettingsViewModel(store, applyTheme: themes.Add);

            vm.Theme = ThemePreference.Light;

            Assert.Equal(new[] { ThemePreference.Light }, themes);
            Assert.Equal(ThemePreference.Light, Assert.Single(store.Saves).Theme);
        }

        [Fact]
        public void EditingTheFolder_StoresNothing_UntilItIsCommitted()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var folder = Path.Combine(root, "keys");
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            vm.OutputFolder = "  \"" + folder + "\"  ";
            Assert.Empty(store.FolderSaves);
            Assert.False(Directory.Exists(folder));

            // A toggle changed meanwhile stores the toggle, and no folder at all
            vm.CheckForUpdates = false;
            Assert.Single(store.Saves);
            Assert.Empty(store.FolderSaves);

            Assert.True(vm.CommitOutputFolder());

            // The folder alone: the other settings were stored as they changed
            Assert.Equal(new[] { folder }, store.FolderSaves);
            Assert.Single(store.Saves);
            Assert.Equal(folder, vm.OutputFolder);
            Assert.True(Directory.Exists(folder));
            Assert.Equal(1, changed);
        }

        [Fact]
        public void RevertOutputFolder_DropsAnEdit_AndItsError_StoringNothing()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            Assert.False(vm.HasUncommittedFolder);
            Assert.False(vm.RevertOutputFolder());

            vm.OutputFolder = @"relative\folder";
            Assert.True(vm.HasUncommittedFolder);
            Assert.False(vm.CommitOutputFolder());
            Assert.True(vm.HasOutputFolderError);

            Assert.True(vm.RevertOutputFolder());

            Assert.Equal(@"C:\Shortcuts", vm.OutputFolder);
            Assert.False(vm.HasUncommittedFolder);
            Assert.False(vm.HasOutputFolderError);
            Assert.Empty(store.FolderSaves);

            // Nothing left to drop: Escape can go on to leave the screen
            Assert.False(vm.RevertOutputFolder());
        }

        [Fact]
        public void RevertOutputFolder_GoesBackToTheLastCommittedFolder_NotTheLoadedOne()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var folder = Path.Combine(root, "keys");
            vm.OutputFolder = folder;
            Assert.True(vm.CommitOutputFolder());
            Assert.False(vm.HasUncommittedFolder);

            vm.OutputFolder = Path.Combine(root, "typo");
            Assert.True(vm.RevertOutputFolder());

            Assert.Equal(folder, vm.OutputFolder);
            Assert.Single(store.FolderSaves);
        }

        [Fact]
        public void CommittingTheFolderItAlreadyHas_StoresNothing()
        {
            var store = new FakeStore { Stored = new Settings { OutputFolder = root } };
            var vm = NewViewModel(store);
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            Assert.True(vm.CommitOutputFolder());
            vm.OutputFolder = " " + root.ToUpperInvariant() + " ";
            Assert.True(vm.CommitOutputFolder());

            Assert.Empty(store.FolderSaves);
            Assert.Equal(0, changed);
        }

        [Fact]
        public void AStoreMessage_ShowsOnThePage_UntilAChangeStoresCleanly()
        {
            var store = new FakeStore { SaveMessage = Settings.ContextMenuFailedMessage };
            var vm = NewViewModel(store);
            var stored = 0;
            vm.Stored += (sender, e) => stored++;

            vm.RightClickContextMenus = true;

            Assert.Equal(Settings.ContextMenuFailedMessage, vm.SaveError);
            Assert.True(vm.HasSaveError);
            // The values may be stored even so, so the rest of the app still follows
            Assert.Equal(1, stored);

            store.SaveMessage = null;
            vm.CheckForUpdates = false;
            Assert.Null(vm.SaveError);
        }

        [Fact]
        public void APathFailure_StillCountsAsANewFolder()
        {
            // The folder is stored before PATH fails, so the count must follow it
            var store = new FakeStore { FolderResult = new FolderSaveResult(true, Settings.PathUpdateFailedMessage) };
            var vm = NewViewModel(store);
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            vm.OutputFolder = Path.Combine(root, "keys");
            Assert.True(vm.CommitOutputFolder());

            Assert.Equal(Settings.PathUpdateFailedMessage, vm.SaveError);
            Assert.Equal(1, changed);
        }

        // A refused registry write stores nothing: the folder stays the old one, and Enter can try again (PR #225 review)
        [Fact]
        public void ARefusedRegistryWrite_KeepsTheOldFolder_AndEnterTriesAgain()
        {
            var store = new FakeStore { FolderResult = new FolderSaveResult(false, Settings.SaveFailedPrefix + "Access is denied.") };
            var vm = NewViewModel(store);
            var folder = Path.Combine(root, "keys");
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            vm.OutputFolder = folder;
            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(Settings.SaveFailedPrefix + "Access is denied.", vm.SaveError);
            Assert.Equal(0, changed);
            Assert.Equal(folder, vm.OutputFolder);
            Assert.True(vm.HasUncommittedFolder);

            // A toggle meanwhile writes no folder, so nothing puts back an old one
            store.FolderResult = null;
            vm.CheckForUpdates = false;
            Assert.Single(store.FolderSaves);

            // The same folder again is not "unchanged": it was never stored
            Assert.True(vm.CommitOutputFolder());
            Assert.Equal(new[] { folder, folder }, store.FolderSaves);
            Assert.Equal(1, changed);
            Assert.False(vm.HasUncommittedFolder);
        }

        // The store says whether the folder was stored, apart from its message: a failure after the folder was written still
        // leaves it stored, whatever the message says (PR #225 review)
        [Fact]
        public void AFailureAfterTheFolderWasStored_StillCountsAsANewFolder()
        {
            var store = new FakeStore { FolderResult = new FolderSaveResult(true, Settings.SaveFailedPrefix + "Access is denied.") };
            var vm = NewViewModel(store);
            var folder = Path.Combine(root, "keys");
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            vm.OutputFolder = folder;
            Assert.True(vm.CommitOutputFolder());

            Assert.Equal(Settings.SaveFailedPrefix + "Access is denied.", vm.SaveError);
            Assert.Equal(1, changed);
            Assert.False(vm.HasUncommittedFolder);

            // Escape has nothing to go back to: the new folder is the stored one
            Assert.False(vm.RevertOutputFolder());
        }

        // Another window or the CLI can change the stored folder while this screen is open; a toggle here must not undo it
        [Fact]
        public void AToggle_LeavesAFolderSomeoneElseStored()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            store.Stored = new Settings { OutputFolder = @"D:\SetElsewhere" };

            vm.UseEnvironmentVariables = !vm.UseEnvironmentVariables;
            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Equal(2, store.Saves.Count);
            Assert.Empty(store.FolderSaves);
        }

        private sealed class FailingIconService : IAppIconService
        {
            public AppIconChoice Choice { get; private set; } = AppIconChoice.Classic;

            public bool Transparent { get; set; }

            public string? Apply(AppIconChoice choice)
            {
                Choice = choice;
                return AppIconService.SaveFailedMessage;
            }

            public string? ApplyTransparent(bool transparent)
            {
                Transparent = transparent;
                return AppIconService.SaveFailedMessage;
            }
        }

        // The same failure twice (the icon and its background) is said once
        [Fact]
        public void RestoreDefaults_SaysATransparentBackgroundThatCouldntBeSaved_Once()
        {
            var vm = new SettingsViewModel(new FakeStore(), appIcon: new FailingIconService { Transparent = true });

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.False(vm.TransparentIcon);
            Assert.Equal(AppIconService.SaveFailedMessage, vm.SaveError);
        }

        [Fact]
        public void TransparentIcon_ThatCouldntBeSaved_SaysSoOnThePage()
        {
            var vm = new SettingsViewModel(new FakeStore(), appIcon: new FailingIconService());

            vm.TransparentIcon = true;

            Assert.Equal(AppIconService.SaveFailedMessage, vm.SaveError);
        }

        // The picker, where an icon error shows, is closed during restore defaults, so the page says it (PR #225 review)
        [Fact]
        public void RestoreDefaults_ShowsAnIconThatCouldntBeSaved_OnThePage_AfterAnyOtherProblem()
        {
            var store = new FakeStore { SaveMessage = Settings.ContextMenuFailedMessage };
            var vm = new SettingsViewModel(store, appIcon: new FailingIconService());

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Equal(Settings.ContextMenuFailedMessage + Environment.NewLine + AppIconService.SaveFailedMessage, vm.SaveError);

            // Alone, it is the whole message
            store.SaveMessage = null;
            var alone = new SettingsViewModel(store, appIcon: new FailingIconService());
            alone.RestoreDefaultsCommand.Execute(null);
            Assert.Equal(AppIconService.SaveFailedMessage, alone.SaveError);
        }

        [Fact]
        public void ShowWelcome_AsksTheShell()
        {
            var vm = NewViewModel(new FakeStore());
            var asked = 0;
            vm.ShowWelcomeRequested += (sender, e) => asked++;

            vm.ShowWelcomeCommand.Execute(null);

            Assert.Equal(1, asked);
        }

        #endregion

        #region Restore defaults (#218)

        [Fact]
        public void RestoreDefaults_PutsEverySettingBack_ButNeverTheFolder_AndStoresOnce()
        {
            var store = new FakeStore
            {
                Stored = new Settings
                {
                    OutputFolder = @"D:\Elsewhere",
                    RightClickContextMenus = false,
                    UseEnvironmentVariables = true,
                    Theme = ThemePreference.Dark,
                    CheckForUpdates = false
                }
            };
            var icon = new InMemoryAppIconService(AppIconChoice.Classic, transparent: true);
            var themes = new List<ThemePreference>();
            var vm = new SettingsViewModel(store, appIcon: icon, applyTheme: themes.Add);
            // An edit in the box that wasn't committed stays an edit
            vm.OutputFolder = @"E:\Typing";

            vm.RestoreDefaultsCommand.Execute(null);

            var saved = Assert.Single(store.Saves);
            var defaults = Settings.Defaults();
            Assert.Empty(store.FolderSaves);
            Assert.True(saved.RightClickContextMenus);
            Assert.False(saved.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.System, saved.Theme);
            Assert.True(saved.CheckForUpdates);

            Assert.True(vm.RightClickContextMenus);
            Assert.False(vm.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.System, vm.Theme);
            Assert.True(vm.CheckForUpdates);
            Assert.Equal(@"E:\Typing", vm.OutputFolder);

            Assert.Equal(new[] { ThemePreference.System }, themes);
            Assert.Equal(Settings.DefaultAppIcon, icon.Choice);
            Assert.Equal(Settings.DefaultAppIcon, vm.SelectedIcon.Choice);
            Assert.False(icon.Transparent);
            Assert.False(vm.TransparentIcon);
        }

        [Fact]
        public void RestoreDefaults_AsksFirst_AndANoChangesNothing()
        {
            var store = new FakeStore();
            var asked = 0;
            var vm = new SettingsViewModel(store, confirmRestoreDefaults: () =>
            {
                asked++;
                return false;
            });

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Equal(1, asked);
            Assert.Empty(store.Saves);
            Assert.Equal(ThemePreference.Dark, vm.Theme);
        }

        [Fact]
        public void RestoreDefaults_NotifiesEveryValueItChanged()
        {
            var vm = NewViewModel(new FakeStore());
            var changed = new List<string?>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Contains(nameof(SettingsViewModel.RightClickContextMenus), changed);
            Assert.Contains(nameof(SettingsViewModel.UseEnvironmentVariables), changed);
            Assert.Contains(nameof(SettingsViewModel.CheckForUpdates), changed);
            Assert.Contains(nameof(SettingsViewModel.Theme), changed);
        }

        [Fact]
        public void Defaults_AreWhatANewInstallHas()
        {
            var defaults = Settings.Defaults();

            Assert.True(defaults.RightClickContextMenus);
            Assert.False(defaults.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.System, defaults.Theme);
            Assert.True(defaults.CheckForUpdates);
            Assert.Equal(AppIconChoice.BlackAndWhite, Settings.DefaultAppIcon);
            Assert.False(Settings.DefaultAppIconTransparent);
            Assert.Equal(string.Empty, defaults.OutputFolder);
        }

        [Fact]
        public void TheConfirmation_SaysTheFolderStays()
        {
            var confirm = ConfirmViewModel.ForRestoreDefaults();

            Assert.Equal("restore defaults", confirm.ConfirmText);
            Assert.Contains("output folder stays", confirm.Intro);
            Assert.False(confirm.ShowTargets);
            Assert.False(confirm.ShowWarnings);
        }

        #endregion

        #region Validation

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Commit_RejectsAnEmptyFolder(string folder)
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = folder;

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(SettingsViewModel.EmptyFolderMessage, vm.OutputFolderError);
            Assert.True(vm.HasOutputFolderError);
            Assert.Equal(SettingsViewModel.EmptyFolderMessage, vm.OutputFolderMessage);
            Assert.Empty(store.Saves);
        }

        [Theory]
        [InlineData(@"relative\folder")]
        [InlineData(@"C:folder")]
        public void Commit_RejectsAFolderThatIsNotAFullPath(string folder)
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = folder;

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(Settings.InvalidOutputFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Commit_RejectsAFolderThatCannotBeCreated()
        {
            // A file where the parent folder should be
            var blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "x");
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = Path.Combine(blocker, "keys");

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Commit_RejectsAPathThatIsAFile()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var file = Path.Combine(root, "afile");
            File.WriteAllText(file, "x");
            vm.OutputFolder = file;

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [WindowsFact]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void Commit_RejectsAnExistingFolderThatIsNotWritable()
        {
            // A temp folder that denies the current user file creation; the rule is removed before cleanup
            var folder = Directory.CreateDirectory(Path.Combine(root, "readonly"));
            var user = WindowsIdentity.GetCurrent().User!;
            var deny = new FileSystemAccessRule(user, FileSystemRights.CreateFiles | FileSystemRights.WriteData, AccessControlType.Deny);
            var security = folder.GetAccessControl();
            security.AddAccessRule(deny);
            folder.SetAccessControl(security);

            try
            {
                var store = new FakeStore();
                var vm = NewViewModel(store);
                vm.OutputFolder = folder.FullName;

                Assert.False(vm.CommitOutputFolder());

                Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
                Assert.Empty(store.Saves);
            }
            finally
            {
                security = folder.GetAccessControl();
                security.RemoveAccessRule(deny);
                folder.SetAccessControl(security);
            }
        }

        [Fact]
        public void ARefusedFolder_IsNeverStored_NotEvenByTheNextToggle()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = @"relative\folder";
            Assert.False(vm.CommitOutputFolder());

            vm.CheckForUpdates = false;

            Assert.Single(store.Saves);
            Assert.Empty(store.FolderSaves);
        }

        [Fact]
        public void CheckFolder_LeavesNoProbeFileBehind()
        {
            var folder = Path.Combine(root, "probe");

            Assert.Null(SettingsViewModel.CheckFolder(folder));

            Assert.Empty(Directory.GetFileSystemEntries(folder));
        }

        [Fact]
        public void EditingTheFolder_ClearsTheError()
        {
            var vm = NewViewModel(new FakeStore());
            vm.OutputFolder = string.Empty;
            vm.CommitOutputFolder();
            Assert.True(vm.HasOutputFolderError);

            var changed = new List<string?>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);
            vm.OutputFolder = Path.Combine(root, "fixed");

            Assert.False(vm.HasOutputFolderError);
            Assert.Equal(SettingsViewModel.DefaultHelperText, vm.OutputFolderMessage);
            Assert.Contains(nameof(SettingsViewModel.OutputFolderMessage), changed);
        }

        [Fact]
        public void TheOtherToggles_NotifyUnderTheirOwnNames()
        {
            var vm = NewViewModel(new FakeStore());
            var changed = new List<string?>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            vm.CheckForUpdates = !vm.CheckForUpdates;
            vm.RightClickContextMenus = !vm.RightClickContextMenus;

            Assert.Equal(new[] { nameof(SettingsViewModel.CheckForUpdates), nameof(SettingsViewModel.RightClickContextMenus) }, changed);
        }

        #endregion

        #region Choose and Launch

        [Fact]
        public void Launch_IsEnabledOnlyForAFolderThatExists()
        {
            var vm = NewViewModel(new FakeStore());

            vm.OutputFolder = string.Empty;
            Assert.False(vm.CanLaunch);
            Assert.False(vm.LaunchCommand.CanExecute(null));

            vm.OutputFolder = Path.Combine(root, "missing");
            Assert.False(vm.CanLaunch);

            vm.OutputFolder = root;
            Assert.True(vm.CanLaunch);
            Assert.True(vm.LaunchCommand.CanExecute(null));

            vm.OutputFolder = "   ";
            Assert.False(vm.CanLaunch);
        }

        [Fact]
        public void Launch_OpensTheFolder_AndDoesNothingWhenItIsMissing()
        {
            var opened = new List<string>();
            var vm = NewViewModel(new FakeStore(), open: opened.Add);

            vm.OutputFolder = Path.Combine(root, "missing");
            vm.LaunchCommand.Execute(null);
            Assert.Empty(opened);

            vm.OutputFolder = root;
            vm.LaunchCommand.Execute(null);
            Assert.Equal(new[] { root }, opened);
        }

        [Fact]
        public void LaunchCommand_RaisesCanExecuteChanged_WhenTheFolderTextChanges()
        {
            var vm = NewViewModel(new FakeStore());
            var raised = 0;
            vm.LaunchCommand.CanExecuteChanged += (sender, e) => raised++;

            vm.OutputFolder = root;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Choose_SeedsThePickerWithTheCurrentFolder_AndTakesItsAnswer()
        {
            string? seed = null;
            var picked = Path.Combine(root, "picked");
            var store = new FakeStore();
            var vm = NewViewModel(store, pick: current =>
            {
                seed = current;
                return picked;
            });
            vm.OutputFolder = root;

            vm.ChooseCommand.Execute(null);

            Assert.Equal(root, seed);
            Assert.Equal(picked, vm.OutputFolder);
            // A pick from the folder button is a commit: stored at once (#218)
            Assert.Equal(picked, Assert.Single(store.FolderSaves));
        }

        [Fact]
        public void Choose_KeepsTheFolder_WhenThePickerIsCancelled()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store, pick: _ => null);
            vm.OutputFolder = root;

            vm.ChooseCommand.Execute(null);

            Assert.Equal(root, vm.OutputFolder);
            Assert.Empty(store.Saves);
        }

        #endregion

        #region Run keycuts in Terminal (Phase 5.4)

        // The profile's PATH is :-separated, so these need macOS paths (a Windows C:\ would split at its colon)

        /// <summary>A shell profile under the temp root: nothing touches the real ~/.zprofile or PATH.</summary>
        private (SettingsViewModel Settings, MacShellPath Shell, string Folder) WithShell(string shell = "zsh", bool onPathAlready = false)
        {
            var home = Path.Combine(root, "home");
            Directory.CreateDirectory(home);
            var folder = Path.Combine(home, "Shortcuts");
            var path = new MacShellPath(home, shell, onPathAlready ? "/usr/bin:" + folder : "/usr/bin:/bin", Array.Empty<string>());
            var store = new FakeStore();
            store.Stored.OutputFolder = folder;
            return (new SettingsViewModel(store, shellPath: path), path, folder);
        }

        [Fact]
        public void RunInTerminal_IsOnlyOffered_WithAShellProfile()
        {
            Assert.False(NewViewModel(new FakeStore()).IsTerminalPathOffered);
            Assert.True(WithShell().Settings.IsTerminalPathOffered);
        }

        [MacFact]
        public void RunInTerminal_AddsTheBlock_SaysToOpenANewWindow_AndTakesItOutAgain()
        {
            var (settings, shell, folder) = WithShell();
            Assert.False(settings.RunInTerminal);
            Assert.True(settings.CanChangeTerminalPath);
            Assert.Equal("Adds the folder to PATH in ~/.zprofile.", settings.TerminalPathNote);

            settings.RunInTerminal = true;
            Assert.True(settings.RunInTerminal);
            Assert.Equal(folder, shell.ReadBlockFolder());
            Assert.Equal(SettingsViewModel.TerminalPathAddedNote, settings.TerminalPathNote);
            Assert.Null(settings.SaveError);

            settings.RunInTerminal = false;
            Assert.False(settings.RunInTerminal);
            Assert.Null(shell.ReadBlockFolder());
        }

        [MacFact]
        public void RunInTerminal_IsOnAndLocked_WhenSomethingElsePutTheFolderOnPath()
        {
            var (settings, shell, _) = WithShell(onPathAlready: true);

            Assert.True(settings.RunInTerminal);
            Assert.False(settings.CanChangeTerminalPath);
            Assert.Equal(SettingsViewModel.TerminalPathOnElsewhereNote, settings.TerminalPathNote);

            settings.RunInTerminal = false;
            Assert.True(settings.RunInTerminal);
        }

        [Fact]
        public void RunInTerminal_ForFish_ExplainsWhatToAdd_AndCantBeChanged()
        {
            var (settings, _, folder) = WithShell("fish");

            Assert.False(settings.CanChangeTerminalPath);
            Assert.Equal("Run: fish_add_path '" + folder + "'", settings.TerminalPathNote);
        }

        #endregion
    }
}
