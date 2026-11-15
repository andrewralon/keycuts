using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// Run counts (#187): the lines every keycut gets, what reads them back, and how renames, deletes and the upgrade of
    /// older keycuts keep them in step. Everything uses a temp usage folder, never the real %LOCALAPPDATA%.
    /// </summary>
    public class UsageTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;
        private readonly string _usageFolder;
        private readonly UsageLog _usage;

        public UsageTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "out");
            _usageFolder = Path.Combine(_root, "localappdata", "keycuts", "usage");
            Directory.CreateDirectory(_folder);
            _usage = new UsageLog(_usageFolder);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        #region Helpers

        private void SetRuns(string name, int runs, DateTime lastRun)
        {
            Directory.CreateDirectory(_usageFolder);
            var path = Path.Combine(_usageFolder, name + UsageLog.Extension);
            File.WriteAllText(path, string.Concat(Enumerable.Repeat("1\r\n", runs)));
            File.SetLastWriteTime(path, lastRun);
        }

        #endregion Helpers

        #region The lines in the .bat

        [Fact]
        public void TheBatFolder_IsTheFolderTheAppReads()
        {
            Assert.Equal(Environment.ExpandEnvironmentVariables(UsageLog.BatFolder), UsageLog.DefaultFolder, ignoreCase: true);
        }

        [Fact]
        public void TheUsageLines_AreNeverReadAsTheLaunchLine()
        {
            Assert.All(UsageLog.BatLines, line => Assert.False(ShortcutFile.IsLaunchLine(line)));
        }

        #endregion The lines in the .bat

        #region Reading

        [Theory]
        [InlineData("", 0)]
        [InlineData("1\r\n", 1)]
        [InlineData("1\r\n1\r\n1\r\n", 3)]
        [InlineData("1\n1\n", 2)]
        [InlineData("garbage", 1)]
        [InlineData("1\r\n1", 2)]
        public void CountRuns_IsTheNumberOfLines(string contents, int runs)
        {
            Assert.Equal(runs, UsageLog.CountRuns(Encoding.ASCII.GetBytes(contents)));
        }

        [Fact]
        public void ReadAll_IsByName_InAnyCase_WithTheLastRunFromTheFile()
        {
            var lastRun = new DateTime(2026, 3, 4, 5, 6, 7);
            SetRuns("Gmail", 3, lastRun);

            var records = _usage.ReadAll();

            Assert.Equal(new UsageRecord(3, lastRun), records["gmail"]);
        }

        [Fact]
        public void AMissingFolder_HasNoRecords_AndNoStart()
        {
            Assert.Empty(_usage.ReadAll());
            Assert.Null(_usage.Read("gmail"));
            Assert.Null(_usage.CountingSince());
        }

        #endregion Reading
    }
}
