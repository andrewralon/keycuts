using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// Holds the GUI's XAML to docs/styleguide.md (#219): no literal font size, no hex color outside the three palettes, margins
    /// and padding on the 4px grid, radii only from the tokens, no plain Bold, and every heading marked as one. A file that
    /// doesn't follow the guide yet is on <see cref="NotYetConverted"/>; the rest are checked, so each file is guarded from the
    /// moment it is done. The guide's own exceptions are in <see cref="Exceptions"/>.
    /// </summary>
    public class StyleGuideTests
    {
        static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        /// <summary>
        /// Files not converted yet. #218 rebuilds the Settings dialog as a screen and #219 Part 2 converts the rest, one at a time;
        /// take a file off as it is done (a test fails while a listed file already follows the guide). Part 2 empties the list.
        /// </summary>
        static readonly HashSet<string> NotYetConverted = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ConfirmWindow.xaml",
            "EditWindow.xaml",
            "SettingsWindow.xaml",
            "Views/CreatorView.xaml",
            "Views/FooterView.xaml",
            "Views/ManagerStatusView.xaml",
            "Views/ManagerView.xaml",
        };

        /// <summary>The palettes, the only files where a hex color belongs.</summary>
        static readonly HashSet<string> Palettes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Themes/Light.xaml", "Themes/Dark.xaml", "Themes/HighContrast.xaml"
        };

        /// <summary>
        /// The guide's allowed exceptions (Spacing; Corner radii), each with its reason: a value that breaks a rule on purpose, in
        /// one file, on one kind of element. Every entry must still be used (<see cref="Exceptions_AreAllStillNeeded"/>).
        /// </summary>
        static readonly Exception[] Exceptions =
        {
            // Focus rings and glows sit outside the control and follow its radius plus their offset
            new("Themes/Controls.xaml", "Rectangle", "RadiusX", "7", "focus ring: RadiusSm + 2"),
            new("Themes/Controls.xaml", "Rectangle", "RadiusY", "7", "focus ring: RadiusSm + 2"),
            new("Themes/Controls.xaml", "Border", "Margin", "-3", "text box glow, 3 outside the box"),
            new("Themes/Controls.xaml", "Border", "CornerRadius", "8", "text box glow: RadiusSm + 3"),
            new("Themes/Controls.xaml", "Rectangle", "Margin", "-4,-2", "toggle focus ring, 2 outside the track"),
            new("Themes/Controls.xaml", "Rectangle", "RadiusX", "12", "toggle focus ring: the track's 10 + 2"),
            new("Themes/Controls.xaml", "Rectangle", "RadiusY", "12", "toggle focus ring: the track's 10 + 2"),

            // Fully round shapes use half their height
            new("Themes/Controls.xaml", "Border", "CornerRadius", "9", "radio box and check badge, 18 high"),
            new("Themes/Controls.xaml", "Border", "CornerRadius", "10", "toggle track, 20 high"),

            // Slim scroll thumbs
            new("Themes/Controls.xaml", "Border", "Margin", "2", "scroll thumb's inset"),
            new("Themes/Controls.xaml", "Border", "CornerRadius", "3", "scroll thumb"),

            // Optical offsets of 1-2px inside a control template or beside an icon
            new("Themes/Controls.xaml", "Ellipse", "Margin", "2,0,0,0", "toggle thumb, 2 inside the track"),
            new("Themes/Controls.xaml", "Setter", "Margin", "0,0,2,0", "toggle thumb when on, 2 inside the track"),
            new("Themes/Controls.xaml", "Icon", "Margin", "0,2,4,0", "helper text icon lowered onto the first line"),

            // Selection borders: a card whose border goes from 1 to 2 takes 1 off its padding, so nothing moves
            new("Themes/Controls.xaml", "Setter", "Padding", "11", "selected card"),
        };

        sealed record Exception(string File, string Element, string Property, string Value, string Why);

        public sealed record Violation(string File, int Line, string Rule, string Element, string Property, string Value)
        {
            public override string ToString()
            {
                return $"{File}:{Line} <{Element}> {Property}=\"{Value}\": {Rule}";
            }
        }

        #region The rules

        static readonly string[] SpacingProperties = { "Margin", "Padding" };
        static readonly string[] RadiusProperties = { "CornerRadius", "RadiusX", "RadiusY" };
        static readonly string[] HeadingSizes = { "FontSizeHeading", "FontSizeSubheading" };
        static readonly Regex HexColor = new Regex(@"(?<![&\w])#[0-9A-Fa-f]{3,8}\b", RegexOptions.Compiled);

        /// <summary>Every rule broken in one XAML document, before exceptions.</summary>
        public static List<Violation> Check(string file, XDocument doc)
        {
            var violations = new List<Violation>();
            var isPalette = Palettes.Contains(file);

            foreach (var element in doc.Descendants())
            {
                var line = ((IXmlLineInfo)element).LineNumber;
                var name = element.Name.LocalName;

                void Add(string rule, string property, string value)
                {
                    violations.Add(new Violation(file, line, rule, name, property, value));
                }

                if (!isPalette)
                {
                    foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && HexColor.IsMatch(a.Value)))
                    {
                        Add("a color comes from a palette brush, never a hex literal", attribute.Name.LocalName, attribute.Value);
                    }

                    foreach (var text in element.Nodes().OfType<XText>().Where(t => HexColor.IsMatch(t.Value)))
                    {
                        Add("a color comes from a palette brush, never a hex literal", "(content)", text.Value.Trim());
                    }
                }

                // A Style's setters are checked as elements of their own
                foreach (var (property, value) in name == "Style" ? Enumerable.Empty<(string, string)>() : Settings(element))
                {
                    if (IsMarkupExtension(value))
                    {
                        continue;
                    }

                    if (property == "FontSize")
                    {
                        Add("a font size comes from a FontSize* token", property, value);
                    }
                    else if (property == "FontWeight" && value.Trim().Equals("Bold", StringComparison.OrdinalIgnoreCase))
                    {
                        Add("no plain Bold: SemiBold, or ExtraBold for a section label", property, value);
                    }
                    else if (SpacingProperties.Contains(property) && !IsOnGrid(value))
                    {
                        Add("margins and padding sit on the 4px grid", property, value);
                    }
                    else if (RadiusProperties.Contains(property) && !IsZero(value))
                    {
                        Add("a radius comes from the Radius* tokens", property, value);
                    }
                }

                CheckHeading(element, Add);
            }

            return violations;
        }

        // A heading must say so to assistive tech: a heading-sized TextBlock, a window-local section label, or a SectionLabel
        // whose level was taken away
        static void CheckHeading(XElement element, Action<string, string, string> add)
        {
            const string rule = "a heading has its AutomationProperties.HeadingLevel";
            if (element.Name.LocalName == "Setter")
            {
                // A setter is part of its Style, which is checked as a whole
                return;
            }

            var settings = Settings(element).ToList();
            var level = settings.Where(s => s.Property == "AutomationProperties.HeadingLevel").Select(s => s.Value).LastOrDefault();

            if (element.Name.LocalName == "SectionLabel")
            {
                if (level != null && level.Trim() == "None")
                {
                    add(rule, "AutomationProperties.HeadingLevel", level);
                }

                return;
            }

            if (level != null)
            {
                return;
            }

            var fontSize = settings.Where(s => s.Property == "FontSize").Select(s => s.Value).LastOrDefault();
            if (fontSize != null && HeadingSizes.Any(size => Regex.IsMatch(fontSize, @"Resource\s+" + size + @"\s*}")))
            {
                add(rule, "FontSize", fontSize);
            }

            var style = (string)element.Attribute("Style");
            if (style != null && Regex.IsMatch(style, @"Resource\s+SectionLabel\s*}"))
            {
                add(rule, "Style", style);
            }
        }

        /// <summary>The properties an element sets: its attributes, or a Setter's Property and Value.</summary>
        static IEnumerable<(string Property, string Value)> Settings(XElement element)
        {
            if (element.Name.LocalName == "Setter")
            {
                var property = (string)element.Attribute("Property");
                var value = (string)element.Attribute("Value");
                if (property != null && value != null)
                {
                    // "Control.Template", "TextElement.Foreground": the owner type doesn't matter here, except for attached ones
                    yield return (property.StartsWith("AutomationProperties.", StringComparison.Ordinal) ? property : property.Split('.').Last(), value);
                }

                yield break;
            }

            if (element.Name.LocalName == "Style")
            {
                // A Style's own attributes are its key and target, and its setters are checked as elements; for the heading
                // rule, a style counts as one element with all its setters
                foreach (var setter in element.Elements().Where(e => e.Name.LocalName == "Setter"))
                {
                    foreach (var setting in Settings(setter))
                    {
                        yield return setting;
                    }
                }

                yield break;
            }

            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.Namespace != Xaml))
            {
                yield return (attribute.Name.LocalName, attribute.Value);
            }
        }

        static bool IsMarkupExtension(string value)
        {
            return value.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }

        static IEnumerable<double> Numbers(string value)
        {
            return value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        static bool IsOnGrid(string value)
        {
            return Numbers(value).All(n => n % 4 == 0);
        }

        static bool IsZero(string value)
        {
            return Numbers(value).All(n => n == 0);
        }

        #endregion

        #region The app's XAML

        [Fact]
        public void ConvertedXaml_FollowsTheStyleGuide()
        {
            var broken = GuiXaml()
                .Where(file => !NotYetConverted.Contains(file.Name))
                .SelectMany(file => Check(file.Name, file.Doc))
                .Where(v => !IsException(v))
                .ToList();

            Assert.True(broken.Count == 0,
                "These break docs/styleguide.md (an intended exception goes in StyleGuideTests.Exceptions, with its reason):"
                + Environment.NewLine + string.Join(Environment.NewLine, broken));
        }

        [Fact]
        public void NotYetConverted_ListsOnlyFilesThatStillNeedIt()
        {
            var files = GuiXaml().ToDictionary(f => f.Name, f => f.Doc, StringComparer.OrdinalIgnoreCase);
            foreach (var name in NotYetConverted)
            {
                Assert.True(files.ContainsKey(name), $"{name} is on NotYetConverted but isn't a XAML file in keycuts.GUI");
                Assert.True(Check(name, files[name]).Any(v => !IsException(v)),
                    $"{name} follows the style guide now: take it off StyleGuideTests.NotYetConverted, so the test guards it");
            }
        }

        [Fact]
        public void Exceptions_AreAllStillNeeded()
        {
            var all = GuiXaml().SelectMany(file => Check(file.Name, file.Doc)).ToList();
            foreach (var exception in Exceptions)
            {
                Assert.True(all.Any(v => Matches(exception, v)),
                    $"No XAML needs the exception for {exception.Element} {exception.Property}=\"{exception.Value}\" in {exception.File} ({exception.Why}) any more: remove it");
            }
        }

        static bool IsException(Violation violation)
        {
            return Exceptions.Any(e => Matches(e, violation));
        }

        static bool Matches(Exception exception, Violation violation)
        {
            return exception.File.Equals(violation.File, StringComparison.OrdinalIgnoreCase)
                && exception.Element == violation.Element
                && exception.Property == violation.Property
                && exception.Value == violation.Value;
        }

        static IEnumerable<(string Name, XDocument Doc)> GuiXaml()
        {
            var root = GuiFolder();
            return Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(name => !name.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("obj/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => (name, XDocument.Load(Path.Combine(root, name), LoadOptions.SetLineInfo)))
                .ToList();
        }

        static string GuiFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "keycuts.GUI");
        }

        #endregion

        #region Each rule fires

        const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:controls=\"clr-namespace:keycuts.GUI.Controls\"";

        static List<Violation> CheckSnippet(string body, string file = "Views/Sample.xaml")
        {
            return Check(file, XDocument.Parse($"<Grid {Ns}>{body}</Grid>", LoadOptions.SetLineInfo));
        }

        [Theory]
        [InlineData("<TextBlock FontSize=\"11\" />", "FontSize")]
        [InlineData("<Style TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"22\" /></Style>", "FontSize")]
        [InlineData("<Border Background=\"#ffffff\" />", "Background")]
        [InlineData("<SolidColorBrush Color=\"#6B0F172A\" />", "Color")]
        [InlineData("<Color>#0F172A</Color>", "(content)")]
        [InlineData("<Border Margin=\"0,6,0,0\" />", "Margin")]
        [InlineData("<Border Padding=\"14,12\" />", "Padding")]
        [InlineData("<Style TargetType=\"Border\"><Setter Property=\"Margin\" Value=\"5\" /></Style>", "Margin")]
        [InlineData("<Border CornerRadius=\"10\" />", "CornerRadius")]
        [InlineData("<Rectangle RadiusX=\"12\" RadiusY=\"12\" />", "RadiusX")]
        [InlineData("<TextBlock FontWeight=\"Bold\" />", "FontWeight")]
        [InlineData("<Style TargetType=\"TextBlock\"><Setter Property=\"FontWeight\" Value=\"Bold\" /></Style>", "FontWeight")]
        [InlineData("<TextBlock FontSize=\"{StaticResource FontSizeHeading}\" />", "FontSize")]
        [InlineData("<TextBlock FontSize=\"{DynamicResource FontSizeSubheading}\" />", "FontSize")]
        [InlineData("<TextBlock Text=\"OUTPUT\" Style=\"{StaticResource SectionLabel}\" />", "Style")]
        [InlineData("<controls:SectionLabel Text=\"Output\" AutomationProperties.HeadingLevel=\"None\" />", "AutomationProperties.HeadingLevel")]
        public void Check_FindsEachBrokenRule(string xaml, string property)
        {
            Assert.Contains(CheckSnippet(xaml), v => v.Property == property);
        }

        [Theory]
        [InlineData("<TextBlock FontSize=\"{StaticResource FontSizeCaption}\" FontWeight=\"SemiBold\" />")]
        [InlineData("<Border Background=\"{DynamicResource SurfaceBrush}\" Margin=\"0,8,0,16\" Padding=\"{StaticResource PagePadding}\" />")]
        [InlineData("<Border Margin=\"-4,0\" CornerRadius=\"0\" />")]
        [InlineData("<Border CornerRadius=\"{StaticResource RadiusLg}\" />")]
        [InlineData("<Rectangle RadiusX=\"{StaticResource RadiusLgValue}\" />")]
        [InlineData("<TextBlock Text=\"Heading\" FontSize=\"{StaticResource FontSizeHeading}\" AutomationProperties.HeadingLevel=\"Level1\" />")]
        [InlineData("<TextBlock Text=\"Heading\" Style=\"{StaticResource Heading}\" />")]
        [InlineData("<controls:SectionLabel Text=\"Output\" />")]
        [InlineData("<controls:SectionLabel Text=\"Follows the theme\" AutomationProperties.HeadingLevel=\"Level3\" />")]
        [InlineData("<TextBlock Text=\"&#x2026; C# #1\" />")]
        [InlineData("<Style TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"{StaticResource FontSizeSubheading}\" /><Setter Property=\"AutomationProperties.HeadingLevel\" Value=\"Level2\" /></Style>")]
        public void Check_PassesWhatTheGuideAllows(string xaml)
        {
            Assert.Empty(CheckSnippet(xaml));
        }

        [Fact]
        public void Check_AllowsHexColorsOnlyInThePalettes()
        {
            const string brush = "<SolidColorBrush x:Key=\"TextBrush\" Color=\"#172033\" />";
            Assert.Empty(CheckSnippet(brush, "Themes/Light.xaml"));
            Assert.NotEmpty(CheckSnippet(brush, "Themes/Controls.xaml"));
        }

        #endregion
    }
}
