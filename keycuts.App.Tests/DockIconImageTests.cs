using System;
using Avalonia.Headless.XUnit;
using keycuts.GUI;
using keycuts.GUI.Themes;
using SkiaSharp;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The Dock's image of an icon (issue #310): the art on Apple's grid, 824 px in a 1024 px canvas, so the tile matches the other
    /// apps'. Measured by the opaque part of the PNG.
    /// </summary>
    public class DockIconImageTests
    {
        private static (SKBitmap Image, SKRectI Opaque) Draw(AppIconArt art)
        {
            var image = SKBitmap.Decode(DockIconImage.Render(AppIcons.GetPreview(art)));
            int left = image.Width, top = image.Height, right = -1, bottom = -1;
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    if (image.GetPixel(x, y).Alpha > 128)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            return (image, new SKRectI(left, top, right + 1, bottom + 1));
        }

        [AvaloniaTheory]
        [InlineData(AppIconArt.Onyx)]
        [InlineData(AppIconArt.Porcelain)]
        [InlineData(AppIconArt.Classic)]
        public void ATile_FillsAboutEightyOnePercentOfTheCanvas_Centered(AppIconArt art)
        {
            var (image, opaque) = Draw(art);

            Assert.Equal(DockIconImage.Canvas, image.Width);
            Assert.Equal(DockIconImage.Canvas, image.Height);
            // The tile's own corners are rounded and the monogram has a margin, so only its extent can be told: never past the grid,
            // and centered
            Assert.InRange(opaque.Width, DockIconImage.Art * 0.9, DockIconImage.Art);
            Assert.InRange(opaque.Left, (DockIconImage.Canvas - DockIconImage.Art) / 2 - 1, (DockIconImage.Canvas - DockIconImage.Art) / 2 + DockIconImage.Art * 0.1);
            Assert.InRange(DockIconImage.Canvas - opaque.Right - opaque.Left, -2, 2);
            Assert.InRange(DockIconImage.Canvas - opaque.Bottom - opaque.Top, -2, 2);
        }

        [AvaloniaFact]
        public void TheMargin_IsTransparent()
        {
            var (image, _) = Draw(AppIconArt.Onyx);

            Assert.Equal(0, image.GetPixel(10, 10).Alpha);
            Assert.Equal(0, image.GetPixel(1013, 512).Alpha);
            Assert.Equal(255, image.GetPixel(512, 512).Alpha);
        }
    }
}
