using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using keycuts.Common;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;

namespace keycuts.GUI
{
    /// <summary>
    /// The app's startup (WPF's App.xaml.cs): the theme and icon, the chores on the keycuts folder, the window, and the notices and
    /// the update check once it is up. The keycuts folder is Windows-only until Phase 4: elsewhere the window opens over nothing.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>The saved theme choice, read at start. Headless tests replace it, so they never read the registry.</summary>
        public static Func<ThemePreference> LoadThemePreference { get; set; } = () => new Runner().GetThemePreference();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Before the first window, so a saved theme never flashes the wrong palette
            ThemeManager.Initialize(this, LoadThemePreference());

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                CrashReporter.Install();
                try
                {
                    ShowMainWindow(desktop);
                }
                catch (Exception exception)
                {
                    // Nothing is on screen yet, so staying alive would leave an invisible process behind
                    CrashReporter.Report(exception);
                    desktop.Shutdown(1);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow? mainWindow = null;
            var shell = CreateShell(() => mainWindow);

            mainWindow = new MainWindow(shell);
            desktop.MainWindow = mainWindow;
            MacMenus.Attach(Current!, mainWindow);
            MacMenuBar.Attach(Current!, desktop, mainWindow);

            // Explorer "keycut this!": pre-fill the target; the creator view focuses the name field on load. A bad path shows as a
            // field error, not a crash.
            if (desktop.Args is { Length: > 0 } args)
            {
                var result = TargetResolver.Resolve(args[0]);
                shell.Creator.Target = result.Target;
                shell.Creator.TargetKind = result.Kind;
                shell.Creator.TargetError = result.Error;
            }

            // Before Show, so the taskbar never shows the exe's own icon first
            AppIconManager.ApplyTo(mainWindow);
            mainWindow.Show();
            _ = LoadPathVariablesAsync(shell);
        }

