#nullable enable
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The app menu on macOS (Phase 5.2): Avalonia builds its own first, so ours goes into that one. <c>[AvaloniaFact]</c>, because a
    /// <c>NativeMenuItem</c> is an Avalonia object: made on a plain test's pool thread it can take <c>Dispatcher.UIThread</c> between two
    /// tests and fail the next one at setup (docs/testing.md).
    /// </summary>
    public class MacMenusTests
    {
        private static string[] Headers(NativeMenu menu)
        {
            return menu.Items.Select(i => i is NativeMenuItem item ? item.Header ?? "" : "-").ToArray();
        }

        [AvaloniaFact]
        public void FillAppMenu_ReplacesAvaloniasAbout_AndKeepsTheItemsMacOSAdds()
        {
            var menu = new NativeMenu();
            menu.Add(new NativeMenuItem("About Avalonia"));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(new NativeMenuItem("Hide keycuts"));
            menu.Add(new NativeMenuItem("Quit"));

            MacMenus.FillAppMenu(menu, () => { }, () => { });

            Assert.Equal(new[] { "About keycuts", "-", "Settings…", "-", "Hide keycuts", "Quit" }, Headers(menu));
        }

        [AvaloniaFact]
        public void FillAppMenu_FillsAnEmptyMenu()
        {
            var menu = new NativeMenu();

            MacMenus.FillAppMenu(menu, () => { }, () => { });

            Assert.Equal(new[] { "About keycuts", "-", "Settings…" }, Headers(menu));
        }

        [AvaloniaFact]
        public void TheItems_RunTheirActions_AndSettingsIsCommandComma()
        {
            var menu = new NativeMenu();
            var calls = "";

            MacMenus.FillAppMenu(menu, () => calls += "about;", () => calls += "settings;");

            var about = (NativeMenuItem)menu.Items[0];
            var settings = (NativeMenuItem)menu.Items[2];
            ((INativeMenuItemExporterEventsImplBridge)about).RaiseClicked();
            ((INativeMenuItemExporterEventsImplBridge)settings).RaiseClicked();
            Assert.Equal("about;settings;", calls);
            Assert.Equal(new KeyGesture(Key.OemComma, KeyModifiers.Meta), settings.Gesture);
        }
    }
}
