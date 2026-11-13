using System;
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
    /// Changing a keycut's description in the Manager's table (#202) rewrites one line, as renaming does: the file isn't
    /// emitted again, so nothing else in it can be lost, and a file the Edit form refuses can still have a description.
    /// </summary>
    public class KeycutDescriptionTests : IDisposable
    {
        private readonly string _root;
        private readonly string _folder;

        public KeycutDescriptionTests()
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

        private string Emit(string name, ShortcutType type, string target, string description = "")
        {
            var path = Path.Combine(_folder, name + ".bat");
            var shortcut = Shortcut.FromParts(type, target, path);
            File.WriteAllLines(path, Runner.BuildShortcutLines(shortcut, description));
            return path;
        }

        private string WriteBytes(string name, string text, Encoding encoding = null)
        {
            var path = Path.Combine(_folder, name + ".bat");
            File.WriteAllBytes(path, (encoding ?? new UTF8Encoding(false)).GetBytes(text));
            return path;
        }

        private async Task<Keycut> LoadOne(string name)
        {
            return (await Repo().LoadAllAsync()).Single(k => k.Name == name);
        }

        private string[] Everything()
        {
            return Directory.GetFileSystemEntries(_folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        private static readonly DateTime LongAgo = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Local);

        #region A line that is there

        [Fact]
        public async Task ChangesOnlyTheDescriptionLine_AndTheDate()
        {
            var path = Emit("gmail", ShortcutType.Url, "https://mail.google.com", "Old words");
            File.SetLastWriteTime(path, LongAgo);
            var before = File.ReadAllLines(path);

            var saved = await Repo().SetDescriptionAsync(await LoadOne("gmail"), "Work email");

            var after = File.ReadAllLines(path);
            Assert.Equal("Work email", saved.Description);
            Assert.Equal(path, saved.FilePath);
            Assert.Equal(before.Select(l => l.StartsWith("REM <description>") ? "REM <description>Work email</description>" : l), after);
            Assert.True(saved.Modified > LongAgo.AddYears(1), "the file's date should show the change");
            Assert.True(saved.CanEditInForm, "an emitted file is still one the form can edit");
            Assert.Equal(new[] { "gmail.bat" }, Everything());
        }

        // The point of one-line edits: nothing the file holds can be lost, whether or not the Edit form would take it
        [Fact]
        public async Task ALaunchLineEditedByHand_SurvivesAndStaysUneditableInTheForm()
        {
            var path = Path.Combine(_folder, "kp.bat");
            var lines = new[]
            {
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\kp.bat</shortcut>", "REM <type>file</type>",
                @"REM <target>C:\Tools\app.exe</target>", "REM <description>old</description>",
                "START \"\" /B \"C:\\Tools\\app.exe\" %*", "EXIT"
            };
            File.WriteAllLines(path, lines);

            var saved = await Repo().SetDescriptionAsync(await LoadOne("kp"), "Pass arguments through");

            Assert.Equal("Pass arguments through", saved.Description);
            Assert.Equal(lines.Select(l => l.StartsWith("REM <description>") ? "REM <description>Pass arguments through</description>" : l), File.ReadAllLines(path));
            Assert.False(saved.CanEditInForm);
        }

        [Theory]
        [InlineData("Opens %~dp0 and %~f1")]
        [InlineData("100% done")]
        [InlineData("%1 %* %PATH:~0,3%")]
        [InlineData("a & b | c < d > e ^ f")]
        [InlineData("ends with a caret ^")]
        public async Task TheTextIsEscapedAsTheCreatorDoesIt_AndReadsBackAsTyped_AndDoesNotStopTheScript(string description)
        {
            // A launch line that does nothing, then an ECHO that shows the script ran to the end
            var path = WriteBytes("echo", string.Join("\r\n", new[]
            {
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\echo.bat</shortcut>", "REM <type>file</type>",
                @"REM <target>C:\Windows\System32\cmd.exe</target>", "REM <description>x</description>",
                "START \"\" /B \"C:\\Windows\\System32\\cmd.exe\" /C exit", "ECHO ran", ""
            }));

            var saved = await Repo().SetDescriptionAsync(await LoadOne("echo"), description);

            Assert.Equal(description, saved.Description);
            Assert.Equal("ran", RunScript(path).Trim());
        }

        [Fact]
        public async Task APercentTilde_IsDoubledOnTheLine()
        {
            var path = Emit("pct", ShortcutType.Url, "https://example.com");

            await Repo().SetDescriptionAsync(await LoadOne("pct"), "Opens %~dp0");

            Assert.Contains("REM <description>Opens %%~dp0</description>", File.ReadAllLines(path));
        }

        // One line: a pasted newline becomes a space, as in the Creator
        [Fact]
        public async Task ANewlineInTheText_BecomesASpace()
        {
            var path = Emit("lines", ShortcutType.Url, "https://example.com");

            var saved = await Repo().SetDescriptionAsync(await LoadOne("lines"), "first\r\nsecond\nthird");

            // Each break is one space, so \r\n is two, which is what the Creator writes too
            Assert.Equal("first  second third", saved.Description);
            Assert.Single(File.ReadAllLines(path), l => l.StartsWith("REM <description>"));
            Assert.Contains("REM <description>first  second third</description>", File.ReadAllLines(path));
        }

        [Fact]
        public async Task AnEmptyDescription_LeavesTheEmptyLineTheEmitterWrites()
        {
            var path = Emit("clear", ShortcutType.Url, "https://example.com", "Something");

            var saved = await Repo().SetDescriptionAsync(await LoadOne("clear"), "   ");

            Assert.Equal("", saved.Description);
            Assert.Contains("REM <description></description>", File.ReadAllLines(path));
        }

        [Fact]
        public async Task TheSameDescription_LeavesTheFileAlone_BytesAndDate()
        {
            var path = Emit("same", ShortcutType.Url, "https://example.com", "Kept");
            File.SetLastWriteTime(path, LongAgo);
            var bytes = File.ReadAllBytes(path);

            var saved = await Repo().SetDescriptionAsync(await LoadOne("same"), "  Kept  ");

            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(LongAgo, File.GetLastWriteTime(path));
            Assert.Equal("Kept", saved.Description);
            Assert.Equal(LongAgo, saved.Modified);
        }

        #endregion A line that is there

        #region Keeping the file's own bytes

        [Theory]
        [InlineData("\r\n")]
        [InlineData("\n")]
        public async Task TheFilesLineBreaksStay(string lineBreak)
        {
            var path = WriteBytes("breaks", string.Join(lineBreak, new[]
            {
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\breaks.bat</shortcut>", "REM <type>url</type>",
                "REM <target>https://example.com</target>", "REM <description>old</description>", "START https://example.com", "EXIT", ""
            }));

            await Repo().SetDescriptionAsync(await LoadOne("breaks"), "new");

            var text = File.ReadAllText(path);
            Assert.Contains("REM <description>new</description>" + lineBreak, text);

            if (lineBreak == "\n")
            {
                Assert.DoesNotContain("\r", text);
            }
            else
            {
                // Eight lines, each ended by \r\n and by nothing else
                Assert.Equal(8, text.Split("\r\n").Length - 1);
                Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
            }
        }

        [Fact]
        public async Task AUtf8ByteOrderMark_AndNonAsciiText_AreKept()
        {
            var path = WriteBytes("bom", string.Join("\r\n", new[]
            {
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\bom.bat</shortcut>", "REM <type>url</type>",
                "REM <target>https://example.com/résumé</target>", "REM <description>old</description>", "START https://example.com/résumé", "EXIT", ""
            }));
            File.WriteAllBytes(path, Encoding.UTF8.GetPreamble().Concat(File.ReadAllBytes(path)).ToArray());

            var saved = await Repo().SetDescriptionAsync(await LoadOne("bom"), "Résumé ✓");

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.False(bytes.AsSpan(3).StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.Equal("Résumé ✓", saved.Description);
            Assert.Equal("https://example.com/résumé", saved.Target);
        }

        // The "downloaded from the internet" mark and the file's permissions come with File.Replace
        [Fact]
        public async Task TheDownloadedMark_Survives()
        {
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

            await Repo().SetDescriptionAsync(await LoadOne("marked"), "changed");

            Assert.Equal(mark, File.ReadAllText(path + ":Zone.Identifier"));
            Assert.Equal(new[] { "marked.bat" }, Everything());
        }

        [Fact]
        public async Task AFileThatIsNotUtf8_IsRefused_AndNotTouched()
        {
            var lines = new[]
            {
                "@ECHO OFF", "REM keycuts 1.3.0.0", @"REM <shortcut>C:\Shortcuts\cafe.bat</shortcut>", "REM <type>url</type>",
                "REM <target>https://example.com</target>", "REM <description>caf\u00E9</description>", "START https://example.com", "EXIT", ""
            };
            var path = WriteBytes("cafe", string.Join("\r\n", lines), Encoding.Latin1);
            var bytes = File.ReadAllBytes(path);
            var keycut = await LoadOne("cafe");

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().SetDescriptionAsync(keycut, "plain"));

            Assert.Equal(KeycutRepositoryError.NotEditable, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(new[] { "cafe.bat" }, Everything());
        }

        #endregion Keeping the file's own bytes

        #region A line that isn't there

        // keycuts 1.0 wrote no description line: it goes where the emitter puts it, just before the launch line
        [Fact]
        public async Task AKeycutFromBeforeDescriptions_GetsALineBeforeTheLaunchLine()
        {
            var path = WriteBytes("v10", string.Join("\r\n", new[]
            {
                "@ECHO OFF", "REM keycuts 1.0.0.0", @"REM <shortcut>C:\Shortcuts\v10.bat</shortcut>", "REM <type>url</type>",
                "REM <destination>https://toolbox.googleapps.com/apps/dig/</destination>", "START https://toolbox.googleapps.com/apps/dig/", "EXIT", ""
            }));

            var saved = await Repo().SetDescriptionAsync(await LoadOne("v10"), "DNS lookups");

            Assert.Equal(
                new[]
                {
                    "@ECHO OFF", "REM keycuts 1.0.0.0", @"REM <shortcut>C:\Shortcuts\v10.bat</shortcut>", "REM <type>url</type>",
                    "REM <destination>https://toolbox.googleapps.com/apps/dig/</destination>", "REM <description>DNS lookups</description>",
                    "START https://toolbox.googleapps.com/apps/dig/", "EXIT"
                },
                File.ReadAllLines(path));
            Assert.Equal("DNS lookups", saved.Description);
            Assert.Equal("https://toolbox.googleapps.com/apps/dig/", saved.Target);
            Assert.True(File.ReadAllText(path).Contains("\r\n") && !File.ReadAllText(path).Replace("\r\n", "").Contains("\n"), "the file's CRLF line breaks are kept");
        }

        // The 0.1.x format, with the type as a tag name, and a launcher written by hand with no REM lines at all
        [Theory]
        [InlineData("REM keycuts 0.1.1.5\r\nREM <url>C:\\Shortcuts\\old.bat</url>\r\nSTART https://mail.google.com/mail/u/0/#inbox\r\nEXIT\r\n")]
        [InlineData("@echo OFF\r\nSTART \"\" /D \"C:\\Program Files\\Google\\Chrome\\Application\" chrome.exe --profile-directory=\"Default\"\r\nexit")]
        [InlineData("@ECHO OFF\r\n\"%SystemRoot%\\explorer.exe\" \"C:\\Users\"\r\nEXIT\r\n")]
        public async Task AFileWithNoRemLines_GetsALineJustBeforeItsLaunchLine(string content)
        {
            var path = WriteBytes("old", content);
            var before = File.ReadAllLines(path);
            var launch = Array.FindIndex(before, ShortcutFile.IsLaunchLine);

            var saved = await Repo().SetDescriptionAsync(await LoadOne("old"), "Added later");

            var after = File.ReadAllLines(path);
            Assert.Equal("Added later", saved.Description);
            Assert.Equal(before.Length + 1, after.Length);
            Assert.Equal("REM <description>Added later</description>", after[launch]);
            Assert.Equal(before.Take(launch).Concat(new[] { "REM <description>Added later</description>" }).Concat(before.Skip(launch)), after);
        }

        [Fact]
        public async Task NothingToSay_AddsNoLine_AndLeavesTheFileAlone()
        {
            var path = WriteBytes("v10", "@ECHO OFF\r\nREM keycuts 1.0.0.0\r\nREM <type>url</type>\r\nREM <destination>https://example.com</destination>\r\nSTART https://example.com\r\nEXIT\r\n");
            File.SetLastWriteTime(path, LongAgo);
            var bytes = File.ReadAllBytes(path);

            var saved = await Repo().SetDescriptionAsync(await LoadOne("v10"), "  ");

            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(LongAgo, File.GetLastWriteTime(path));
            Assert.Equal("", saved.Description);
        }

        #endregion A line that isn't there

        #region Refusals

        [Fact]
        public async Task AFileChangedSinceItLoaded_IsRefused_RatherThanOverwritingTheNewDescription()
        {
            var path = Emit("moved", ShortcutType.Url, "https://example.com", "original");
            var loaded = await LoadOne("moved");

            // Notepad, or another window, saves a different description meanwhile
            Emit("moved", ShortcutType.Url, "https://example.com", "changed elsewhere");
            var bytes = File.ReadAllBytes(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().SetDescriptionAsync(loaded, "mine"));

            Assert.Equal(KeycutRepositoryError.Changed, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Fact]
        public async Task AFileThatIsGone_SaysNotFound()
        {
            var path = Emit("gone", ShortcutType.Url, "https://example.com");
            var loaded = await LoadOne("gone");
            File.Delete(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().SetDescriptionAsync(loaded, "x"));

            Assert.Equal(KeycutRepositoryError.NotFound, ex.Error);
            Assert.Empty(Everything());
        }

        [Fact]
        public async Task AReadOnlyFile_IsRefusedWithAccessDenied_AndKeepsItsContent()
        {
            var path = Emit("locked", ShortcutType.Url, "https://example.com", "kept");
            var loaded = await LoadOne("locked");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            var bytes = File.ReadAllBytes(path);

            var ex = await Assert.ThrowsAsync<KeycutRepositoryException>(() => Repo().SetDescriptionAsync(loaded, "new"));

            Assert.Equal(KeycutRepositoryError.AccessDenied, ex.Error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(new[] { "locked.bat" }, Everything());
        }

        #endregion Refusals

        /// <summary>Runs the .bat with the real cmd and returns what it printed.</summary>
        private static string RunScript(string path)
        {
            var start = new ProcessStartInfo("cmd.exe", "/d /c \"\"" + path + "\"\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(start))
            {
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();

                Assert.True(process.WaitForExit(15000), "cmd didn't finish");

                return output.Result + error.Result;
            }
        }
    }
}
