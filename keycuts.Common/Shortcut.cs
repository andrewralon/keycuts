using Shell32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public class Shortcut
    {
        #region Properties

        public string Destination { get; private set; }

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

        public Shortcut(string destination, string shortcut, string defaultFolder, string openWithAppPath = null)
        {
            Type = GetShortcutType(destination, out string newDestination);
            Destination = newDestination;
            Extension = ".bat";
            Filename = Path.GetFileNameWithoutExtension(shortcut);
            FilenameWithExtension = $"{Filename}{Extension}";
            Folder = IsNotFullPath(shortcut) ?
                defaultFolder :
                Path.GetDirectoryName(shortcut);
            FullPath = Path.Combine(Folder, FilenameWithExtension);
            OpenWithAppPath = openWithAppPath;
            OpenWithApp = openWithAppPath != null && File.Exists(openWithAppPath);
        }

        #endregion Constructors

        #region Private Methods

        #endregion Private Methods

        #region Public Methods

        public static ShortcutType GetShortcutType(string destination, out string newDestination)
        {
            newDestination = destination;
            var type = new ShortcutType();

            if (IsCLSIDKey(destination))
            {
                type = ShortcutType.CLSIDKey;
            }
            else if (IsValidUrl(destination, out string url1))
            {
                newDestination = url1;
                type = ShortcutType.Url;
            }
            else if (IsValidUrlFile(destination, out string url2))
            {
                newDestination = url2;
                type = ShortcutType.Url;
            }
            else
            {
                destination = Path.GetFullPath(destination);
                newDestination = destination;

                var attributes = File.GetAttributes(destination);

                if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    type = ShortcutType.Folder;
                }
                else if (File.Exists(destination))
                {
                    if (destination.ToLower() == @"C:\Windows\System32\drivers\etc\hosts".ToLower() ||
                        destination.ToLower() == @"%windir%\System32\drivers\etc\hosts".ToLower())
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

        public static bool IsValidUrl(string destination, out string url)
        {
            url = destination;
            var result = Uri.TryCreate(destination, UriKind.Absolute, out Uri uriResult)
                && !uriResult.IsFile;

            // Check for an incomplete Uri without the scheme
            //  Complete:   http://www.amazon.com
            //  Incomplete: amazon.com
            if (!result)
            {
                var newUri = new UriBuilder(destination);
                if (uriResult != null && !uriResult.IsFile)
                {
                    // Fix the incomplete Uri
                    url = newUri.Uri.AbsoluteUri;
                    result = true;
                }
            }

            return result;
        }

        public static bool IsValidUrlFile(string destination, out string url)
        {
            var result = false;
            url = destination;

            if (Path.GetExtension(destination).EndsWith(".url", StringComparison.InvariantCultureIgnoreCase))
            {
                // Check for a line starting with "URL="
                var line = File.ReadAllLines(destination)
                    .ToList()
                    .FirstOrDefault(a => a.StartsWith("URL=", StringComparison.InvariantCultureIgnoreCase));

                // Update the destination to use the URL from the link
                if (line != null)
                {
                    url = line.Substring(4);
                    if (url != null)
                    {
                        destination = url;
                        result = true;
                    }
                }
            }

            return result;
        }

        public static bool IsNotFullPath(string path)
        {
            var result = false;
            if (path != Path.GetFullPath(path))
            {
                result = true;
            }
            return result;
        }

        public static bool IsLink(string destination, out string link)
        {
            var result = false;

            // Handle relative paths
            destination = Path.GetFullPath(destination);
            link = destination;

            // Code found here: http://stackoverflow.com/questions/310595/how-can-i-test-programmatically-if-a-path-file-is-a-shortcut
            var path = Environment.ExpandEnvironmentVariables(Path.GetDirectoryName(destination));
            var file = Path.GetFileName(destination);

            var shell = new Shell();
            var folder = shell.NameSpace(path);
            var folderItem = folder.ParseName(file);

            if (folderItem != null && folderItem.IsLink)
            {
                var linkObject = (ShellLinkObject)folderItem.GetLink;
                link = linkObject.Path;
                result = true;
            }

            return result;
        }

        public static string SanitizeBatEscapeCharacters(string command)
        {
            // Bat files use % for variables, so escape a single % with %%
            command = Regex.Replace(command, "(?<!%)%(?!%)", "%%");
            return command;
        }

        public static bool IsCLSIDKey(string guid)
        {
            var result = false;

            if ((guid.StartsWith("{") || guid.StartsWith("::{") || guid.StartsWith("shell:::{")) &&
                guid.EndsWith("}"))
            {
                result = true;
            }

            return result;
        }

        public static string GetCLSIDKeyFullString(string guid)
        {
            var result = guid;

            if (guid.StartsWith("{"))
            {
                result = $"shell:::{guid}";
            }
            else if (guid.StartsWith("::{"))
            {
                result = $"shell:{guid}";
            }

            return result;
        }

        #endregion Public Methods
    }
}
