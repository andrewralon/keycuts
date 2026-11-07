using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;
using keycuts.GUI.Controls;
using Xunit;

namespace keycuts.Tests
{
    public class IconTests
    {
        static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void EveryIconKind_HasAGeometryInIconsXaml()
        {
            var keys = LoadFigures().Keys;
            foreach (var kind in Enum.GetValues<IconKind>())
            {
                Assert.Contains(Icon.GetResourceKey(kind), keys);
            }
        }

        [Fact]
        public void IconsXaml_DefinesNoUnknownKeys()
        {
            var expected = Enum.GetValues<IconKind>().Select(Icon.GetResourceKey).OrderBy(k => k);
            Assert.Equal(expected, LoadFigures().Keys.OrderBy(k => k));
        }

        [Fact]
        public void EveryGeometry_ParsesAndFitsTheViewBox()
        {
            foreach (var (key, figures) in LoadFigures())
            {
                var geometry = PathGeometry.CreateFromGeometry(Geometry.Parse(figures));
                var bounds = geometry.Bounds;
                Assert.False(bounds.IsEmpty, key);
                Assert.True(bounds.Width > 1 && bounds.Height > 1, key);
                Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= Icon.ViewBoxSize && bounds.Bottom <= Icon.ViewBoxSize, $"{key} {bounds}");
            }
        }

        [Fact]
        public void OnlyMore_IsFilled()
        {
            Assert.Equal(new[] { IconKind.More }, Enum.GetValues<IconKind>().Where(Icon.IsFilled));
        }

        [Fact]
        public void GetResourceKey_RejectsUndefinedKinds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Icon.GetResourceKey((IconKind)999));
        }

        static System.Collections.Generic.Dictionary<string, string> LoadFigures()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            var path = Path.Combine(dir!.FullName, "keycuts.GUI", "Themes", "Icons.xaml");
            return XDocument.Load(path).Root!.Elements()
                .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => (string)e.Attribute("Figures")!);
        }
    }
}
