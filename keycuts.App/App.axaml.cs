using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using keycuts.App.Themes;
using keycuts.Common;

namespace keycuts.App
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
                // A stand-in until the shell is ported (Phase 3.5)
                desktop.MainWindow = new Window { Title = "keycuts", Width = 600, Height = 400 };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
