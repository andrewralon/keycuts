using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using keycuts.Common;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The app icons in the Avalonia app (WPF's AppIconTests, ported): the choices and their drawings, the files beside the exe, and
    /// every drawing as a window icon and a 256 px preview. The picker's behavior is SettingsViewModelTests' and ViewTests'.
    /// </summary>
    public class AppIconTests
    {
        [Theory]
        [InlineData("BlackAndWhite", AppIconChoice.BlackAndWhite)]
        [InlineData("accent", AppIconChoice.Accent)]
        [InlineData("PORCELAIN", AppIconChoice.Porcelain)]
        [InlineData("Classic", AppIconChoice.Classic)]
        [InlineData(null, AppIconChoice.BlackAndWhite)]
        [InlineData("", AppIconChoice.BlackAndWhite)]
        [InlineData("Neon", AppIconChoice.BlackAndWhite)]
        [InlineData(" Cobalt", AppIconChoice.BlackAndWhite)]
        public void Parse_ReadsANameInAnyCase_AndAnythingElseIsTheDefault(string? value, AppIconChoice expected)
        {
            Assert.Equal(expected, AppIconChoices.Parse(value!));
        }

        // The owner's mapping (2026-11-13): Black & White is Porcelain in light mode and Onyx in dark mode; Accent is Cobalt and
        // Twilight. The rest never change.
        [Theory]
        [InlineData(AppIconChoice.BlackAndWhite, AppIconArt.Porcelain, AppIconArt.Onyx)]
        [InlineData(AppIconChoice.Accent, AppIconArt.Cobalt, AppIconArt.Twilight)]
        [InlineData(AppIconChoice.Onyx, AppIconArt.Onyx, AppIconArt.Onyx)]
        [InlineData(AppIconChoice.Classic, AppIconArt.Classic, AppIconArt.Classic)]
        public void Resolve_GivesEachChoiceItsLightAndDarkDrawing(AppIconChoice choice, AppIconArt light, AppIconArt dark)
        {
            Assert.Equal(light, AppIcons.Resolve(choice, true));
            Assert.Equal(dark, AppIcons.Resolve(choice, false));
        }

        [Theory]
        [InlineData(Palette.Light, "#000000", true)]
        [InlineData(Palette.Dark, "#FFFFFF", false)]
        [InlineData(Palette.HighContrast, "#FFFFFF", true)]
        [InlineData(Palette.HighContrast, "#000000", false)]
        [InlineData(Palette.HighContrast, "#FFFAEF", true)]
        public void IsLight_FollowsThePalette_AndTheWindowColorInHighContrast(Palette palette, string window, bool expected)
        {
            Assert.Equal(expected, AppIconManager.IsLight(palette, Color.Parse(window)));
        }

        [Theory]
        [InlineData(AppIconArt.Porcelain, AppIconArt.KeyCharcoal, "porcelain.ico")]
        [InlineData(AppIconArt.Onyx, AppIconArt.KeyWhite, "onyx.ico")]
        [InlineData(AppIconArt.Twilight, AppIconArt.KeyBlue, "twilight.ico")]
        [InlineData(AppIconArt.Classic, AppIconArt.ClassicTransparent, "classic.ico")]
        [InlineData(AppIconArt.KeyBlue, AppIconArt.KeyBlue, "key-blue.ico")]
        public void EachDrawing_HasItsOwnWithoutABackground_AndItsFileName(AppIconArt art, AppIconArt transparent, string file)
        {
            Assert.Equal(transparent, AppIcons.GetTransparent(art));
            Assert.Equal(file, AppIcons.GetFileName(art));
        }

        [Fact]
        public void Options_AreTheSevenChoicesInThePickersOrder()
        {
            Assert.Equal(Enum.GetValues<AppIconChoice>(), AppIcons.Options.Select(o => o.Choice));
            Assert.Equal(new[] { true, true, false, false, false, false, false }, AppIcons.Options.Select(o => o.FollowsTheme));
            Assert.Equal(new[] { AppIconChoice.Classic }, AppIcons.Options.Where(o => o.IsWide).Select(o => o.Choice));
        }

        [Fact]
        public void EveryDrawing_IsCopiedBesideTheExe_ForTheShortcuts()
        {
            foreach (var art in Enum.GetValues<AppIconArt>())
            {
                var path = Path.Combine(AppContext.BaseDirectory, AppIcons.FolderName, AppIcons.GetFileName(art));
                Assert.True(File.Exists(path), path + " is missing");
            }
        }

        [Fact]
        public void TheExe_UsesPorcelain_AsWpfsAndTheInstallersDo()
        {
            var csproj = XDocument.Load(RepoFiles.Find(Path.Combine("keycuts.App", "keycuts.App.csproj")));
            var icon = csproj.Descendants("ApplicationIcon").Single().Value.Replace('\\', Path.DirectorySeparatorChar);
            Assert.EndsWith(Path.Combine("assets", "icons", "porcelain", "porcelain.ico"), icon);
        }

        [AvaloniaFact]
        public void EveryDrawing_LoadsAsAWindowIcon_AndA256PixelPreview()
        {
            foreach (var art in Enum.GetValues<AppIconArt>())
            {
                Assert.IsType<WindowIcon>(AppIcons.GetWindowIcon(art));
                var preview = AppIcons.GetPreview(art);
                Assert.Equal(256, preview.PixelSize.Width);
                Assert.Same(preview, AppIcons.GetPreview(art));
            }
        }
    }
}
