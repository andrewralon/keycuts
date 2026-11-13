using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The GIF encoder behind the README's walkthrough: what it writes has to decode to what went in.</summary>
    public class GifWriterTests
    {
        /// <summary>Colors that stay distinct however the palette is cut: channel values are multiples of 4, so each has its own 6-bit bin.</summary>
        private static byte[][] DistinctColors(int count)
        {
            // A 6 x 6 x 6 grid, 11 bins apart on each channel: up to 216 colors, none sharing a bin
            return Enumerable.Range(0, count)
                .Select(i => new[]
                {
                    (byte)((i % 6) * 11 * 4),
                    (byte)((i / 6 % 6) * 11 * 4),
                    (byte)((i / 36 % 6) * 11 * 4)
                })
                .ToArray();
        }

        private static GifFrame NoiseFrame(int width, int height, byte[][] colors, int seed, int delay = 10)
        {
            var random = new Random(seed);
            var bgra = new byte[width * height * 4];

            for (var i = 0; i < width * height; i++)
            {
                var color = colors[random.Next(colors.Length)];
                bgra[i * 4] = color[2];
                bgra[i * 4 + 1] = color[1];
                bgra[i * 4 + 2] = color[0];
                bgra[i * 4 + 3] = 255;
            }

            return new GifFrame(width, height, bgra, delay);
        }

        private static List<byte[]> Decode(byte[] gif)
        {
            List<byte[]> frames = null;
            Exception failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    var decoder = BitmapDecoder.Create(new MemoryStream(gif), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    frames = decoder.Frames.Select(frame =>
                    {
                        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
                        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
                        return pixels;
                    }).ToList();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new Exception("Decoding failed: " + failure, failure);
            }

            return frames;
        }

        [Fact]
        public void NoiseOf200Colors_RoundTripsExactly_AcrossManyTableResets()
        {
            // 128 x 128 random pixels fill LZW's 4096-entry table several times, which is where code widths and clears go wrong
            var colors = DistinctColors(200);
            Assert.Equal(200, colors.Select(c => string.Join(",", c)).Distinct().Count());
            var first = NoiseFrame(128, 128, colors, 1);
            var second = NoiseFrame(128, 128, colors, 2);

            var decoded = Decode(GifWriter.Write(new[] { first, second }));

            Assert.Equal(2, decoded.Count);
            Assert.Equal(first.Bgra, decoded[0]);
            Assert.Equal(second.Bgra, decoded[1]);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 3)]
        [InlineData(7, 5)]
        [InlineData(255, 1)]
        [InlineData(256, 1)]
        public void SmallAndOddSizes_RoundTripExactly(int width, int height)
        {
            // Sizes either side of the 255-byte sub-block, and a frame of one pixel
            var frame = NoiseFrame(width, height, DistinctColors(5), width * 100 + height);

            var decoded = Decode(GifWriter.Write(new[] { frame }));

            Assert.Equal(frame.Bgra, decoded.Single());
        }

        [Fact]
        public void AFlatImage_RoundTripsExactly()
        {
            var bgra = Enumerable.Range(0, 64 * 48).SelectMany(_ => new byte[] { 0xFB, 0xF8, 0xF7, 255 }).ToArray();

            var decoded = Decode(GifWriter.Write(new[] { new GifFrame(64, 48, bgra, 20) }));

            Assert.Equal(bgra, decoded.Single());
        }

        [Fact]
        public void MoreThan256Colors_ArePutInAPaletteOf256_AndStayClose()
        {
            // A smooth gradient, 4096 colors: the palette can't hold them, so each pixel lands near its color
            var bgra = new byte[64 * 64 * 4];

            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var offset = (y * 64 + x) * 4;
                    bgra[offset] = (byte)(x * 4);
                    bgra[offset + 1] = (byte)(y * 4);
                    bgra[offset + 2] = (byte)((x + y) * 2);
                    bgra[offset + 3] = 255;
                }
            }

            var decoded = Decode(GifWriter.Write(new[] { new GifFrame(64, 64, bgra, 10) })).Single();

            var worst = 0;

            for (var i = 0; i < bgra.Length; i++)
            {
                if (i % 4 != 3)
                {
                    worst = Math.Max(worst, Math.Abs(bgra[i] - decoded[i]));
                }
            }

            Assert.InRange(worst, 0, 40);
            Assert.True(decoded.Where((_, i) => i % 4 != 3).Select((_, i) => i / 3).Distinct().Count() > 1);
        }

        [Fact]
        public void TheFileIsALoopingGif89a_WithOneControlBlockPerFrame_AndTheDelaysGiven()
        {
            var colors = DistinctColors(4);
            var gif = GifWriter.Write(new[]
            {
                NoiseFrame(8, 8, colors, 1, 150),
                NoiseFrame(8, 8, colors, 2, 25),
                NoiseFrame(8, 8, colors, 3, 1)
            });

            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
            Assert.Equal(8, BitConverter.ToUInt16(gif, 6));
            Assert.Equal(8, BitConverter.ToUInt16(gif, 8));
            Assert.Equal(0x3B, gif[gif.Length - 1]);

            // Loop forever: the Netscape application extension, with a loop count of 0
            var text = System.Text.Encoding.Latin1.GetString(gif);
            var netscape = text.IndexOf("NETSCAPE2.0", StringComparison.Ordinal);
            Assert.True(netscape > 0);
            Assert.Equal(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 }, gif.Skip(netscape + 11).Take(5).ToArray());

            // Graphic control blocks (21 F9 04 04 dd dd 00 00): one for each frame, with the delay clamped to at least 2
            var delays = new List<int>();

            for (var i = 0; i < gif.Length - 7; i++)
            {
                if (gif[i] == 0x21 && gif[i + 1] == 0xF9 && gif[i + 2] == 0x04 && gif[i + 3] == 0x04 && gif[i + 6] == 0x00 && gif[i + 7] == 0x00)
                {
                    delays.Add(BitConverter.ToUInt16(gif, i + 4));
                }
            }

            Assert.Equal(new[] { 150, 25, 2 }, delays.Take(3));
        }

        [Fact]
        public void NoFrames_FramesOfDifferentSizes_AndABadBuffer_AreRefused()
        {
            Assert.Throws<ArgumentException>(() => GifWriter.Write(Array.Empty<GifFrame>()));
            Assert.Throws<ArgumentException>(() => GifWriter.Write(new[] { NoiseFrame(4, 4, DistinctColors(2), 1), NoiseFrame(5, 4, DistinctColors(2), 1) }));
            Assert.Throws<ArgumentException>(() => new GifFrame(4, 4, new byte[10], 10));
        }
    }
}
