#if DEBUG
using System;
using System.Collections.Generic;
using keycuts.Common;

namespace keycuts.GUI.DesignData
{
    /// <summary>
    /// Sample keycuts for the XAML designer, Hot Reload and tests. Debug builds only; never shipped in release.
    /// </summary>
    public static class SampleKeycuts
    {
        public const string VickiName = "l-vicki";

        public const string VickiDescription = "logs for vicki";

        public const string VickiDestination = "https://logs.example.com/vicki";

        public const string DownloadsName = "downloads";

        public const string DownloadsDescription = "Downloads folder";

        public const string DownloadsDestination = @"C:\Users\sample\Downloads";

        private const string SampleFolder = @"C:\Shortcuts";

        public static Keycut Vicki()
        {
            return Create(VickiName, KeycutKind.Url, VickiDescription, VickiDestination, DestinationStatus.Unknown);
        }

        public static Keycut Downloads()
        {
            return Create(DownloadsName, KeycutKind.Folder, DownloadsDescription, DownloadsDestination, DestinationStatus.Ok);
        }

        /// <summary>The two canonical entries.</summary>
        public static IReadOnlyList<Keycut> Canonical()
        {
            return new List<Keycut> { Vicki(), Downloads() };
        }

        /// <summary>
        /// The canonical entries first, then a deterministic mix of every kind with long names, long descriptions,
        /// very long destinations and a few missing files.
        /// </summary>
        public static IReadOnlyList<Keycut> Generate(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var keycuts = new List<Keycut>(count);
            var canonical = Canonical();

            for (var i = 0; i < count; i++)
            {
                keycuts.Add(i < canonical.Count ? canonical[i] : GenerateOne(i));
            }

            return keycuts;
        }

        private static Keycut GenerateOne(int index)
        {
            var number = index + 1;
            var name = $"sample-{number:000}";
            var description = $"Sample keycut {number}";
            var longDestinationTail = new string('x', 210);

            switch (index % 6)
            {
                case 0:
                    return Create(name, KeycutKind.Url, description, $"https://example.com/items/{number}", DestinationStatus.Unknown);
                case 1:
                    return Create(name, KeycutKind.Folder, description, $@"C:\Sample\Folder{number}", DestinationStatus.Ok);
                case 2:
                    return Create(name, KeycutKind.File, description, $@"C:\Sample\File{number}.txt", DestinationStatus.Ok);
                case 3:
                    return Create(name, KeycutKind.File, description, $@"C:\Sample\Missing{number}.txt", DestinationStatus.Missing);
                case 4:
                    return Create(name, KeycutKind.Command, description, "\"C:\\Windows\\System32\\cmd.exe\" /k echo hello", DestinationStatus.Unknown);
                default:
                    return Create(
                        $"a-very-long-keycut-name-for-layout-testing-{number}",
                        KeycutKind.Folder,
                        $"A deliberately long description for keycut {number} that should be truncated or wrapped instead of breaking the row layout",
                        $@"C:\Sample\{longDestinationTail}\{number}",
                        DestinationStatus.Ok);
            }
        }

        private static Keycut Create(string name, KeycutKind kind, string description, string destination, DestinationStatus status)
        {
            return new Keycut
            {
                Name = name,
                Kind = kind,
                RawType = ToRawType(kind),
                Description = description,
                Destination = destination,
                FilePath = $@"{SampleFolder}\{name}.bat",
                DestinationStatus = status
            };
        }

        private static ShortcutType ToRawType(KeycutKind kind)
        {
            switch (kind)
            {
                case KeycutKind.Url:
                    return ShortcutType.Url;
                case KeycutKind.Folder:
                    return ShortcutType.Folder;
                case KeycutKind.File:
                    return ShortcutType.File;
                default:
                    return ShortcutType.Command;
            }
        }
    }
}
#endif
