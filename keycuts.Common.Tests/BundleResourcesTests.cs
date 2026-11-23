using System.IO;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class BundleResourcesTests
    {
        private static readonly string Macos = Path.Combine(Path.GetTempPath(), "keycuts.app", "Contents", "MacOS");

        [MacFact]
        public void InABundle_TheFolderIsInResources()
        {
            var resources = Path.GetFullPath(Path.Combine(Macos, "..", "Resources", "QuickAction"));

            var folder = BundleResources.Folder(Macos, "QuickAction", path => path == resources);

            Assert.Equal(resources, folder);
        }

        [Fact]
        public void WithoutOne_TheFolderIsBesideTheExecutable()
        {
            var folder = BundleResources.Folder(Macos, "QuickAction", path => false);

            Assert.Equal(Path.Combine(Macos, "QuickAction"), folder);
        }
    }
}
