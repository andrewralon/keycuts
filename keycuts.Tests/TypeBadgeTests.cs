using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    public class TypeBadgeTests
    {
        static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Theory]
        [InlineData(KeycutKind.Url, "URL", IconKind.Link, "Type: URL")]
        [InlineData(KeycutKind.Folder, "Folder", IconKind.Folder, "Type: Folder")]
        [InlineData(KeycutKind.File, "File", IconKind.File, "Type: File")]
        [InlineData(KeycutKind.Command, "Command", IconKind.Play, "Type: Command")]
        public void EachKind_HasLabelIconAndAutomationName(KeycutKind kind, string label, IconKind icon, string automationName)
        {
            Assert.Equal(label, TypeBadge.GetLabel(kind));
            Assert.Equal(icon, TypeBadge.GetIconKind(kind));
            Assert.Equal(automationName, TypeBadge.GetAutomationName(kind));
        }

        [Fact]
        public void EveryKind_HasADistinctIconAndLabel()
        {
            var kinds = Enum.GetValues<KeycutKind>();
            Assert.Equal(kinds.Length, kinds.Select(TypeBadge.GetLabel).Distinct().Count());
            Assert.Equal(kinds.Length, kinds.Select(TypeBadge.GetIconKind).Distinct().Count());
        }

        [Fact]
        public void UndefinedKind_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TypeBadge.GetLabel((KeycutKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TypeBadge.GetIconKind((KeycutKind)99));
        }

        // Read from the palettes themselves, so a changed color is checked rather than a copy of it
        [Theory]
        [InlineData("Light.xaml")]
        [InlineData("Dark.xaml")]
        public void BadgeLabels_MeetAaContrastOnTheirBackground(string file)
        {
            var colors = LoadColors(file);
            foreach (var kind in new[] { "Url", "File", "Folder", "Command" })
            {
                var ratio = ColorContrast.Ratio(colors[$"Badge{kind}ForegroundBrush"], colors[$"Badge{kind}BackgroundBrush"]);
                Assert.True(ratio >= 4.5, $"{file}: {kind} badge is {ratio:0.00}:1");
            }
        }

        [Fact]
        public void HighContrastPalette_DefinesEveryBadgeBrushWithSystemColors()
        {
            var root = XDocument.Load(ThemePath("HighContrast.xaml")).Root!;
            var badges = root.Elements().Where(e => ((string)e.Attribute(Xaml + "Key")!).StartsWith("Badge")).ToList();
            Assert.Equal(12, badges.Count);
            Assert.All(badges, e => Assert.Contains("SystemColors", (string)e.Attribute("Color")!));
        }

        static Dictionary<string, string> LoadColors(string file)
        {
            // A brush has a Color attribute; a Color (ShadowColor) is the element's own text
            return XDocument.Load(ThemePath(file)).Root!.Elements()
                .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => ((string)e.Attribute("Color") ?? e.Value.Trim()).ToLowerInvariant());
        }

        static string ThemePath(string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "keycuts.GUI", "Themes", file);
        }
    }
}
