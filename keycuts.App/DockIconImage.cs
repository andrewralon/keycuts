using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace keycuts.GUI
{
    /// <summary>
    /// The Dock's image of an app icon (issue #310): Apple's macOS icon grid, a 1024 px canvas with the art at 824 px, centered, so
    /// the tile is the same size as every other app's. The Dock draws a runtime icon exactly as given, so the whole canvas (the tile,
    /// or the keycap alone with its own margin) is scaled to 824 on a transparent 1024 px canvas: a uniform scale that leaves the
    /// keycap-to-tile ratio alone. Pure drawing, so it is tested everywhere; only macOS shows it.
    /// </summary>
    public static class DockIconImage
    {
        public const int Canvas = 1024;

        public const int Art = 824;

        /// <summary>The picture as a PNG on the grid.</summary>
        public static byte[] Render(Bitmap art)
        {
            var margin = (Canvas - Art) / 2.0;
            using var target = new RenderTargetBitmap(new PixelSize(Canvas, Canvas), new Vector(96, 96));

            using (var context = target.CreateDrawingContext(true))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(art, new Rect(art.Size), new Rect(margin, margin, Art, Art));
            }

            using var stream = new MemoryStream();
            target.Save(stream, new PngBitmapEncoderOptions());
            return stream.ToArray();
        }
    }
}
