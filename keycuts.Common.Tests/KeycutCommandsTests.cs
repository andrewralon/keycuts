using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #173: the CLI's rm, rename and open, and list --json. Temp folders and a permanent deleter only: never the real keycuts
    /// folder, the Recycle Bin, the registry or a real launch.
    /// </summary>
    public class KeycutCommandsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _folder;
        private readonly UsageLog _usage;
        private readonly StringWriter _output = new StringWriter();
        private readonly StringWriter _error = new StringWriter();
        private readonly List<(string Path, string Arguments)> _launched = new List<(string, string)>();

        public KeycutCommandsTests()
        {
            _folder = Path.Combine(_root, "out");
            Directory.CreateDirectory(_folder);
            _usage = new UsageLog(Path.Combine(_root, "usage"));
            _usage.EnsureFolder();
        }

        public void Dispose()
        {
            Directory.Delete(_root, true);
        }

        private string Create(string name, string target = "https://example.com")
        {
            var shortcut = new Shortcut(target, name + ShortcutFile.Extension, _folder);
            new Runner().CreateShortcutFile(shortcut, true, "");
            return shortcut.FullPath;
        }

        private KeycutCommandContext Context(Func<string, NameCheck> checkName = null, bool redirected = true, char answer = 'n')
        {
            return new KeycutCommandContext(new KeycutRepository(_folder, new PermanentDeleter(), null, _usage), _output, _error)
            {
                IsInputRedirected = redirected,
                ReadKey = () => answer,
                CheckName = checkName,
                Launch = (path, arguments) => _launched.Add((path, arguments))
            };
        }

        private static NameCheck Conflicting(string name)
        {
            return new NameCheck(name, null, new[] { new NameConflict(@"C:\Windows\notepad.exe", NameConflictEffect.Hidden, NameConflictSource.WindowsFolder) });
        }

        #region rm

        [Fact]
        public void Rm_DeletesTheKeycut_AndItsRunCount()
        {
            var path = Create("mail");
            File.WriteAllText(Path.Combine(_usage.Folder, "mail" + UsageLog.Extension), "1\r\n");

            Assert.Equal(ExitCode.Success, KeycutCommands.Remove(Context(), "mail"));

            Assert.False(File.Exists(path));
            Assert.Null(_usage.Read("mail"));
            Assert.Equal("Deleted mail.", _output.ToString().Trim());
        }

        [Fact]
        public void Rm_FindsTheNameInAnyCase()
        {
            var path = Create("mail");

            Assert.Equal(ExitCode.Success, KeycutCommands.Remove(Context(), "MAIL"));
            Assert.False(File.Exists(path));
        }

        [WindowsTheory]
        [InlineData("MAIL")]
        [InlineData("mail.bat")]
        public void Rm_FindsTheNameInAnyCase_WithOrWithoutDotBat(string typed)
        {
            var path = Create("mail");

            Assert.Equal(ExitCode.Success, KeycutCommands.Remove(Context(), typed));
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void Rm_ANameThatIsntAKeycut_ExitsCannotFindFile()
        {
            Assert.Equal(ExitCode.CannotFindFile, KeycutCommands.Remove(Context(), "nope"));
            Assert.Contains("there's no keycut named \"nope\"", _error.ToString());
        }

        [Fact]
        public void Rm_NeverDeletesTheKeycutsCommand()
        {
            CommandShims.Ensure(_folder);

            Assert.Equal(ExitCode.CannotFindFile, KeycutCommands.Remove(Context(), "keycuts"));
            Assert.True(File.Exists(Path.Combine(_folder, "keycuts" + CommandShims.Extension)));
        }

        #endregion

        #region rename

        [Fact]
        public void Rename_RenamesTheKeycut_AndMovesItsRunCount()
        {
            Create("mail");
            File.WriteAllText(Path.Combine(_usage.Folder, "mail" + UsageLog.Extension), "1\r\n1\r\n");

            Assert.Equal(ExitCode.Success, KeycutCommands.Rename(Context(), "mail", "gmail", false));

            Assert.True(File.Exists(Path.Combine(_folder, "gmail" + ShortcutFile.Extension)));
            Assert.False(File.Exists(Path.Combine(_folder, "mail" + ShortcutFile.Extension)));
            Assert.Equal(2, _usage.Read("gmail").Runs);
            Assert.Equal("Renamed mail to gmail.", _output.ToString().Trim());
        }

        [Fact]
        public void Rename_ToATakenName_IsRefused()
        {
            Create("mail");
            Create("gmail");

            Assert.Equal(ExitCode.FileAlreadyExists, KeycutCommands.Rename(Context(), "mail", "gmail", true));
            Assert.True(File.Exists(Path.Combine(_folder, "mail" + ShortcutFile.Extension)));
        }

        [Theory]
        [InlineData("keypaste")]
        [InlineData("a/b")]
        [InlineData("NUL")]
        [InlineData("")]
        public void Rename_ToAnInvalidName_IsRefused(string newName)
        {
            Create("mail");

            Assert.Equal(ExitCode.BadArguments, KeycutCommands.Rename(Context(), "mail", newName, true));
            Assert.True(File.Exists(Path.Combine(_folder, "mail" + ShortcutFile.Extension)));
        }

        [Fact]
        public void Rename_ToANameAnotherCommandHas_CantAskWithRedirectedInput()
        {
            Create("mail");

            Assert.Equal(ExitCode.ConfirmationNeeded, KeycutCommands.Rename(Context(Conflicting), "mail", "notepad", false));

            Assert.True(File.Exists(Path.Combine(_folder, "mail" + ShortcutFile.Extension)));
            Assert.Contains("already opens C:\\Windows\\notepad.exe", _output.ToString());
            Assert.Contains(KeycutCommands.RetryRenameWithForceMessage, _output.ToString());
        }

        [Theory]
        [InlineData('y', true)]
        [InlineData('n', false)]
        public void Rename_ToANameAnotherCommandHas_AsksFirst(char answer, bool renamed)
        {
            Create("mail");

            Assert.Equal(ExitCode.Success, KeycutCommands.Rename(Context(Conflicting, redirected: false, answer: answer), "mail", "notepad", false));

            Assert.Equal(renamed, File.Exists(Path.Combine(_folder, "notepad" + ShortcutFile.Extension)));
            Assert.Contains("Rename it anyway? (y / n)", _output.ToString());
        }

        [Fact]
        public void Rename_WithForce_DoesntAsk()
        {
            Create("mail");
            var asked = false;

            Assert.Equal(ExitCode.Success, KeycutCommands.Rename(Context(name => { asked = true; return Conflicting(name); }), "mail", "notepad", true));

            Assert.False(asked);
            Assert.True(File.Exists(Path.Combine(_folder, "notepad" + ShortcutFile.Extension)));
        }

        [Fact]
        public void Rename_ACaseOnlyChange_IsAllowed_AndDoesntCheckTheName()
        {
            Create("mail");

            Assert.Equal(ExitCode.Success, KeycutCommands.Rename(Context(name => throw new InvalidOperationException("not checked")), "mail", "Mail", false));
            Assert.Equal("Mail" + ShortcutFile.Extension, Path.GetFileName(Directory.GetFiles(_folder).Single()));
        }

        #endregion

        #region open

        [Fact]
        public void Open_RunsTheKeycutsBat_WithTheArguments()
        {
            var path = Create("mail");

            Assert.Equal(ExitCode.Success, KeycutCommands.Open(Context(), "mail", "inbox \"two words\""));

            Assert.Equal(new[] { (path, "inbox \"two words\"") }, _launched);
        }

        // Review of #253: what follows the name is the keycut's, so the option parser must not see it
        // The command line, its arguments separated by |
        [Theory]
        [InlineData("open|search|-x|--draft", "search", "-x --draft")]
        [InlineData("OPEN|g|-h", "g", "-h")]
        [InlineData("open|g|two words|", "g", "\"two words\" \"\"")]
        [InlineData("open|g", "g", "")]
        public void TryReadOpen_KeepsEverythingAfterTheName_ForTheKeycut(string commandLine, string name, string arguments)
        {
            Assert.True(KeycutCommands.TryReadOpen(commandLine.Split('|'), out var readName, out var readArguments));
            Assert.Equal((name, arguments), (readName, readArguments));
        }

        [Theory]
        [InlineData("open")]
        [InlineData("open|--help")]
        [InlineData("list|g")]
        public void TryReadOpen_LeavesTheRestToTheParser(string commandLine)
        {
            Assert.False(KeycutCommands.TryReadOpen(commandLine.Split('|'), out _, out _));
        }

        [Fact]
        public void Open_ANameThatIsntAKeycut_ExitsCannotFindFile_AndRunsNothing()
        {
            Assert.Equal(ExitCode.CannotFindFile, KeycutCommands.Open(Context(), "nope", ""));
            Assert.Empty(_launched);
        }

        [Fact]
        public void Open_ALaunchThatFails_ExitsCannotOpenFile()
        {
            Create("mail");
            var context = new KeycutCommandContext(new KeycutRepository(_folder, new PermanentDeleter()), _output, _error)
            {
                Launch = (path, arguments) => throw new System.ComponentModel.Win32Exception(5, "Access is denied")
            };

            Assert.Equal(ExitCode.CannotOpenFile, KeycutCommands.Open(context, "mail", ""));
            Assert.Contains("couldn't open mail", _error.ToString());
        }

        #endregion

        #region list --json

        [Fact]
        public void ListJson_HasEveryField_WithNullForNoRecord()
        {
            var keycuts = new[]
            {
                new Keycut { Name = "gmail", Kind = KeycutKind.Url, Target = "https://mail.google.com/?a=1&b=2", Description = "café", Runs = 3, LastRun = new DateTime(2026, 11, 16, 8, 0, 0), FilePath = @"C:\Shortcuts\gmail.bat" },
                new Keycut { Name = "old", Kind = KeycutKind.Folder, Target = @" C:\Old", FilePath = @"C:\Shortcuts\old.bat" }
            };

            var json = KeycutListing.ToJson(keycuts);
            using var document = JsonDocument.Parse(json);
            var items = document.RootElement.EnumerateArray().ToList();

            Assert.Equal(2, items.Count);
            Assert.Equal(new[] { "name", "type", "target", "description", "runs", "lastRun", "file" }, items[0].EnumerateObject().Select(p => p.Name));
            Assert.Equal("https://mail.google.com/?a=1&b=2", items[0].GetProperty("target").GetString());
            Assert.Equal(3, items[0].GetProperty("runs").GetInt32());
            Assert.Equal("2026-11-16T08:00:00", items[0].GetProperty("lastRun").GetString());
            Assert.Equal(JsonValueKind.Null, items[1].GetProperty("runs").ValueKind);
            Assert.Equal(JsonValueKind.Null, items[1].GetProperty("lastRun").ValueKind);
            Assert.Equal(@"C:\Old", items[1].GetProperty("target").GetString());
            Assert.Contains("café", json);   // readable, not \u00e9
            Assert.Contains("a=1&b=2", json);
        }

        [Fact]
        public void ListJson_OfNothing_IsAnEmptyArray()
        {
            Assert.Equal("[]", KeycutListing.ToJson(Array.Empty<Keycut>()).Trim());
        }

        #endregion
    }
}
