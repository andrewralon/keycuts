using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI
{
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
            ThemeManager.Initialize(this, LoadThemePreference());

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow(CreateShell());

                // Explorer "keycut this!": pre-fill the target; the creator view focuses the name field on load
                if (desktop.Args is { Length: > 0 } args)
                {
                    var shell = ((MainWindow)desktop.MainWindow).Shell;
                    var result = TargetResolver.Resolve(args[0]);
                    shell.Creator.Target = result.Target;
                    shell.Creator.TargetKind = result.Kind;
                    shell.Creator.TargetError = result.Error;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>The screens over the keycuts folder. Startup's chores (renames, migration, shims) come with Settings.</summary>
        private static ViewModels.ShellViewModel CreateShell()
        {
            if (!OperatingSystem.IsWindows())
            {
                // The keycuts folder is Windows-only until Phase 4
                return new ViewModels.ShellViewModel(() => System.Threading.Tasks.Task.FromResult(0));
            }

            return new ViewModels.ShellViewModel(
                () => KeycutRepository.CreateDefault().CountAsync(),
                ct => KeycutRepository.CreateDefault().LoadAllAsync(ct),
                (keycuts, ct) => KeycutRepository.CreateDefault().CheckTargetsAsync(keycuts, ct),
                new ViewModels.SystemKeycutActions());
        }
    }
}
