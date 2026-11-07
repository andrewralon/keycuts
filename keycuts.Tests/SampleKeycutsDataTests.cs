#if DEBUG
using System.Linq;
using keycuts.Common;
using keycuts.GUI.DesignData;
using Xunit;

namespace keycuts.Tests
{
    public class SampleKeycutsDataTests
    {
        [Fact]
        public void Canonical_entries_match_the_ticket()
        {
            var canonical = SampleKeycuts.Canonical();

            Assert.Equal(2, canonical.Count);
            Assert.Equal("l-vicki", canonical[0].Name);
            Assert.Equal(KeycutKind.Url, canonical[0].Kind);
            Assert.Equal("logs for vicki", canonical[0].Description);
            Assert.Equal("downloads", canonical[1].Name);
            Assert.Equal(KeycutKind.Folder, canonical[1].Kind);
            Assert.Equal("Downloads folder", canonical[1].Description);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(25)]
        [InlineData(100)]
        public void Generate_returns_the_requested_count_with_unique_names(int count)
        {
            var keycuts = SampleKeycuts.Generate(count);

            Assert.Equal(count, keycuts.Count);
            Assert.Equal(count, keycuts.Select(k => k.Name).Distinct().Count());
        }

        [Fact]
        public void Long_list_covers_every_kind_and_edge_case()
        {
            var keycuts = SampleKeycuts.Generate(25);

            foreach (var kind in new[] { KeycutKind.Url, KeycutKind.Folder, KeycutKind.File, KeycutKind.Command })
            {
                Assert.Contains(keycuts, k => k.Kind == kind);
            }

            Assert.Contains(keycuts, k => k.DestinationStatus == DestinationStatus.Missing);
            Assert.Contains(keycuts, k => k.Destination.Length > 200);
            Assert.Contains(keycuts, k => k.Name.Length > 40);
        }

        [Fact]
        public void Design_view_models_expose_the_three_manager_states()
        {
            Assert.Empty(DesignViewModels.ManagerEmpty.Keycuts);
            Assert.Equal(2, DesignViewModels.ManagerTwoItems.KeycutCount);
            Assert.Equal(25, DesignViewModels.ManagerLongList.Keycuts.Count);
        }

        [Fact]
        public void Design_creator_states_use_the_canonical_entries()
        {
            Assert.Equal(string.Empty, DesignViewModels.CreatorEmpty.Destination);
            Assert.Equal("l-vicki", DesignViewModels.CreatorUrl.ShortcutName);
            Assert.Equal("downloads", DesignViewModels.CreatorFolder.ShortcutName);
        }
    }
}
#endif
