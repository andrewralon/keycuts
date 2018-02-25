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

        public string Name { get; private set; }

        public ShortcutType Type { get; private set; } = ShortcutType.Unknown;

        public Shortcut()
        {
        }

        public Shortcut(string destination, string name)
        {
            Destination = destination;
            Name = name;

            GetShortcutType();
        }

        private void GetShortcutType()
        {
            // TODO Check if the destination is a URL shortcut file - .url

            if (IsValidUrl())
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
            bool result = Uri.TryCreate(Destination, UriKind.Absolute, out uriResult);

            if (!result)
            {
                // Check for an incomplete Uri without the scheme
                //  Complete:   https://www.amazon.com
                //  Incomplete: amazon.com

                var newUri = new UriBuilder(Destination);
                if (uriResult != null)
                {
                    // Fix the incomplete Uri
                    Destination = newUri.Uri.AbsoluteUri;
                    result = true;
                }
            }

            return result;
        }
    }
}
