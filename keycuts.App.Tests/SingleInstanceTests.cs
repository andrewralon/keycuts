using System;
using System.Collections.Concurrent;
using System.Threading;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>One keycuts per user on Windows (issue #307): the first start owns the name, later ones hand it their arguments. A short name of its own per test (a Unix socket path is limited to about 100 characters), never keycuts' real one.</summary>
    public class SingleInstanceTests
    {
        private static string NewName() => "kt-" + Guid.NewGuid().ToString("N").Substring(0, 12);

        [Fact]
        public void TheFirstStartOwnsTheName_AndASecondDoesNot()
        {
            var name = NewName();

            using var first = SingleInstance.TryOwn(name);
            using var second = SingleInstance.TryOwn(name);

            Assert.NotNull(first);
            Assert.Null(second);
        }

        // Windows only (issue #339): elsewhere a named pipe is a Unix socket, where a send made while the last message is still
        // handled queues on the socket that is about to close, and is lost. A Windows pipe makes the sender wait instead.
        [WindowsFact]
        public void ASecondStart_HandsItsArgumentsToTheFirst()
        {
            var name = NewName();
            using var first = SingleInstance.TryOwn(name)!;
            var received = new BlockingCollection<string[]>();
            first.Listen(args => received.Add(args));

            Assert.True(SingleInstance.Send(name, new[] { @"C:\Users\me\Desktop\a file.txt", "--startup" }));
            Assert.True(received.TryTake(out var args, 5000));
            Assert.Equal(new[] { @"C:\Users\me\Desktop\a file.txt", "--startup" }, args);

            // And the next one, so the listener went back to waiting
            Assert.True(SingleInstance.Send(name, Array.Empty<string>()));
            Assert.True(received.TryTake(out args, 5000));
            Assert.Empty(args);
        }

        [Fact]
        public void WithNobodyListening_SendGivesUp()
        {
            Assert.False(SingleInstance.Send(NewName(), new[] { "x" }, 200));
        }

        [Fact]
        public void AfterTheOwnerEnds_TheNameIsFree()
        {
            var name = NewName();
            var first = SingleInstance.TryOwn(name)!;
            first.Dispose();

            using var next = SingleInstance.TryOwn(name);
            Assert.NotNull(next);
        }
    }
}
