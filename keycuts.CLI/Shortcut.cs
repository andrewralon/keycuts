using Shell32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace keycuts.CLI
{
    public class Shortcut
    {
        #region Properties

        public string Destination { get; private set; }

        public string DestinationFilename { get; private set; }

        public string DestinationFolder { get; private set; }

        public string Extension { get; private set; } = ".bat";

        public string Filename { get; private set; }

        public string FilenameWithExtension { get; private set; }

        public string Folder { get; private set; }

        public string FullPath { get; private set; }

        public string OpenWithAppPath { get; private set; }

        public bool OpenWithApp { get; private set; }

        public ShortcutType Type { get; private set; } = ShortcutType.Unknown;

        public string ShortcutsFolder { get; private set; }

        #endregion Properties

        #region Constructors

        public Shortcut(string destination, string path, string defaultFolder, string openWithAppPath = null)
        {
            Destination = GetWindowsLinkTargetPath(destination);
            DestinationFolder = Path.GetDirectoryName(Destination);
            DestinationFilename = Path.GetFileName(Destination);
            Extension = ".bat";
            Filename = Path.GetFileNameWithoutExtension(path);
            FilenameWithExtension = string.Format("{0}{1}", Filename, Extension);
            Folder = IsNotFullPath(path) ?
                RegistryKey.GetDefaultShortcutsFolder(defaultFolder) :
                Path.GetDirectoryName(path);
            FullPath = Path.Combine(Folder, string.Format("{0}{1}", Filename, Extension));
            OpenWithAppPath = openWithAppPath;
            OpenWithApp = openWithAppPath != null && File.Exists(openWithAppPath);
            Type = GetShortcutType();
        }

        #endregion Constructors

        #region Private Methods

        private ShortcutType GetShortcutType()
        {
            var type = new ShortcutType();

            if (IsValidUrl() || IsValidUrlFile())
            {
                type = ShortcutType.Url;
            }
            else
            {
                var attributes = File.GetAttributes(Destination);

                if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    type = ShortcutType.Folder;
                }
                else if (File.Exists(Destination))
                {
                    if (Destination.ToLower() == @"C:\Windows\System32\drivers\etc\hosts".ToLower() ||
                        Destination.ToLower() == @"%windir%\System32\drivers\etc\hosts".ToLower())
                    {
                        type = ShortcutType.HostsFile;
                    }
                    else
                    {
                        type = ShortcutType.File;
                    }
                }
                else
                {
                    type = ShortcutType.Unknown;
                }
            }

            return type;
        }

        private bool IsValidUrl()
        {
            var result = Uri.TryCreate(Destination, UriKind.Absolute, out Uri uriResult)
                && !uriResult.IsFile;

            // Check for an incomplete Uri without the scheme
            //  Complete:   http://www.amazon.com
            //  Incomplete: amazon.com
            if (!result)
            {
                var newUri = new UriBuilder(Destination);
                if (uriResult != null && !uriResult.IsFile)
                {
                    // Fix the incomplete Uri
                    Destination = newUri.Uri.AbsoluteUri;
                    result = true;
                }
            }

            return result;
        }

        private bool IsValidUrlFile()
        {
            var result = false;

            if (Path.GetExtension(Destination).EndsWith(".url", StringComparison.InvariantCultureIgnoreCase))
            {
                // Check for a line starting with "URL="
                var line = File.ReadAllLines(Destination)
                    .ToList()
                    .FirstOrDefault(a => a.StartsWith("URL=", StringComparison.InvariantCultureIgnoreCase));

                // Update the destination to use the URL from the link
                if (line != null)
                {
                    var url = line.Substring(4);
                    if (url != null)
                    {
                        Destination = url;
                        result = true;
                    }
                }
            }

            return result;
        }

        #endregion Private Methods

        #region Public Methods

        public static bool IsNotFullPath(string path)
        {
            var result = false;

            var filename = Path.GetFileNameWithoutExtension(path);
            var filenamewithextension = Path.GetFileName(path);

            if (path == filename || path == filenamewithextension)
            {
                result = true;
            }

            return result;
        }

        public static string GetWindowsLinkTargetPath(string shortcutFilename)
        {
            var result = shortcutFilename;

            // Code found here: http://stackoverflow.com/questions/310595/how-can-i-test-programmatically-if-a-path-file-is-a-shortcut
            var path = Environment.ExpandEnvironmentVariables(Path.GetDirectoryName(shortcutFilename));
            var file = Path.GetFileName(shortcutFilename);

            var shell = new Shell();
            var folder = shell.NameSpace(path);
            var folderItem = folder.ParseName(file);

            if (folderItem != null && folderItem.IsLink)
            {
                var link = (ShellLinkObject)folderItem.GetLink;
                result = link.Path;
            }

            return result;
        }

        public static string SanitizeBatEscapeCharacters(string command)
        {
            // Bat files use % for variables, so escape a single % with %%
            command = Regex.Replace(command, "(?<!%)%(?!%)", "%%");
            return command;
        }

        #endregion Public Methods
    }
}
