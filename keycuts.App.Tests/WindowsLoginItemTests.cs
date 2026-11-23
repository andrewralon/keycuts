#nullable enable
using System;
using keycuts.GUI;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Windows' Open at login (issue #307): the Run key's value with the startup argument, and Startup apps' switch.</summary>
    public class WindowsLoginItemTests
    {
        private const string Exe = @"C:\Users\me\AppData\Local\Programs\keycuts\keycuts.GUI.exe";

        private sealed class FakeKeys : IStartupKeys
        {
            public string? Command { get; set; }

            public byte? Approval { get; set; }

            public Exception? Fails { get; set; }

            public string? GetCommand() => Command;

            public void SetCommand(string? command)
            {
                if (Fails != null)
                {
                    throw Fails;
                }

                Command = command;
            }

            public byte? GetApproval() => Approval;

            public void ClearApproval() => Approval = null;
        }

        [Fact]
        public void TheCommand_IsTheExeInQuotes_ThenTheStartupArgument()
        {
            Assert.Equal("\"" + Exe + "\" --startup", WindowsLoginItem.Command(Exe));
        }

        [Fact]
        public void TurningItOnAndOff_WritesAndRemovesTheRunValue()
        {
            var keys = new FakeKeys();
            var item = new WindowsLoginItem(keys, Exe);
            Assert.False(item.IsOn);

            Assert.Null(item.Set(true));
            Assert.True(item.IsOn);
            Assert.Equal(WindowsLoginItem.Command(Exe), keys.Command);

            Assert.Null(item.Set(false));
            Assert.False(item.IsOn);
            Assert.Null(keys.Command);
        }

        [Theory]
        [InlineData((byte)0x02, false)]
        [InlineData((byte)0x06, false)]
        [InlineData((byte)0x03, true)]
        [InlineData((byte)0x07, true)]
        public void StartupApps_SwitchedOff_NeedsApproval(byte first, bool needs)
        {
            var item = new WindowsLoginItem(new FakeKeys { Command = "x", Approval = first }, Exe);

            Assert.Equal(needs, item.NeedsApproval);
        }

        [Fact]
        public void WithNoRunValue_NothingNeedsApproval()
        {
            Assert.False(new WindowsLoginItem(new FakeKeys { Approval = 3 }, Exe).NeedsApproval);
        }

        [Fact]
        public void TurningItOnInKeycuts_SwitchesItBackOnInStartupApps()
        {
            var keys = new FakeKeys { Command = "x", Approval = 3 };

            new WindowsLoginItem(keys, Exe).Set(true);

            Assert.Null(keys.Approval);
        }

        [Fact]
        public void ARefusal_ComesBackAsText()
        {
            var item = new WindowsLoginItem(new FakeKeys { Fails = new UnauthorizedAccessException("Access denied.") }, Exe);

            Assert.Equal("Windows refused: Access denied.", item.Set(true));
        }
    }
}
