using System;
using System.IO;
using Xunit;

namespace keycuts.Tests.Characterization
{
    /// <summary>
    /// Pins what the legacy emitter and parser do today, bugs included (#88), and which corpus files survive a round
    /// trip (#89). A failure means behavior changed. If the change is intended, copy the .received.txt written next to
    /// the snapshot over the .verified.txt and commit it, so the diff shows up in review.
    /// </summary>
    public class CharacterizationTests
    {
        [Fact]
        public void Parser_MatchesSnapshot()
        {
            AssertMatchesSnapshot("Parser", CharacterizationRenderer.RenderParser(RepoRoot));
        }

        [Fact]
        public void Emitter_MatchesSnapshot()
        {
            AssertMatchesSnapshot("Emitter", CharacterizationRenderer.RenderEmitter());
        }

        [Fact]
        public void RoundTrip_MatchesSnapshot()
        {
            AssertMatchesSnapshot("RoundTrip", CharacterizationRenderer.RenderRoundTrip(RepoRoot));
        }

        [Fact]
        public void Corpus_IsFound()
        {
            // Guards against an empty snapshot passing because the corpus moved
            Assert.True(CharacterizationRenderer.ReadCorpus(RepoRoot).GetEnumerator().MoveNext());
        }

        #region Helpers

        private static readonly Lazy<string> repoRoot = new Lazy<string>(FindRepoRoot);

        private static string RepoRoot => repoRoot.Value;

        private static string SnapshotFolder => Path.Combine(RepoRoot, "keycuts.Tests", "Characterization", "Snapshots");

        private static string FindRepoRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);

            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "keycuts.sln")))
            {
                folder = folder.Parent;
            }

            return folder?.FullName ?? throw new InvalidOperationException("Can't find keycuts.sln above the test output folder.");
        }

        private static void AssertMatchesSnapshot(string name, string actual)
        {
            var verifiedPath = Path.Combine(SnapshotFolder, $"{name}.verified.txt");
            var receivedPath = Path.Combine(SnapshotFolder, $"{name}.received.txt");

            // Git may check the snapshot out with CRLF on Windows
            var expected = File.Exists(verifiedPath) ? File.ReadAllText(verifiedPath).Replace("\r\n", "\n") : "";

            if (expected == actual)
            {
                File.Delete(receivedPath);
                return;
            }

            File.WriteAllText(receivedPath, actual);

            Assert.Fail($"{name} output changed. Compare {receivedPath} with {verifiedPath}; if the change is intended, replace the verified file with the received one.\n\n{FirstDifference(expected, actual)}");
        }

        private static string FirstDifference(string expected, string actual)
        {
            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            var count = Math.Max(expectedLines.Length, actualLines.Length);

            for (var i = 0; i < count; i++)
            {
                var left = i < expectedLines.Length ? expectedLines[i] : "<end of file>";
                var right = i < actualLines.Length ? actualLines[i] : "<end of file>";

                if (left != right)
                {
                    return $"First difference at line {i + 1}:\n  verified: {left}\n  received: {right}";
                }
            }

            return "";
        }

        #endregion Helpers
    }
}
