using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class OutputFolderDefaultTests
    {
        private static XElement FindById(XDocument wxs, string elementName, string id)
        {
            return wxs.Descendants().Single(element =>
                element.Name.LocalName == elementName && (string)element.Attribute("Id") == id);
        }

        [Fact]
        public void Default_is_in_the_users_profile()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            Assert.Equal(Path.Combine(profile, "Shortcuts"), Runner.DefaultOutputFolder);
        }

        [Fact]
        public void Installer_defaults_to_the_same_folder_as_the_app()
        {
            var wxs = XDocument.Load(ContextMenuRegistrarTests.FindRepoFile(@"installer\Package.wxs"));
            var installerDefault = (string)FindById(wxs, "SetProperty", "OUTPUTFOLDER").Attribute("Value");
            var profile = Environment.GetEnvironmentVariable("USERPROFILE");

            Assert.Equal(Runner.DefaultOutputFolder, installerDefault.Replace("[%USERPROFILE]", profile));
        }

        [Fact]
        public void Installer_saves_the_folder_where_the_app_reads_it()
        {
            var wxs = XDocument.Load(ContextMenuRegistrarTests.FindRepoFile(@"installer\Package.wxs"));

            var saved = wxs.Descendants().Single(element =>
                element.Name.LocalName == "RegistryValue" && (string)element.Attribute("Name") == RegistryStuff.OutputFolderKeyName);

            Assert.Equal(RegistryStuff.StartPath, (string)saved.Attribute("Key"));
            Assert.Equal("[OUTPUTFOLDER]", (string)saved.Attribute("Value"));
        }

        [Theory]
        [InlineData(@"C:\Users\me\Shortcuts\", @"C:\Users\me\Shortcuts")]
        [InlineData(@"C:\Users\me\Shortcuts", @"C:\Users\me\Shortcuts")]
        [InlineData(@"C:\", @"C:\")]
        public void TrimFolder_drops_the_installers_trailing_backslash_but_keeps_a_drive_root(string folder, string expected)
        {
            Assert.Equal(expected, RegistryStuff.TrimFolder(folder));
        }
    }
}
