using System;
using System.Collections.Generic;
using System.IO;

namespace keycuts.Tests
{
    /// <summary>One frame of an animated GIF: opaque BGRA pixels, and how long it shows.</summary>
    internal sealed class GifFrame
    {
        public GifFrame(int width, int height, byte[] bgra, int delayCentiseconds)
        {
            if (bgra.Length != width * height * 4)
            {
                throw new ArgumentException("A frame needs width * height * 4 bytes of BGRA.", nameof(bgra));
            }

            Width = width;
            Height = height;
            Bgra = bgra;
            DelayCentiseconds = delayCentiseconds;
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] Bgra { get; }

        /// <summary>Browsers show anything under 2 as 10, so the writer raises it to 2.</summary>
        public int DelayCentiseconds { get; }
    }

    /// <summary>
    /// Writes an animated GIF (89a, looping forever) from frames of the same size, with one palette for all of them.
    /// It is the tool behind the README's walkthrough (<c>ScreenshotTests</c>), here so nothing is downloaded to make it.
    /// The palette is a median cut over the colors that occur (to 6 bits a channel), so an image with up to 256 such
    /// colors, which a flat UI mostly is, comes out exact.
    /// </summary>
    internal static class GifWriter
    {
        private const int ChannelBits = 6;

        private const int BinCount = 1 << (ChannelBits * 3);

        private const int MinCodeSize = 8;

        private const int ClearCode = 1 << MinCodeSize;

        private const int EndCode = ClearCode + 1;

        private const int MaxCodeBits = 12;

        private const int TableLimit = 1 << MaxCodeBits;

        public static byte[] Write(IReadOnlyList<GifFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                throw new ArgumentException("A GIF needs at least one frame.", nameof(frames));
            }

            var width = frames[0].Width;
            var height = frames[0].Height;

            foreach (var frame in frames)
            {
                if (frame.Width != width || frame.Height != height)
                {
                    throw new ArgumentException("Every frame must be the same size.", nameof(frames));
                }
            }

            BuildPalette(frames, out var palette, out var binToIndex);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' });
                writer.Write((ushort)width);
                writer.Write((ushort)height);
                writer.Write((byte)0xF7);   // a global color table of 256 colors, 8 bits a channel
                writer.Write((byte)0);      // background color index
                writer.Write((byte)0);      // pixel aspect ratio
                writer.Write(palette);

                // The Netscape extension: loop forever
                writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
                writer.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                writer.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

                foreach (var frame in frames)
                {
                    writer.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 });   // graphic control: leave the frame in place
                    writer.Write((ushort)Math.Max(2, frame.DelayCentiseconds));
                    writer.Write(new byte[] { 0x00, 0x00 });               // no transparent color; end of block