        /// <summary>The window's screens and what they work on, on Windows and macOS alike; the differences are marked.</summary>
        private static ShellViewModel CreateShell(Func<Window?> mainWindow)
        {
            // After the theme, which the icon options that follow it read. Only Windows has shortcuts to repoint.
            if (OperatingSystem.IsWindows())
            {
                AppIconManager.Initialize(RegistryStuff.GetAppIcon(), RegistryStuff.GetAppIconTransparent(), UpdateShortcutIcons);
            }
            else
            {
                AppIconManager.Initialize(RegistryStuff.GetAppIcon(), RegistryStuff.GetAppIconTransparent());
            }

            // GUI creates never change PATH, so a portable copy needs the welcome to check it. The Debug folder override must never
            // write PATH, so it skips the check (null means "on PATH").
            var usesDebugFolder = DebugOverrides.GetOutputFolder() != null;
            // Off the UI thread, since the folder can be a network share that's offline. The count and the loads below wait for it,
            // so the manager and the welcome already see the new names.
            var moveAside = Task.Run(() =>
            {
                // macOS: what the installer does on Windows, done here (correction 3)
                if (OperatingSystem.IsMacOS())
                {
                    RecordMacInstall();
                }

                var renamed = MoveAsideCommandNames();
                // #196, #207: older keycuts to the current format, once per folder, with a backup (.bat files only)
                var migration = OperatingSystem.IsWindows() ? MigrateFolder() : new MigrationResult();
                AddRunCounting();
                // #248: the keycuts and keypaste commands beside the keycuts
                KeepCommandShims();
                return (Renamed: renamed, Migration: migration);
            });
            var (isOnPath, addToPath) = usesDebugFolder ? (null, null) : PathChecks();
            var welcome = new WelcomeViewModel(
                RegistryStuff.GetWelcomeDismissed,
                RegistryStuff.SetWelcomeDismissed,
                isOnPath,
                addToPath);

            // #171: the GUI only; the CLI never checks
            var updateNotice = new UpdateNoticeViewModel(UpdateChecker.CreateDefault());

            // Settings is the window's third screen (#218), made fresh each time it opens
            Func<SettingsViewModel> settingsFactory = () => new SettingsViewModel(
                new RunnerSettingsStore(),
                current => SettingsView.PickFolder(mainWindow(), current),
                SettingsView.OpenFolder,
                updates: updateNotice.Checker,
                appIcon: new AppIconService(),
                applyTheme: ThemeManager.SetPreference,
                confirmRestoreDefaults: () => ConfirmWindow.Ask(mainWindow(), ConfirmViewModel.ForRestoreDefaults()),
                // The Debug folder override never writes PATH, so it has no PATH switch either
                shellPath: OperatingSystem.IsMacOS() && !usesDebugFolder ? MacShellPath.CreateDefault() : null);

            var shell = new ShellViewModel(
                async () =>
                {
                    await moveAside;
                    return await KeycutRepository.CreateDefault().CountAsync();
                },
                async ct =>
                {
                    await moveAside.WaitAsync(ct);
                    return await KeycutRepository.CreateDefault().LoadAllAsync(ct);
                },
                (keycuts, ct) => KeycutRepository.CreateDefault().CheckTargetsAsync(keycuts, ct),
                new SystemKeycutActions(),
                welcome,
                updateNotice,
                settingsFactory);
            // A rename to a name other commands use asks first (#153), through NameCheck.Run like the Creator
            shell.Manager.CheckRename = name => NameCheck.Run(name, new Runner().GetOutputFolder(), false, NameConflictEnvironment.GetDefault());
            // #235: the Runs and Last run headers say when counting started on this machine
            shell.Manager.CountingSince = UsageLog.CreateDefault().CountingSince;
            // #238: a folder chosen in Settings counts its keycuts' runs at once, and is brought to the current format (#196, #207)
            shell.PrepareOutputFolder = async () =>
            {
                var migration = await Task.Run(() =>
                {
                    var result = OperatingSystem.IsWindows() ? MigrateFolder() : new MigrationResult();
                    AddRunCounting();
                    KeepCommandShims();
                    return result;
                });
                ShowMigrationNotice(mainWindow(), migration);
            };

            // Environment variables in paths (#31): the setting now, and this machine's variables once the window is up
            var useEnvironmentVariables = RegistryStuff.GetUseEnvironmentVariables();
            foreach (var form in shell.Forms)
            {
                form.UseEnvironmentVariablesSetting = useEnvironmentVariables;
            }

            // After the window is up
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _ = ShowStartupNoticesAsync(mainWindow, moveAside);
                _ = CheckForUpdatesAsync(updateNotice);
            });
            return shell;
        }

        private static readonly SemaphoreSlim shortcutLock = new(1, 1);

        private static string? pendingShortcutIcon;

        /// <summary>
        /// Points the app's shortcuts (Start menu, desktop, pinned taskbar) at the icon now shown, off the UI thread. Only the
        /// latest pick is written. Only shortcuts to this exe change, so a Debug run leaves the installed app's alone.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static void UpdateShortcutIcons(AppIconArt art)
        {
            var exe = Environment.ProcessPath;
            var icon = System.IO.Path.Combine(AppContext.BaseDirectory, AppIcons.FolderName, AppIcons.GetFileName(art));
            if (exe == null || !System.IO.File.Exists(icon))
            {
                return;
            }

            Interlocked.Exchange(ref pendingShortcutIcon, icon);
            _ = Task.Run(async () =>
            {
                await shortcutLock.WaitAsync();
                try
                {
                    var latest = Interlocked.Exchange(ref pendingShortcutIcon, null);
                    if (latest != null)
                    {
                        ShortcutIcons.PointAt(ShortcutIcons.GetDefaultFolders(), exe, latest);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("keycuts: couldn't update the shortcut icons: " + ex.Message);
                }
                finally
                {
                    shortcutLock.Release();
                }
            });
        }

        /// <summary>After the window is up, so startup never waits on the network. Any failure just leaves the footer line hidden.</summary>
        private static async Task CheckForUpdatesAsync(UpdateNoticeViewModel updateNotice)
        {
            try
            {
                await updateNotice.CheckOnStartupAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't check for updates: " + ex.Message);
            }
        }

        /// <summary>Says what was renamed and what was updated, once that's done, unless the window is already closed.</summary>
        private static async Task ShowStartupNoticesAsync(Func<Window?> owner, Task<(IReadOnlyList<RenamedKeycut> Renamed, MigrationResult Migration)> startup)
        {
            // Resumes on the UI thread, which the dialog needs
            var (renamed, migration) = await startup;

            if (renamed.Count > 0 && owner() is { IsVisible: true } window)
            {
                NoticeWindow.Show(window, CommandNameNotice.Format(renamed));
            }

            ShowMigrationNotice(owner(), migration);
        }

        /// <summary>The one-time notice after keycuts were updated (#196, #207): how many, and where the originals are.</summary>
        private static void ShowMigrationNotice(Window? owner, MigrationResult migration)
        {
            var text = MigrationNotice.Format(migration);

            if (text != null && owner is { IsVisible: true })
            {
                NoticeWindow.Show(owner, text);
            }
        }

        /// <summary>
        /// The welcome panel's PATH check and "add to PATH" (GUI creates never change PATH): the user PATH on Windows; on macOS the
        /// shell profile's block (Phase 4.9), where a folder already in the profile counts, since a new Terminal window finds it.
        /// </summary>
        private static (Func<string?, bool>? IsOnPath, Func<string?, bool>? AddToPath) PathChecks()
        {
            if (OperatingSystem.IsWindows())
            {
                return WindowsPathChecks();
            }

            if (OperatingSystem.IsMacOS())
            {
                var shell = MacShellPath.CreateDefault();
                return (folder => shell.Status(folder ?? new Runner().GetOutputFolder()) != ShellPathStatus.NotOnPath,
                    folder => shell.Add(folder ?? new Runner().GetOutputFolder()));
            }

            return (null, null);
        }

        [SupportedOSPlatform("windows")]
        private static (Func<string?, bool>? IsOnPath, Func<string?, bool>? AddToPath) WindowsPathChecks()
        {
            return (folder => UserPathUpdater.IsOnPath(folder ?? new Runner().GetOutputFolder()),
                folder => new UserPathUpdater().AddToOrReplace(null, folder ?? new Runner().GetOutputFolder()));
        }

        /// <summary>
        /// What the installer does on Windows, done by the app's own start on macOS (correction 3): an installed copy (the
        /// <c>keycuts</c> CLI beside it) records its folder for the shims and makes the keycuts folder. Never fails.
        /// </summary>
        private static void RecordMacInstall()
        {
            try
            {
                if (DebugOverrides.GetOutputFolder() == null &&
                    CommandShims.RecordInstallFolder(CommandShims.InstallFolderPointer, AppContext.BaseDirectory, System.IO.File.Exists))
                {
                    System.IO.Directory.CreateDirectory(new Runner().GetOutputFolder());
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't make the keycuts folder: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts keycuts.cmd and keypaste.cmd in the keycuts folder (#248; keycuts and keypaste on macOS). Only the installed copy
        /// does it, never under the Debug folder override. Runs on a worker thread. Never fails.
        /// </summary>
        private static void KeepCommandShims()
        {
            try
            {
                var runner = new Runner();
                runner.KeepCommandShims(runner.GetOutputFolder());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't put the keycuts commands in the keycuts folder: " + ex.Message);
            }
        }

        /// <summary>
        /// Brings the folder's keycuts written by older versions to the current format, with a backup, once per folder. Runs on a
        /// worker thread. Never fails.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static MigrationResult MigrateFolder()
        {
            try
            {
                var usesDebugFolder = DebugOverrides.GetOutputFolder() != null;
                return KeycutMigration.RunOnce(new Runner().GetOutputFolder(), DateTime.Now,
                    usesDebugFolder ? null : RegistryStuff.IsMigrationDone,
                    usesDebugFolder ? null : RegistryStuff.MarkMigrationDone);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't update older keycuts: " + ex.Message);
                return new MigrationResult();
            }
        }

        /// <summary>
        /// Renames keycuts named keycuts or keypaste, which clash with those commands in the keycuts folder (#209). Runs on a worker
        /// thread. Never fails.
        /// </summary>
        private static IReadOnlyList<RenamedKeycut> MoveAsideCommandNames()
        {
            try
            {
                return KeycutRepository.CreateDefault().MoveAsideCommandNames();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't check for keycuts named after commands: " + ex.Message);
                return Array.Empty<RenamedKeycut>();
            }
        }

        /// <summary>Gives the keycuts in the folder the lines that count their runs (#187), once per folder. Never fails.</summary>
        private static void AddRunCounting()
        {
            try
            {
                var usesDebugFolder = DebugOverrides.GetOutputFolder() != null;
                KeycutRepository.CreateDefault().AddRunCountingOnce(
                    usesDebugFolder ? null : RegistryStuff.IsRunCountingDone,
                    usesDebugFolder ? null : RegistryStuff.MarkRunCountingDone);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't add run counting: " + ex.Message);
            }
        }

        /// <summary>This machine's variables, read off the UI thread; the target shows its %VAR% form once they arrive.</summary>
        private static async Task LoadPathVariablesAsync(ShellViewModel shell)
        {
            try
            {
                var variables = await Task.Run(PathVariables.GetDefault);
                foreach (var form in shell.Forms)
                {
                    form.PathVariables = variables;
                }
            }
            catch (Exception ex)
            {
                // No preview; a create reads them itself
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't read environment variables: " + ex.Message);
            }
        }
    }
}
