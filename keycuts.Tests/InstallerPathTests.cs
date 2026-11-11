using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// What the MSI puts on PATH: the keycuts folder and the install folder's bin, which holds only the CLI's shims.
    /// Never the install folder itself, which would put the self-contained runtime's DLLs and createdump.exe on PATH.
    /// </summary>
    public class InstallerPathTests
    {
        [Fact]
        public void Path_gets_the_output_folder_and_bin_but_not_the_install_folder()
        {
            var wxs = XDocument.Load(ContextMenuRegistrarTests.FindRepoFile(@"installer\Package.wxs"));

            var pathValues = wxs.Descendants()
                .Where(element => element.Name.LocalName == "Environment" && (string)element.Attribute("Name") == "PATH")
                .Select(element => (string)element.Attribute("Value"))
                .OrderBy(value => value);

            Assert.Equal(new[] { "[BINFOLDER]", "[OUTPUTFOLDER]" }, pathValues);
        }

        [Fact]
        public void Bin_is_a_folder_in_the_install_folder()
        {
            var wxs = XDocument.Load(ContextMenuRegistrarTests.FindRepoFile(@"installer\Package.wxs"));

            var bin = wxs.Descendants().Single(element =>
                element.Name.LocalName == "Directory" && (string)element.Attribute("Id") == "BINFOLDER");

            Assert.Equal("bin", (string)bin.Attribute("Name"));
            Assert.Equal("INSTALLFOLDER", (string)bin.Parent.Attribute("Id"));
        }

        [Theory]
        [InlineData("keycuts.cmd", @"@""%~dp0..\keycuts.exe"" %*")]
        [InlineData("keypaste.cmd", @"@""%~dp0..\keycuts.exe"" paste %*")]
        public void Shim_runs_keycuts_exe_one_folder_up_and_passes_its_arguments(string shim, string expectedLaunchLine)
        {
            var text = File.ReadAllText(ContextMenuRegistrarTests.FindRepoFile(Path.Combine("keycuts.CLI", "shims", shim)));

            var lines = text.Split("\r\n");

            Assert.Equal(expectedLaunchLine, lines.Single(line => !line.StartsWith("@REM") && line.Length > 0));
            Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
        }
    }
}
