using System;
using System.IO;
using System.Linq;
using keycuts.Common;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// Keycuts named after the keycuts commands on PATH (keycuts, keypaste), which hide or are hidden by them: refused for
    /// new keycuts, and renamed aside at startup (<see cref="KeycutRepository.MoveAsideCommandNames"/>).
    /// </summary>
    public class CommandNameTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;

        public CommandNameTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "out");
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private string WriteKeycut(string name, string target = @"C:\Tools\app.exe")
        {
            var path = Path.Combine(_folder, name + ".bat");
            File.WriteAllLines(path, new[]
            {
                "@ECHO OFF",
                "REM keycuts 1.1.1",
                $"REM <shortcut>{path}</shortcut>",
                "REM <type>file</type>",
                $"REM <destination>{target}</destination>",
                $"START \"\" /B \"{target}\" %*",
                "EXIT"
            });
            return path;
        }

        private string[] BatNames()
        {
            return Directory.GetFiles(_folder, "*.bat").Select(Path.GetFileNameWithoutExtension).OrderBy(name => name).ToArray();
        }

        [Fact]
        public void The_command_names_are_the_shims_on_PATH()
        {
            var shims = new[] { "keycuts.cmd", "keypaste.cmd" }
                .Select(shim => ContextMenuRegistrarTests.FindRepoFile(Path.Combine("keycuts.CLI", "shims", shim)))
                .Select(Path.GetFileNameWithoutExtension);

            Assert.Equal(shims.OrderBy(name => name), CommandNames.All.OrderBy(name => name));
        }

        [Theory]
        [InlineData("keycuts")]
        [InlineData("keypaste")]
        [InlineData("KeyPaste")]
        public void A_command_name_is_not_a_valid_keycut_name(string name)
        {
            Assert.False(KeycutCreator.IsValidName(name));
        }

        [Theory]
        [InlineData("kc")]
        [InlineData("keycuts2")]
        [InlineData("keypaste_previous")]
        [InlineData("keypaste.old")]
        public void Names_that_only_start_like_a_command_are_valid(string name)
        {
            Assert.True(KeycutCreator.IsValidName(name));
        }

        [Fact]
        public void Both_are_renamed_with_the_previous_suffix_and_still_work()
        {
            WriteKeycut("keycuts", @"C:\Apps\keycuts.GUI.exe");
            WriteKeycut("keypaste", @"C:\Old\keypaste.exe");
            WriteKeycut("other");

            var renamed = new KeycutRepository(_folder).MoveAsideCommandNames();

            Assert.Equal(new[] { "keycuts -> keycuts_previous", "keypaste -> keypaste_previous" },
                renamed.Select(keycut => keycut.OldName + " -> " + keycut.NewName));
            Assert.Equal(new[] { "keycuts_previous", "keypaste_previous", "other" }, BatNames());

            // Renamed, not re-emitted: the REM <shortcut> line follows the file and the launch line is untouched
            var lines = File.ReadAllLines(Path.Combine(_folder, "keypaste_previous.bat"));
            Assert.Contains($"REM <shortcut>{Path.Combine(_folder, "keypaste_previous.bat")}</shortcut>", lines);
            Assert.Contains("START \"\" /B \"C:\\Old\\keypaste.exe\" %*", lines);
        }

        [Fact]
        public void A_taken_previous_name_gets_a_number()
        {
            WriteKeycut("keypaste");
            WriteKeycut("keypaste_previous");
            WriteKeycut("keypaste_previous2");

            var renamed = new KeycutRepository(_folder).MoveAsideCommandNames();

            Assert.Equal("keypaste_previous3", renamed.Single().NewName);
            Assert.Equal(new[] { "keypaste_previous", "keypaste_previous2", "keypaste_previous3" }, BatNames());
        }

        [Fact]
        public void The_old_name_is_reported_as_the_file_spells_it()
        {
            WriteKeycut("KeyPaste");

            var renamed = new KeycutRepository(_folder).MoveAsideCommandNames().Single();

            Assert.Equal("KeyPaste", renamed.OldName);
            Assert.Equal("keypaste_previous", renamed.NewName);
        }

        [Fact]
        public void Nothing_to_rename_changes_nothing()
        {
            WriteKeycut("kc");
            WriteKeycut("keycuts2");

            Assert.Empty(new KeycutRepository(_folder).MoveAsideCommandNames());
            Assert.Equal(new[] { "kc", "keycuts2" }, BatNames());
        }

        [Fact]
        public void A_missing_folder_is_left_missing()
        {
            var missing = Path.Combine(_root, "missing");

            Assert.Empty(new KeycutRepository(missing).MoveAsideCommandNames());
            Assert.False(Directory.Exists(missing));
        }

        [Fact]
        public void A_locked_keycut_is_left_and_the_other_is_still_renamed()
        {
            var keycuts = WriteKeycut("keycuts");
            WriteKeycut("keypaste");

            using (new FileStream(keycuts, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var renamed = new KeycutRepository(_folder).MoveAsideCommandNames();

                Assert.Equal("keypaste_previous", renamed.Single().NewName);
            }

            Assert.Equal(new[] { "keycuts", "keypaste_previous" }, BatNames());
        }

        [Fact]
        public void The_message_for_a_command_name_says_so()
        {
            Assert.Equal("\u201Ckeypaste\u201D is a keycuts command. Choose another name.", CreatorViewModel.GetInvalidNameMessage(" keypaste "));
            Assert.Equal(CreatorViewModel.InvalidNameMessage, CreatorViewModel.GetInvalidNameMessage("a|b"));
        }

        [Fact]
        public void The_notice_lists_each_rename()
        {
            var notice = CommandNameNotice.Format(new[]
            {
                new RenamedKeycut("keycuts", "keycuts_previous"),
                new RenamedKeycut("keypaste", "keypaste_previous")
            });

            Assert.StartsWith("These keycuts had the same name as a keycuts command", notice);
            Assert.Contains("keycuts  \u2192  keycuts_previous", notice);
            Assert.Contains("keypaste  \u2192  keypaste_previous", notice);
        }

        [Fact]
        public void The_notice_for_one_rename_is_singular()
        {
            var notice = CommandNameNotice.Format(new[] { new RenamedKeycut("keypaste", "keypaste_previous") });

            Assert.StartsWith("This keycut had the same name as a keycuts command", notice);
            Assert.Contains("renamed it:", notice);
        }
    }
}
