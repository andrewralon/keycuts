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

        private static string alphanumeric = "A-Za-z0-9";
        private static string alphanumericspecial = $"{alphanumeric}:\\.";
        private static string alphanumericurl = $@"{alphanumeric}:/.";

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
            var bats = new List<Bat>();

            var outputFolder = RegistryStuff.GetOutputFolder(@"C:\Shortcuts");
            var shortcuts = Directory.GetFiles(outputFolder, "*.bat").ToList();

            foreach (var shortcut in shortcuts)
            {
                var lines = File.ReadAllLines(shortcut)
                    .Where(x => !string.IsNullOrEmpty(x) &&
                        (x.StartsWith("START", StringComparison.CurrentCultureIgnoreCase) || x.Contains("\\explorer.exe")))
                    .ToList();

                var bat = Parse(shortcut, lines);

                if (bat != null)
                {
                    bats.Add(bat);
                }
            }

            dataGrid.ItemsSource = bats;
        }

        public static Bat Parse(string batFile, List<string> lines)
        {
            var bat = new Bat();

            if (lines.Any())
            {
                bat.Shortcut = Path.GetFileNameWithoutExtension(batFile);
                bat.Command = lines[0];

                if (lines[0].Contains("\\explorer.exe"))
                {
                    // It's a folder!
                    bat.ShortcutType = ShortcutType.Folder;
                }
                else if (lines[0].Substring(0, 5).ToUpper() == "START")
                {
                    // It's NOT a folder! -- Could be File, Url, HostsFile, or CLSIDKey


                    bat.ShortcutType = ShortcutType.File;
                }
            }

            if (string.IsNullOrEmpty(bat.Command) ||
                (string.IsNullOrEmpty(bat.Shortcut) && string.IsNullOrEmpty(bat.Destination)))
            {
                bat = null;
            }

            return bat;
        }

        #endregion Public Methods
    }
}
