using Avalonia;
using Avalonia.Headless;
using keycuts.App.Tests;
using keycuts.Common;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace keycuts.App.Tests
{
    /// <summary>The real <see cref="App"/>, headless but drawn with Skia, with the saved theme choice faked so no test reads the registry.</summary>
    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            App.LoadThemePreference = () => ThemePreference.System;
            // Skia, not the headless stub drawing: real geometry (bounds, fills) and real rendered frames
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
    }
}
