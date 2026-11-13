using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The Manager's Edit form writes a keycut again (#180): which files it may, and what it writes.</summary>
    public class KeycutEditTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;

        public KeycutEditTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keycuts-tests-" + Guid.NewGuid().ToString("N"));
            _folder = Path.Combine(_root, "out");
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_root, true);
        }

        private KeycutRepository Repo() => new KeycutRepository(_folder);

        /// <summary>A keycut as the current emitter writes it, without touching the disk for the target.</summary>
        private string Emit(string name, ShortcutType type, string target, string description = "")
        {
            var path = Path.Combine(_folder, name + ".bat");
            var shortcut = Shortcut.FromParts(type, target, path);
            File.WriteAllLines(path, Runner.BuildShortcutLines(shortcut, description));
            return path;
        }

        private string WriteLines(string name, params string[] lines)
        {
            var path = Path.Combine(_folder, name + ".bat");
            File.WriteAllLines(path, lines);
            return path;
        }

        private async Task<Keycut> LoadOne(string name)
        {
            return (await Repo().LoadAllAsync()).Single(k => k.Name == name);
        }

        private string[] Bats()
        {
            return Directory.GetFiles(_folder, "*.bat").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        private string[] Everything()
        {
            return Directory.GetFileSystemEntries(_folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        #region Which files the form may rewrite

        [Fact]
        public void AFileTheCurrentEmitterWrote_CanBeEdited()
        {
            var path = Emit("gmail", ShortcutType.Url, "https://mail.google.com", "Work email");

            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void TheOlderDestinationTag_AndAnOldVersionHeader_DontMatter()
        {
            var path = WriteLines("old",
                "@ECHO OFF", "REM keycuts 1.1.0.0", @"REM <shortcut>C:\Moved\old.bat</shortcut>", "REM <type>url</type>",
                "REM <destination>https://example.com</destination>", "REM <description>Kept</description>",
                "START https://example.com", "EXIT");

            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Theory]
        [InlineData(ShortcutType.Folder, @"C:\Users\me\Downloads")]
        [InlineData(ShortcutType.Folder, @"%USERPROFILE%\Google Drive")]
        [InlineData(ShortcutType.CLSIDKey, "::{645FF040-5081-101B-9F08-00AA002F954E}")]
        public async Task AFolderOrCLSIDKeycutWithoutStart_StaysEditable_AndSavingWritesTheStartForm(ShortcutType type, string target)
        {
            // Before #160 the emitter called explorer.exe directly. Those files run the same, so refusing them would lock
            // out most existing folders; the first save moves them to START (cmd no longer waits for explorer.exe)
            var path = Emit("old", type, target, "Kept");
            var startLine = $"START \"\" /B \"%SystemRoot%\\explorer.exe\" \"{target}\"";
            var oldLine = $"\"%SystemRoot%\\explorer.exe\" \"{target}\"";
            var current = File.ReadAllLines(path);
            Assert.Contains(startLine, current);
            File.WriteAllLines(path, current.Select(l => l == startLine ? oldLine : l));

            Assert.True(KeycutRepository.CanEditInForm(path));

            await Repo().UpdateAsync(await LoadOne("old"), new KeycutEdit(target, "old", "Kept"));

            Assert.Equal(current, File.ReadAllLines(path));
        }

        [Fact]
        public void AnExplorerLineWithAnotherProgramOrArgument_IsStillNotEditable()
        {
            // Only the emitter's own old line is treated as the START form, not any call to explorer.exe
            var path = WriteLines("odd",
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\odd.bat</shortcut>", "REM <type>folder</type>",
                @"REM <target>C:\Dev</target>", "REM <description></description>",
                "\"%SystemRoot%\\explorer.exe\" \"C:\\Dev\" /select", "EXIT");

            Assert.False(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void AKeycutFromBeforeDescriptions_HasNoDescriptionLine_AndCanStillBeEdited()
        {
            // keycuts 1.0 wrote no REM <description>; adding an empty one loses nothing
            var path = WriteLines("v10",
                "@ECHO OFF", "REM keycuts 1.0.0.0", @"REM <shortcut>C:\Shortcuts\v10.bat</shortcut>", "REM <type>url</type>",
                "REM <destination>https://toolbox.googleapps.com/apps/dig/</destination>",
                "START https://toolbox.googleapps.com/apps/dig/", "EXIT");

            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void ADescriptionThatIsThere_MustMatchTheFile()
        {
            // No description line, but the REM lines don't say it's empty either: a file with a description line that
            // differs in spelling is compared as it is
            var path = WriteLines("odd",
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\odd.bat</shortcut>", "REM <type>url</type>",
                "REM <target>https://example.com</target>", "REM <description>  padded  </description>",
                "START https://example.com", "EXIT");

            // The parser trims what it reads, so the emitter writes it without the padding: not identical
            Assert.False(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void AHandEditedLaunchLine_CannotBeEdited()
        {
            var path = WriteLines("kp",
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\kp.bat</shortcut>", "REM <type>file</type>",
                @"REM <target>C:\Tools\app.exe</target>", "REM <description></description>",
                "START \"\" /B \"C:\\Tools\\app.exe\" %*", "EXIT");

            Assert.False(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void AnExtraLineAddedByHand_CannotBeEdited()
        {
            var path = Emit("extra", ShortcutType.Url, "https://example.com");
            File.AppendAllText(path, "REM my own note\r\n");

            Assert.False(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public void AFileThatIsNotAKeycut_AndAMissingFile_CannotBeEdited()
        {
            var script = WriteLines("script", "@ECHO OFF", "ECHO hello", "EXIT");

            Assert.False(KeycutRepository.CanEditInForm(script));
            Assert.False(KeycutRepository.CanEditInForm(Path.Combine(_folder, "missing.bat")));
        }

        [Fact]
        public void TheOldCommandLine_WhichTheEmitterNowWritesDifferently_CannotBeEdited()
        {
            // 0.1.x wrote START "" /B "<command>"; the emitter now writes cmd /C
            var path = WriteLines("ip",
                "@ECHO OFF", "REM keycuts 1.0.0.0", @"REM <shortcut>C:\Shortcuts\ip.bat</shortcut>", "REM <type>command</type>",
                "REM <destination>ipconfig /all</destination>", "START \"\" /B \"ipconfig /all\"", "EXIT");

            Assert.False(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public async Task TheAsyncCheck_SaysNotFoundForAGoneFile_NotJustNo()
        {
            var path = Emit("gone", ShortcutType.Url, "https://example.com");
            var keycut = await LoadOne("gone");
            File.Delete(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().CanEditInFormAsync(keycut));

            Assert.Equal(KeycutRepositoryError.NotFound, ex.Error);
        }

        [Fact]
        public async Task TheAsyncCheck_ReadsTheFile()
        {
            Emit("ok", ShortcutType.Url, "https://example.com");

            Assert.True(await Repo().CanEditInFormAsync(await LoadOne("ok")));
        }

        // What a real folder of keycuts does: pins which of the corpus files the form offers to, so a change to the
        // emitter or the parser that moves one of them shows up here
        [Fact]
        public void TheCorpus_OffersTheFormToTheFilesTheEmitterWritesIdentically()
        {
            var root = FindRepoRoot();
            var corpus = Path.Combine(root, "tests", "corpus", "legacy");
            var editable = Directory.EnumerateFiles(corpus, "*.bat", SearchOption.AllDirectories)
                .Where(file => KeycutRepository.CanEditInForm(file))
                .Select(file => Path.GetRelativePath(corpus, file).Replace('\\', '/'))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "edge-cases/embedded-quote.bat",
                    "edge-cases/hostsfile-type.bat",
                    "edge-cases/lf-line-endings.bat",
                    "edge-cases/unc-path.bat",
                    "edge-cases/unicode-destination.bat",
                    "new-format/gdig.bat",
                    "new-format/gdrive.bat",
                    "new-format/gdriveweb.bat",
                    "new-format/inception.bat",
                    "new-format/kc.bat",
                    "new-format/keycuts.bat",
                    "new-format/programs.bat",
                    "new-format/rb.bat",
                    "new-format/recyclebin.bat",
                    "target-tag/description-percent.bat",
                    "target-tag/folder.bat",
                    "target-tag/url.bat",
                },
                editable);

            // Not offered, each for a reason: a hand-added %* (keypaste, kp), a REM target that differs from the launch
            // line (d, header-content-drift), the old command line (command-type), the 0.1.x format (old-format/*),
            // an open-with program the parser doesn't read back (openwithapp), trailing spaces (trailing-whitespace),
            // and scripts that aren't keycuts (not-keycuts/*)
        }

        [Fact]
        public async Task TheCorpusFilesThatAreOffered_CanBeEditedAgainAfterBeingWritten()
        {
            // Whatever the form writes must be editable again, or it would lock a keycut out after one save
            var corpus = Path.Combine(FindRepoRoot(), "tests", "corpus", "legacy");

            foreach (var file in Directory.EnumerateFiles(corpus, "*.bat", SearchOption.AllDirectories).Where(f => KeycutRepository.CanEditInForm(f)))
            {
                var parsed = new ShortcutFile(file, File.ReadAllLines(file));
                var copy = Path.Combine(_folder, "copy.bat");
                File.Copy(file, copy, true);
                var keycut = new Keycut { Name = "copy", FilePath = copy, Target = parsed.Target, Description = parsed.Description };

                var saved = await Repo().UpdateAsync(keycut, new KeycutEdit(parsed.Target, "copy", parsed.Description));

                Assert.True(KeycutRepository.CanEditInForm(saved.FilePath), file);
                Assert.Equal(parsed.Target, saved.Target);
                Assert.Equal(parsed.Type, new ShortcutFile(saved.FilePath, File.ReadAllLines(saved.FilePath)).Type);
            }
        }

        // #202: the manager greys out Edit from what the load found, so the flag has to say what the form would
        [Fact]
        public async Task ALoadedKeycut_CarriesTheAnswerOfTheFormsCheck()
        {
            Emit("fine", ShortcutType.Url, "https://example.com");
            WriteLines("handmade",
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\handmade.bat</shortcut>", "REM <type>file</type>",
                @"REM <target>C:\Tools\app.exe</target>", "REM <description></description>",
                "START \"\" /B \"C:\\Tools\\app.exe\" %*", "EXIT");
            WriteLines("zeropointone", "@ECHO OFF", "REM keycuts 0.1.1.5", @"REM <url>C:\Shortcuts\zeropointone.bat</url>", "START https://example.com", "EXIT");

            var all = (await Repo().LoadAllAsync()).ToDictionary(k => k.Name);

            Assert.True(all["fine"].CanEditInForm);
            Assert.False(all["handmade"].CanEditInForm);
            Assert.False(all["zeropointone"].CanEditInForm);
        }

        // A listed keycut that isn't valid UTF-8 keeps its row, read leniently as before, but the form would lose bytes of it
        [Fact]
        public async Task AFileThatIsNotUtf8_IsStillListed_ButCannotBeEdited()
        {
            var lines = new[]
            {
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\cafe.bat</shortcut>", "REM <type>url</type>",
                "REM <target>https://example.com</target>", "REM <description>caf\u00E9</description>", "START https://example.com", "EXIT"
            };
            File.WriteAllBytes(Path.Combine(_folder, "cafe.bat"), System.Text.Encoding.Latin1.GetBytes(string.Join("\r\n", lines) + "\r\n"));

            var keycut = await LoadOne("cafe");

            Assert.False(keycut.CanEditInForm);
            Assert.Equal("https://example.com", keycut.Target);
            Assert.Equal(keycut.CanEditInForm, KeycutRepository.CanEditInForm(keycut.FilePath));
        }

        [Fact]
        public async Task AFileWithAUtf8ByteOrderMark_CanStillBeEdited()
        {
            var path = Emit("bom", ShortcutType.Url, "https://example.com", "Résumé");
            File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetPreamble().Concat(File.ReadAllBytes(path)).ToArray());

            var keycut = await LoadOne("bom");

            Assert.True(keycut.CanEditInForm);
            Assert.Equal("Résumé", keycut.Description);
        }

        // The same answer as the static check, for every real file, so the menu and Ctrl+E can't disagree with the form
        [Fact]
        public async Task TheCorpus_LoadedWithTheSameAnswerAsTheFormsOwnCheck()
        {
            var corpus = Path.Combine(FindRepoRoot(), "tests", "corpus", "legacy");

            foreach (var file in Directory.EnumerateFiles(corpus, "*.bat", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(_folder, Path.GetRelativePath(corpus, file).Replace('\\', '_').Replace('/', '_')), true);
            }

            var all = await Repo().LoadAllAsync();

            Assert.True(all.Count > 20);

            foreach (var keycut in all)
            {
                Assert.True(keycut.CanEditInForm == KeycutRepository.CanEditInForm(keycut.FilePath), keycut.FilePath);
            }

            Assert.Contains(all, k => k.CanEditInForm);
            Assert.Contains(all, k => !k.CanEditInForm);
        }

        private static string FindRepoRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);

            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "keycuts.sln")))
            {
                folder = folder.Parent;
            }

            return folder?.FullName ?? throw new InvalidOperationException("keycuts.sln not found above " + AppContext.BaseDirectory);
        }

        #endregion Which files the form may rewrite

        #region Saving

        [Fact]
        public async Task ChangingOnlyTheDescription_RewritesTheFile_KeepingTheLaunchLine()
        {
            var path = Emit("gmail", ShortcutType.Url, "https://mail.google.com/mail/u/0/#inbox");
            var before = File.ReadAllLines(path);

            var saved = await Repo().UpdateAsync(await LoadOne("gmail"), new KeycutEdit("https://mail.google.com/mail/u/0/#inbox", "gmail", "Work email"));

            var after = File.ReadAllLines(path);
            Assert.Equal("Work email", saved.Description);
            Assert.Equal(path, saved.FilePath);
            Assert.Contains("REM <description>Work email</description>", after);
            Assert.Equal(before.Where(l => !l.StartsWith("REM <description>")), after.Where(l => !l.StartsWith("REM <description>")));
            Assert.Equal(new[] { "gmail.bat" }, Everything());
        }

        [Fact]
        public async Task AnUnchangedTarget_IsWrittenExactlyAsItWas_EvenWithEnvironmentVariables()
        {
            // A folder saved with a variable: the type and target aren't read again, so the variable is still there
            var target = @"%KC_EDIT_TEST%\Dropbox";
            var path = Emit("db", ShortcutType.Folder, target, "Dropbox");
            var before = File.ReadAllLines(path);

            var saved = await Repo().UpdateAsync(await LoadOne("db"), new KeycutEdit(target, "db", "Dropbox folder"));

            Assert.Equal(target, saved.Target);
            Assert.Equal(KeycutKind.Folder, saved.Kind);
            Assert.Equal(before.Select(l => l.Replace("REM <description>Dropbox</description>", "REM <description>Dropbox folder</description>")), File.ReadAllLines(path));
        }

        [Fact]
        public async Task ANewTarget_IsResolvedLikeANewKeycut_AndChangesTheTypeAndTheLaunchLine()
        {
            Emit("site", ShortcutType.Url, "https://example.com");
            var folderTarget = Path.Combine(_root, "target dir");
            Directory.CreateDirectory(folderTarget);

            var saved = await Repo().UpdateAsync(await LoadOne("site"), new KeycutEdit(folderTarget, "site", ""));

            Assert.Equal(KeycutKind.Folder, saved.Kind);
            Assert.Equal(folderTarget, saved.Target);
            var lines = File.ReadAllLines(saved.FilePath);
            Assert.Contains($"REM <target>{folderTarget}</target>", lines);
            Assert.Contains("REM <type>folder</type>", lines);
            Assert.Contains($"START \"\" /B \"%SystemRoot%\\explorer.exe\" \"{folderTarget}\"", lines);
        }

        [Fact]
        public async Task ANewTarget_CanBeSavedWithEnvironmentVariables_LikeANewKeycut()
        {
            Emit("site", ShortcutType.Url, "https://example.com");
            var file = Path.Combine(_root, "notes.txt");
            File.WriteAllText(file, "x");
            var edit = new KeycutEdit(file, "site", "")
            {
                UseEnvironmentVariables = true,
                PathVariables = new[] { new PathVariable("KC_EDIT_TEST", _root) }
            };

            var saved = await Repo().UpdateAsync(await LoadOne("site"), edit);

            Assert.Equal(@"%KC_EDIT_TEST%\notes.txt", saved.Target);
            Assert.Contains(@"START """" /B ""%KC_EDIT_TEST%\notes.txt""", File.ReadAllLines(saved.FilePath));
        }

        [Fact]
        public async Task ANewTargetWithVariablesTypedIn_Resolves_AndIsWrittenAsTheRealPathWhenTheSettingIsOff()
        {
            Emit("site", ShortcutType.Url, "https://example.com");
            var file = Path.Combine(_root, "notes.txt");
            File.WriteAllText(file, "x");
            Environment.SetEnvironmentVariable("KC_EDIT_TYPED", _root);

            try
            {
                var saved = await Repo().UpdateAsync(await LoadOne("site"), new KeycutEdit(@"%KC_EDIT_TYPED%\notes.txt", "site", ""));

                Assert.Equal(file, saved.Target);
            }
            finally
            {
                Environment.SetEnvironmentVariable("KC_EDIT_TYPED", null);
            }
        }

        [Fact]
        public async Task ANewName_IsARenameAndAnEditInOne_TheOldFileIsGone()
        {
            var old = Emit("old", ShortcutType.Url, "https://example.com", "Note");

            var saved = await Repo().UpdateAsync(await LoadOne("old"), new KeycutEdit("https://example.com", "fresh", "Note"));

            var fresh = Path.Combine(_folder, "fresh.bat");
            Assert.Equal(fresh, saved.FilePath);
            Assert.Equal("fresh", saved.Name);
            Assert.False(File.Exists(old));
            Assert.Equal(new[] { "fresh.bat" }, Everything());
            Assert.Contains($"REM <shortcut>{fresh}</shortcut>", File.ReadAllLines(fresh));
            Assert.Equal("fresh", (await Repo().LoadAllAsync()).Single().Name);
        }

        [Fact]
        public async Task ANameThatIsTaken_IsRefused_AndNeitherFileChanges()
        {
            var one = Emit("one", ShortcutType.Url, "https://example.com/one");
            var two = Emit("two", ShortcutType.Url, "https://example.com/two");
            var oneBytes = File.ReadAllBytes(one);
            var twoBytes = File.ReadAllBytes(two);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(LoadOne("one").Result, new KeycutEdit("https://example.com/changed", "two", "changed")));

            Assert.Equal(KeycutRepositoryError.AlreadyExists, ex.Error);
            Assert.Equal(oneBytes, File.ReadAllBytes(one));
            Assert.Equal(twoBytes, File.ReadAllBytes(two));
            Assert.Equal(new[] { "one.bat", "two.bat" }, Everything());
        }

        [Fact]
        public async Task ACaseOnlyRename_IsAllowed_AndLeavesOneFile()
        {
            Emit("Docs", ShortcutType.Url, "https://example.com");

            var saved = await Repo().UpdateAsync(await LoadOne("Docs"), new KeycutEdit("https://example.com", "docs", "now lower"));

            Assert.Equal(new[] { "docs.bat" }, Everything());
            Assert.Equal("docs", saved.Name);
            Assert.Equal("now lower", saved.Description);
            Assert.Contains($"REM <shortcut>{Path.Combine(_folder, "docs.bat")}</shortcut>", File.ReadAllLines(saved.FilePath));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a/b")]
        [InlineData("what?")]
        [InlineData("keycuts")]
        public async Task AnInvalidName_IsRefused_AndTheFileIsUntouched(string name)
        {
            var path = Emit("keep", ShortcutType.Url, "https://example.com");
            var bytes = File.ReadAllBytes(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(LoadOne("keep").Result, new KeycutEdit("https://example.com", name, "")));

            Assert.Equal(KeycutRepositoryError.InvalidName, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Fact]
        public async Task ATargetThatCantBeUsed_IsRefusedWithTheReason_AndTheFileIsUntouched()
        {
            var path = Emit("keep", ShortcutType.Url, "https://example.com");
            var bytes = File.ReadAllBytes(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(LoadOne("keep").Result, new KeycutEdit(Path.Combine(_root, "does", "not", "exist.txt"), "keep", "")));

            Assert.Equal(KeycutRepositoryError.InvalidTarget, ex.Error);
            Assert.Equal(TargetError.NotFound, ex.TargetError);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(new[] { "keep.bat" }, Everything());
        }

        [Fact]
        public async Task AHandEditedFile_IsRefused_AndTheFileIsUntouched()
        {
            var path = WriteLines("hand",
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\hand.bat</shortcut>", "REM <type>url</type>",
                "REM <target>https://example.com</target>", "REM <description></description>",
                "START https://example.com", "ECHO and then something of mine", "EXIT");
            var bytes = File.ReadAllBytes(path);
            var keycut = new Keycut { Name = "hand", FilePath = path, Target = "https://example.com" };

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "hand", "new")));

            Assert.Equal(KeycutRepositoryError.NotEditable, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Fact]
        public async Task AFileThatIsGone_IsNotFound()
        {
            var path = Emit("gone", ShortcutType.Url, "https://example.com");
            var keycut = await LoadOne("gone");
            File.Delete(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "gone", "x")));

            Assert.Equal(KeycutRepositoryError.NotFound, ex.Error);
        }

        [Fact]
        public async Task AnOlderKeycut_IsWrittenAgainWithTheCurrentTagAndHeader()
        {
            var path = WriteLines("older",
                "@ECHO OFF", "REM keycuts 1.0.0.0", @"REM <shortcut>C:\Shortcuts\older.bat</shortcut>", "REM <type>url</type>",
                "REM <destination>https://example.com</destination>", "START https://example.com", "EXIT");

            await Repo().UpdateAsync(await LoadOne("older"), new KeycutEdit("https://example.com", "older", "Now with a note"));

            var lines = File.ReadAllLines(path);
            Assert.Contains("REM <target>https://example.com</target>", lines);
            Assert.DoesNotContain(lines, l => l.Contains("<destination>"));
            Assert.Contains($"REM {Runner.AppName} {Runner.Version}", lines);
            Assert.Contains("REM <description>Now with a note</description>", lines);
        }

        [Fact]
        public async Task ADescriptionWithPercentTilde_IsWrittenEscaped_AndReadsBackAsTyped()
        {
            var path = Emit("pct", ShortcutType.Url, "https://example.com");

            var saved = await Repo().UpdateAsync(await LoadOne("pct"), new KeycutEdit("https://example.com", "pct", "Opens %~dp0 at 100%"));

            Assert.Contains("REM <description>Opens %%~dp0 at 100%%</description>", File.ReadAllLines(path));
            Assert.Equal("Opens %~dp0 at 100%", saved.Description);
            Assert.True(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public async Task ASavedKeycut_CanBeEditedAgain()
        {
            Emit("again", ShortcutType.Url, "https://example.com");
            var first = await Repo().UpdateAsync(await LoadOne("again"), new KeycutEdit("https://example.com", "again", "one"));

            var second = await Repo().UpdateAsync(first, new KeycutEdit("https://example.com/next", "again2", "two"));

            Assert.Equal("again2", second.Name);
            Assert.Equal("two", second.Description);
            Assert.Equal("https://example.com/next", second.Target);
            Assert.Equal(new[] { "again2.bat" }, Everything());
        }

        [Fact]
        public async Task AFailedWrite_LeavesTheKeycutAsItWasAndNoTempFile()
        {
            var path = Emit("locked", ShortcutType.Url, "https://example.com");
            var bytes = File.ReadAllBytes(path);
            var keycut = await LoadOne("locked");
            File.SetAttributes(path, FileAttributes.ReadOnly);

            var ex = await Assert.ThrowsAnyAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "locked", "will not be saved")));

            Assert.True(ex.Error == KeycutRepositoryError.AccessDenied || ex.Error == KeycutRepositoryError.InUse);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(new[] { "locked.bat" }, Everything());
        }

        [Fact]
        public async Task ANewNameForAFileThatCantBeRenamed_LeavesTheOldKeycutAndNoCopy()
        {
            var old = Emit("old", ShortcutType.Url, "https://example.com");
            var bytes = File.ReadAllBytes(old);
            var keycut = await LoadOne("old");

            using (new FileStream(old, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Open for reading without FileShare.Delete, which a rename needs too
                var ex = await Assert.ThrowsAnyAsync<KeycutRepositoryException>(
                    () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "fresh", "")));

                Assert.True(ex.Error == KeycutRepositoryError.InUse || ex.Error == KeycutRepositoryError.AccessDenied);
            }

            Assert.Equal(new[] { "old.bat" }, Everything());
            Assert.Equal(bytes, File.ReadAllBytes(old));
        }

        [Fact]
        public async Task ARenameWhoseContentCantBeWritten_PutsTheNameBack()
        {
            // A read-only file can be moved but not replaced: the rename happens first, so the failure has to undo it
            var old = Emit("old", ShortcutType.Url, "https://example.com");
            var bytes = File.ReadAllBytes(old);
            var keycut = await LoadOne("old");
            File.SetAttributes(old, FileAttributes.ReadOnly);

            var ex = await Assert.ThrowsAnyAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "fresh", "not saved")));

            Assert.True(ex.Error == KeycutRepositoryError.AccessDenied || ex.Error == KeycutRepositoryError.InUse);
            Assert.Equal(new[] { "old.bat" }, Everything());
            Assert.Equal(bytes, File.ReadAllBytes(old));
        }

        [Fact]
        public async Task AnEdit_KeepsTheFilesAlternateStreams_WithOrWithoutARename()
        {
            // The "downloaded from the internet" mark: a copy and a delete would have lost it
            var path = Emit("marked", ShortcutType.Url, "https://example.com");
            const string mark = "[ZoneTransfer]\r\nZoneId=3\r\n";

            try
            {
                File.WriteAllText(path + ":Zone.Identifier", mark);
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is IOException || ex is ArgumentException)
            {
                // Not an NTFS volume, so there is nothing to keep
                return;
            }

            var first = await Repo().UpdateAsync(await LoadOne("marked"), new KeycutEdit("https://example.com", "marked", "one"));
            Assert.Equal(mark, File.ReadAllText(first.FilePath + ":Zone.Identifier"));

            var second = await Repo().UpdateAsync(first, new KeycutEdit("https://example.com", "renamed", "two"));

            Assert.Equal(new[] { "renamed.bat" }, Everything());
            Assert.Equal(mark, File.ReadAllText(second.FilePath + ":Zone.Identifier"));
        }

        #endregion Saving

        #region Files that changed, and files that aren't UTF-8

        [Fact]
        public async Task AFileChangedSinceTheFormOpened_IsRefused_RatherThanPuttingTheOldTargetBack()
        {
            var path = Emit("moved", ShortcutType.Url, "https://example.com/original");
            var opened = await LoadOne("moved");

            // Another window saves a new target while the form is open
            Emit("moved", ShortcutType.Url, "https://example.com/changed-elsewhere");
            var elsewhere = File.ReadAllBytes(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(opened, new KeycutEdit("https://example.com/original", "moved", "only the description changed")));

            Assert.Equal(KeycutRepositoryError.Changed, ex.Error);
            Assert.Equal(elsewhere, File.ReadAllBytes(path));
        }

        [Fact]
        public async Task ADescriptionChangedSinceTheFormOpened_IsRefusedToo()
        {
            var path = Emit("noted", ShortcutType.Url, "https://example.com", "first");
            var opened = await LoadOne("noted");
            Emit("noted", ShortcutType.Url, "https://example.com", "second, written elsewhere");
            var elsewhere = File.ReadAllBytes(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(opened, new KeycutEdit("https://example.com/new", "noted", "first")));

            Assert.Equal(KeycutRepositoryError.Changed, ex.Error);
            Assert.Equal(elsewhere, File.ReadAllBytes(path));
        }

        [Fact]
        public async Task AFileThatHasNotChanged_IsSavedAsUsual_EvenAfterAnotherSaveOfTheSameValues()
        {
            Emit("same", ShortcutType.Url, "https://example.com", "note");
            var opened = await LoadOne("same");
            // Rewritten with the very same target and description: nothing a save could put back
            Emit("same", ShortcutType.Url, "https://example.com", "note");

            var saved = await Repo().UpdateAsync(opened, new KeycutEdit("https://example.com", "same", "changed"));

            Assert.Equal("changed", saved.Description);
        }

        // 0xE9 is "é" in ANSI, which is how an older Notepad saved a file: not UTF-8, so it would read as U+FFFD and be lost
        private static byte[] AnsiKeycut(string description)
        {
            var head = System.Text.Encoding.ASCII.GetBytes(
                "@ECHO OFF\r\nREM keycuts 1.3.0.0\r\nREM <shortcut>C:\\Shortcuts\\ansi.bat</shortcut>\r\nREM <type>url</type>\r\n" +
                "REM <target>https://example.com</target>\r\nREM <description>");
            var tail = System.Text.Encoding.ASCII.GetBytes("</description>\r\nSTART https://example.com\r\nEXIT\r\n");

            return head.Concat(System.Text.Encoding.GetEncoding(28591).GetBytes(description)).Concat(tail).ToArray();
        }

        [Fact]
        public async Task AFileThatIsNotUtf8_IsNotOffered_AndIsLeftByteForByteIfSaved()
        {
            var path = Path.Combine(_folder, "ansi.bat");
            File.WriteAllBytes(path, AnsiKeycut("r\u00E9sum\u00E9"));
            var bytes = File.ReadAllBytes(path);

            Assert.False(KeycutRepository.CanEditInForm(path));

            var keycut = new Keycut { Name = "ansi", FilePath = path, Target = "https://example.com" };
            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "ansi", "new")));

            Assert.Equal(KeycutRepositoryError.NotEditable, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.False(await Repo().CanEditInFormAsync(keycut));
        }

        [Fact]
        public async Task AFileWithABomAndABadByte_IsNotOfferedEither()
        {
            // A BOM must not turn the strict decoder into one that replaces what it can't read
            var path = Path.Combine(_folder, "bom-ansi.bat");
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(AnsiKeycut("résumé")).ToArray();
            File.WriteAllBytes(path, bytes);

            Assert.False(KeycutRepository.CanEditInForm(path));

            var keycut = new Keycut { Name = "bom-ansi", FilePath = path, Target = "https://example.com" };
            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(
                () => Repo().UpdateAsync(keycut, new KeycutEdit("https://example.com", "bom-ansi", "new")));

            Assert.Equal(KeycutRepositoryError.NotEditable, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Theory]
        [InlineData("utf-16")]
        [InlineData("utf-16BE")]
        [InlineData("utf-32")]
        public void AFileInAnotherUnicodeEncoding_IsNotOffered_BecauseSavingWouldChangeItsEncoding(string encoding)
        {
            // cmd can't run these as scripts anyway, and a save would write UTF-8 over them
            var path = Path.Combine(_folder, "wide.bat");
            var text = "@ECHO OFF\r\nREM keycuts 1.3.0.0\r\nREM <shortcut>" + path + "</shortcut>\r\nREM <type>url</type>\r\n" +
                "REM <target>https://example.com</target>\r\nREM <description></description>\r\nSTART https://example.com\r\nEXIT\r\n";
            var enc = System.Text.Encoding.GetEncoding(encoding);
            File.WriteAllBytes(path, enc.GetPreamble().Concat(enc.GetBytes(text)).ToArray());

            Assert.False(KeycutRepository.CanEditInForm(path));
        }

        [Fact]
        public async Task AUtf8FileWithAccentsAndABom_IsOffered_AndKeepsItsAccents()
        {
            var path = Path.Combine(_folder, "accents.bat");
            var text = "@ECHO OFF\r\nREM keycuts 1.3.0.0\r\nREM <shortcut>" + path + "</shortcut>\r\nREM <type>url</type>\r\n" +
                "REM <target>https://example.com/caf\u00E9</target>\r\nREM <description>r\u00E9sum\u00E9</description>\r\n" +
                "START https://example.com/caf\u00E9\r\nEXIT\r\n";
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(true));

            Assert.True(KeycutRepository.CanEditInForm(path));

            var saved = await Repo().UpdateAsync(await LoadOne("accents"), new KeycutEdit("https://example.com/caf\u00E9", "accents", "r\u00E9sum\u00E9 2"));

            Assert.Equal("r\u00E9sum\u00E9 2", saved.Description);
            Assert.Equal("https://example.com/caf\u00E9", saved.Target);
            Assert.Contains("REM <description>r\u00E9sum\u00E9 2</description>", File.ReadAllLines(path));
        }

        #endregion Files that changed, and files that aren't UTF-8
    }
}