                    writer.Write((byte)0x2C);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)width);
                    writer.Write((ushort)height);
                    writer.Write((byte)0);                                 // no local color table, not interlaced

                    writer.Write((byte)MinCodeSize);
                    LzwEncode(ToIndexes(frame, binToIndex), writer);
                }

                writer.Write((byte)0x3B);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static int BinOf(byte red, byte green, byte blue)
        {
            var shift = 8 - ChannelBits;
            return ((red >> shift) << (ChannelBits * 2)) | ((green >> shift) << ChannelBits) | (blue >> shift);
        }

        private static byte[] ToIndexes(GifFrame frame, int[] binToIndex)
        {
            var indexes = new byte[frame.Width * frame.Height];

            for (var i = 0; i < indexes.Length; i++)
            {
                var offset = i * 4;
                indexes[i] = (byte)binToIndex[BinOf(frame.Bgra[offset + 2], frame.Bgra[offset + 1], frame.Bgra[offset])];
            }

            return indexes;
        }

        /// <summary>Median cut over the colors that occur, weighted by how many pixels have them.</summary>
        private static void BuildPalette(IReadOnlyList<GifFrame> frames, out byte[] palette, out int[] binToIndex)
        {
            var count = new long[BinCount];
            var sumRed = new long[BinCount];
            var sumGreen = new long[BinCount];
            var sumBlue = new long[BinCount];

            foreach (var frame in frames)
            {
                for (var offset = 0; offset < frame.Bgra.Length; offset += 4)
                {
                    var blue = frame.Bgra[offset];
                    var green = frame.Bgra[offset + 1];
                    var red = frame.Bgra[offset + 2];
                    var bin = BinOf(red, green, blue);
                    count[bin]++;
                    sumRed[bin] += red;
                    sumGreen[bin] += green;
                    sumBlue[bin] += blue;
                }
            }

            var used = new List<int>();

            for (var bin = 0; bin < BinCount; bin++)
            {
                if (count[bin] > 0)
                {
                    used.Add(bin);
                }
            }

            var boxes = new List<List<int>> { used };

            while (boxes.Count < 256)
            {
                var pick = -1;
                var pickAxis = 0;
                var pickScore = 0.0;

                for (var i = 0; i < boxes.Count; i++)
                {
                    if (boxes[i].Count < 2)
                    {
                        continue;
                    }

                    var axis = LongestAxis(boxes[i], out var range);
                    long population = 0;

                    foreach (var bin in boxes[i])
                    {
                        population += count[bin];
                    }

                    var score = population * (double)(range + 1);

                    if (score > pickScore)
                    {
                        pick = i;
                        pickAxis = axis;
                        pickScore = score;
                    }
                }

                if (pick < 0)
                {
                    break;
                }

                var box = boxes[pick];
                box.Sort((a, b) => Component(a, pickAxis).CompareTo(Component(b, pickAxis)));

                long total = 0;

                foreach (var bin in box)
                {
                    total += count[bin];
                }

                // Cut where half the pixels are behind, keeping at least one color on each side
                var cut = 1;
                long behind = count[box[0]];

                while (cut < box.Count - 1 && behind * 2 < total)
                {
                    behind += count[box[cut]];
                    cut++;
                }

                boxes[pick] = box.GetRange(0, cut);
                boxes.Add(box.GetRange(cut, box.Count - cut));
            }

            palette = new byte[256 * 3];
            binToIndex = new int[BinCount];

            for (var i = 0; i < boxes.Count; i++)
            {
                long pixels = 0;
                long red = 0;
                long green = 0;
                long blue = 0;

                foreach (var bin in boxes[i])
                {
                    pixels += count[bin];
                    red += sumRed[bin];
                    green += sumGreen[bin];
                    blue += sumBlue[bin];
                    binToIndex[bin] = i;
                }

                palette[i * 3] = (byte)((red + pixels / 2) / pixels);
                palette[i * 3 + 1] = (byte)((green + pixels / 2) / pixels);
                palette[i * 3 + 2] = (byte)((blue + pixels / 2) / pixels);
            }
        }

        // 0 red, 1 green, 2 blue
        private static int Component(int bin, int axis)
        {
            var mask = (1 << ChannelBits) - 1;
            return axis == 0 ? (bin >> (ChannelBits * 2)) & mask : axis == 1 ? (bin >> ChannelBits) & mask : bin & mask;
        }

        private static int LongestAxis(List<int> box, out int range)
        {
            var best = 0;
            range = -1;

            for (var axis = 0; axis < 3; axis++)
            {
                var low = int.MaxValue;
                var high = int.MinValue;

                foreach (var bin in box)
                {
                    var value = Component(bin, axis);
                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                }

                if (high - low > range)
                {
                    range = high - low;
                    best = axis;
                }
            }

            return best;
        }

        /// <summary>GIF's variable-width LZW: codes grow from 9 bits to 12, and the table is cleared when it fills.</summary>
        private static void LzwEncode(byte[] indexes, BinaryWriter output)
        {
            var initialBits = MinCodeSize + 1;
            var bits = initialBits;
            var maxCode = (1 << bits) - 1;
            var nextEntry = EndCode + 1;
            var table = new Dictionary<int, int>();
            var block = new byte[255];
            var blockLength = 0;
            uint accumulator = 0;
            var accumulated = 0;

            void WriteByte(byte value)
            {
                block[blockLength++] = value;

                if (blockLength == block.Length)
                {
                    output.Write((byte)blockLength);
                    output.Write(block, 0, blockLength);
                    blockLength = 0;
                }
            }

            void WriteCode(int code, bool resetAfter)
            {
                accumulator |= (uint)code << accumulated;
                accumulated += bits;

                while (accumulated >= 8)
                {
                    WriteByte((byte)(accumulator & 0xFF));
                    accumulator >>= 8;
                    accumulated -= 8;
                }

                // The decoder is one entry behind the encoder, so the width grows after the code that fills the table
                if (nextEntry > maxCode || resetAfter)
                {
                    if (resetAfter)
                    {
                        bits = initialBits;
                        maxCode = (1 << bits) - 1;
                    }
                    else
                    {
                        bits++;
                        maxCode = bits == MaxCodeBits ? TableLimit : (1 << bits) - 1;
                    }
                }
            }

            WriteCode(ClearCode, false);
            var prefix = (int)indexes[0];

            for (var i = 1; i < indexes.Length; i++)
            {
                var next = indexes[i];
                var key = (prefix << 8) | next;

                if (table.TryGetValue(key, out var code))
                {
                    prefix = code;
                    continue;
                }

                WriteCode(prefix, false);

                if (nextEntry < TableLimit)
                {
                    table[key] = nextEntry++;
                }
                else
                {
                    // Full: tell the decoder to start over, and do the same
                    WriteCode(ClearCode, true);
                    table.Clear();
                    nextEntry = EndCode + 1;
                }

                prefix = next;
            }

            WriteCode(prefix, false);
            WriteCode(EndCode, false);

            if (accumulated > 0)
            {
                WriteByte((byte)(accumulator & 0xFF));
            }

            if (blockLength > 0)
            {
                output.Write((byte)blockLength);
                output.Write(block, 0, blockLength);
            }

            output.Write((byte)0);
        }
    }
}
