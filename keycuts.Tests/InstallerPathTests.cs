using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// What the MSI puts on PATH (#248): the keycuts folder only, which also holds the keycuts.cmd and keypaste.cmd shims. Never
    /// the install folder itself, which would put the self-contained runtime's DLLs and createdump.exe on PATH.
    /// </summary>
    public class InstallerPathTests
    {
        private static XDocument Wxs()
        {
            return XDocument.Load(ContextMenuRegistrarTests.FindRepoFile(@"installer\Package.wxs"));
        }

        [Fact]
        public void Path_gets_only_the_output_folder()
        {
            var pathValues = Wxs().Descendants()
                .Where(element => element.Name.LocalName == "Environment" && (string)element.Attribute("Name") == "PATH")
                .Select(element => (string)element.Attribute("Value"));

            Assert.Equal(new[] { "[OUTPUTFOLDER]" }, pathValues);
        }

        [Fact]
        public void The_shims_are_installed_into_the_output_folder_and_nowhere_else()
        {
            var wxs = Wxs();

            Assert.DoesNotContain(wxs.Descendants(), element => (string)element.Attribute("Id") == "BINFOLDER");

            var shims = wxs.Descendants().Single(element => element.Name.LocalName == "ComponentGroup" && (string)element.Attribute("Id") == "ShimFiles");
            Assert.Equal("OUTPUTFOLDER", (string)shims.Attribute("Directory"));
            Assert.Equal(new[] { @"$(PublishDir)\shims\keycuts.cmd", @"$(PublishDir)\shims\keypaste.cmd" },
                shims.Descendants().Where(e => e.Name.LocalName == "File").Select(e => (string)e.Attribute("Source")));

            var app = wxs.Descendants().Single(element => element.Name.LocalName == "ComponentGroup" && (string)element.Attribute("Id") == "AppFiles");
            Assert.Contains(app.Descendants(), e => e.Name.LocalName == "Exclude" && (string)e.Attribute("Files") == @"$(PublishDir)\shims\**");
        }

        [Fact]
        public void The_installer_records_the_install_folder_the_shims_read()
        {
            var value = Wxs().Descendants().Single(element => element.Name.LocalName == "RegistryValue" && (string)element.Attribute("Name") == "InstallFolder");

            Assert.Equal(@"Software\TeamRalon\keycuts", (string)value.Attribute("Key"));
            Assert.Equal("HKCU", (string)value.Attribute("Root"));
            Assert.Contains(@"""HKCU\Software\TeamRalon\keycuts"" /v InstallFolder", CommandShims.Content("keycuts"));
        }

        [Fact]
        public void The_project_publishes_the_shims_into_a_shims_folder()
        {
            var project = File.ReadAllText(ContextMenuRegistrarTests.FindRepoFile(Path.Combine("keycuts.CLI", "keycuts.CLI.csproj")));

            Assert.Contains(@"Link=""shims\%(Filename)%(Extension)""", project);
        }

        // The MSI ships the files and the app writes CommandShims' bytes: the two must be the same, so the app never "fixes" a fresh install
        [Theory]
        [InlineData("keycuts")]
        [InlineData("keypaste")]
        public void The_shim_files_are_exactly_what_the_app_writes_with_crlf_and_no_bom(string command)
        {
            var bytes = File.ReadAllBytes(ContextMenuRegistrarTests.FindRepoFile(Path.Combine("keycuts.CLI", "shims", command + ".cmd")));

            Assert.Equal(CommandShims.Bytes(command), bytes);
            var text = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
            Assert.NotEqual(0xEF, bytes[0]);
        }

        [Theory]
        [InlineData("keycuts", @"@""%KEYCUTS_DIR%\keycuts.exe"" %*")]
        [InlineData("keypaste", @"@""%KEYCUTS_DIR%\keycuts.exe"" paste %*")]
        public void Shim_runs_the_installed_keycuts_exe_and_passes_its_arguments(string command, string launchLine)
        {
            var lines = CommandShims.Content(command).Split("\r\n");

            Assert.Equal(launchLine, lines.Last(line => line.Length > 0));
            Assert.Contains("@SETLOCAL", lines);   // KEYCUTS_DIR doesn't leak into the caller's cmd
        }
    }
}
