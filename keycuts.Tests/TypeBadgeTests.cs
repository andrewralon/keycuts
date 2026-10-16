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

        public static IEnumerable<object[]> Palettes()
        {
            yield return new object[]
            {
                "Light.xaml",
                new Dictionary<string, string[]>
                {
                    ["Url"] = new[] { "#6d43b8", "#f3edff", "#dfd2f7" },
                    ["File"] = new[] { "#1c619d", "#e9f4ff", "#cfe6fa" },
                    ["Folder"] = new[] { "#8a5700", "#fff5dc", "#f1dfae" },
                    ["Command"] = new[] { "#14734a", "#e8f7ef", "#c8ead8" }
                }
            };
            yield return new object[]
            {
                "Dark.xaml",
                new Dictionary<string, string[]>
                {
                    ["Url"] = new[] { "#c4a1ff", "#33254c", "#574279" },
                    ["File"] = new[] { "#86c8ff", "#18354e", "#295879" },
                    ["Folder"] = new[] { "#f0c365", "#41351e", "#62502a" },
                    ["Command"] = new[] { "#75dca8", "#193c2c", "#285e45" }
                }
            };
        }

        [Theory]
        [MemberData(nameof(Palettes))]
        public void Palette_UsesPrototypeBadgeColors(string file, Dictionary<string, string[]> expected)
        {
            var colors = LoadColors(file);
            foreach (var (kind, hex) in expected)
            {
                Assert.Equal(hex[0], colors[$"Badge{kind}ForegroundBrush"]);
                Assert.Equal(hex[1], colors[$"Badge{kind}BackgroundBrush"]);
                Assert.Equal(hex[2], colors[$"Badge{kind}BorderBrush"]);
            }
        }

        [Theory]
        [MemberData(nameof(Palettes))]
        public void BadgeLabels_MeetAaContrastOnTheirBackground(string file, Dictionary<string, string[]> expected)
        {
            foreach (var (kind, hex) in expected)
            {
                var ratio = ColorContrast.Ratio(hex[0], hex[1]);
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
            return XDocument.Load(ThemePath(file)).Root!.Elements()
                .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => ((string)e.Attribute("Color")!).ToLowerInvariant());
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
