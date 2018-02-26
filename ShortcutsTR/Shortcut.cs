using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Shortcut
    {
        public string Destination { get; private set; }

        public string Path { get; private set; }

        public ShortcutType Type { get; private set; } = ShortcutType.Unknown;

        public Shortcut()
        {
        }

        public Shortcut(string destination, string path)
        {
            Destination = destination;
            Path = path;

            GetShortcutType();
        }

        private void GetShortcutType()
        {
            // TODO Check if the destination is a URL shortcut file - .url

            if (IsValidUrl() || IsValidUrlFile())
            {
                Type = ShortcutType.Url;
            }
            else
            {
                var attributes = File.GetAttributes(Destination);

                if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Type = ShortcutType.Folder;
                }
                else if (File.Exists(Destination))
                {
                    Type = ShortcutType.File;
                }
                else
                {
                    Type = ShortcutType.Unknown;
                }
            }
        }

        private bool IsValidUrl()
        {
            Uri uriResult;
            bool result = Uri.TryCreate(Destination, UriKind.Absolute, out uriResult) &&
                !uriResult.IsFile;

            if (!result)
            {
                // Check for an incomplete Uri without the scheme
                //  Complete:   https://www.amazon.com
                //  Incomplete: amazon.com

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
            bool result = false;

            if (System.IO.Path.GetExtension(Destination).ToLower() == ".url")
            {
                // Check for a line starting with "URL="
                var line = File.ReadAllLines(Destination)
                    .ToList()
                    .Where(a => a.Substring(0, 4).ToUpper() == "URL=")
                    .SingleOrDefault();

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
    }
}
