using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace keycuts.Batmanager
{
    public class BatFormLogic
    {
        #region Fields

        private static readonly string alphanumeric = "A-Za-z0-9";
        private static readonly string alphanumericspecial = $"{alphanumeric}:\\.";
        private static readonly string alphanumericurl = $@"{alphanumeric}:/.";

        public static string URL = $"START [{alphanumericurl}]+";
        public static string FILE = $"START \"\" /B \"[{alphanumericspecial}]+";
        public static string FOLDER = $"\"%SystemRoot%\\explorer.exe\" \"[{alphanumericspecial}]+\"";
        public static string OPENWITHAPP = $"\"[{alphanumericspecial}]+\" \"[{alphanumericspecial}]+\"";

        public static List<BatParseArg> BatList = new List<BatParseArg>()
        {
            new BatParseArg(URL, ShortcutType.Url),
            new BatParseArg(FILE, ShortcutType.File),
            new BatParseArg(FOLDER, ShortcutType.Folder)
        };

        #endregion Fields

        #region Public Methods

        public static void PopulateDataGrid(DataGrid dataGrid)
        {
            var outputFolder = RegistryStuff.GetOutputFolder(@"C:\Shortcuts");
            var batFiles = Directory.GetFiles(outputFolder, "*.bat").ToList();
            var bats = ParseBats(batFiles);
            dataGrid.ItemsSource = bats;
        }

        public static List<Bat> ParseBats(List<string> batFiles)
        {
            var bats = new List<Bat>();

            foreach (var batFile in batFiles)
            {
                var lines = File.ReadAllLines(batFile)
                    .Where(x => x.Contains("\\explorer.exe") ||
                                x.StartsWith("START", StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

                if (lines.Any())
                {
                    var shortcut = Path.GetFileNameWithoutExtension(batFile);
                    var command = lines[0];
                    var destination = "";
                    var shortcutType = ShortcutType.Unknown;
                    var openWithApp = "";

                    if (command.Contains("\\explorer.exe"))
                    {
                        // It's a folder!
                        shortcutType = ShortcutType.Folder;
                    }
                    else if (command.Substring(0, 5).ToUpper() == "START")
                    {
                        // It's NOT a folder! -- Could be File, Url, HostsFile, or CLSIDKey


                        shortcutType = ShortcutType.File;
                    }

                    if (!string.IsNullOrEmpty(shortcut) && 
                        !string.IsNullOrEmpty(command))
                    {
                        bats.Add(new Bat(shortcut, command, destination, shortcutType, openWithApp));
                    }
                }
            }

            return bats;
        }

        #endregion Public Methods
    }
}
