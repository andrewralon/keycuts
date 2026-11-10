using System.Linq;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>#9: reading a dragged virtual item (Recycle Bin, This PC) from the "Shell IDList Array" format.</summary>
    public class ShellIdListTests
    {
        // A CIDA: the count, then count + 1 offsets (parent, then items), then the ID lists
        internal static byte[] Cida(byte[] parent, params byte[][] items)
        {
            var header = 4 + (items.Length + 1) * 4;
            var lists = new[] { parent }.Concat(items).ToArray();
            var offsets = new uint[lists.Length];
            var next = (uint)header;

            for (var i = 0; i < lists.Length; i++)
            {
                offsets[i] = next;
                next += (uint)lists[i].Length;
            }

            return System.BitConverter.GetBytes((uint)items.Length)
                .Concat(offsets.SelectMany(System.BitConverter.GetBytes))
                .Concat(lists.SelectMany(l => l))
                .ToArray();
        }

        internal static readonly byte[] Desktop = { 0, 0 };
        internal static readonly byte[] RecycleBin = { 6, 0, 0x1F, 0x50, 0xAA, 0xBB, 0, 0 };

        [Fact]
        public void GetFirstItem_UnderTheDesktop_IsTheItemItself()
        {
            Assert.Equal(RecycleBin, ShellIdList.GetFirstItem(Cida(Desktop, RecycleBin)));
        }

        [Fact]
        public void GetFirstItem_JoinsTheParentAndTheItem_WithOneTerminator()
        {
            var parent = new byte[] { 4, 0, 0x11, 0x22, 0, 0 };
            var item = new byte[] { 4, 0, 0x33, 0x44, 0, 0 };
            var second = new byte[] { 4, 0, 0x55, 0x66, 0, 0 };

            Assert.Equal(new byte[] { 4, 0, 0x11, 0x22, 4, 0, 0x33, 0x44, 0, 0 }, ShellIdList.GetFirstItem(Cida(parent, item, second)));
        }

        [Fact]
        public void GetFirstItem_BadData_IsNull()
        {
            var good = Cida(Desktop, RecycleBin);

            Assert.Null(ShellIdList.GetFirstItem(null));
            Assert.Null(ShellIdList.GetFirstItem(new byte[0]));
            Assert.Null(ShellIdList.GetFirstItem(Cida(Desktop)));                                    // no items
            Assert.Null(ShellIdList.GetFirstItem(good.Take(good.Length - 2).ToArray()));             // no terminator
            Assert.Null(ShellIdList.GetFirstItem(Cida(Desktop, new byte[] { 1, 0, 0, 0 })));         // an ID shorter than its own size field

            var outside = (byte[])good.Clone();
            outside[8] = 0xFF;                                                                       // the item's offset points past the end
            Assert.Null(ShellIdList.GetFirstItem(outside));

            var tooMany = (byte[])good.Clone();
            tooMany[0] = 0xFF;                                                                       // more offsets than the data holds
            Assert.Null(ShellIdList.GetFirstItem(tooMany));
        }
    }
}
