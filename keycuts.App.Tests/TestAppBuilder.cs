using Avalonia;
using Avalonia.Headless;
using keycuts.App.Tests;
using keycuts.Common;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace keycuts.App.Tests
{
    /// <summary>The real <see cref="App"/>, headless, with the saved theme choice faked so no test reads the registry.</summary>
    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            App.LoadThemePreference = () => ThemePreference.System;
            return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
        }
    }
}
