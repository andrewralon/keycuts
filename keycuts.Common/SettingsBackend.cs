using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace keycuts.Common
{
    /// <summary>
    /// Where <see cref="RegistryStuff"/>'s settings live, as named values of three kinds: a string, a DWORD and a multi-string.
    /// The registry on Windows (<see cref="RegistrySettingsBackend"/>), a JSON file with the same value names on macOS (<see cref="JsonSettingsBackend"/>).
    /// </summary>
    public interface ISettingsBackend
    {
        /// <summary>A string, an int (DWORD) or a string[] (multi-string); null when it's missing or can't be read. Never writes.</summary>
        object Read(string name);

        void WriteString(string name, string value);

        void WriteDword(string name, int value);

        void WriteMultiString(string name, string[] value);

        /// <summary>Best effort: removes those of <paramref name="names"/> that exist, and never throws. A failure leaves them for the next call.</summary>
        void DeleteValues(IEnumerable<string> names);
    }

    /// <summary>
    /// The backend where there is none (Linux, which keycuts doesn't ship for): nothing is stored, so every setting reads as its default, and a write
    /// throws rather than pretend to save.
    /// </summary>
    public class UnavailableSettingsBackend : ISettingsBackend
    {
        public object Read(string name)
        {
            return null;
        }

        public void WriteString(string name, string value)
        {
            throw NotStored();
        }

        public void WriteDword(string name, int value)
        {
            throw NotStored();
        }

        public void WriteMultiString(string name, string[] value)
        {
            throw NotStored();
        }

        public void DeleteValues(IEnumerable<string> names)
        {
        }

        private static PlatformNotSupportedException NotStored()
        {
            return new PlatformNotSupportedException("Settings can't be saved on this platform yet.");
        }
    }

    /// <summary>
    /// The macOS backend: one JSON object in <see cref="DefaultPath"/>, named as the registry values are. A string is a JSON
    /// string, a DWORD a number and a multi-string an array of strings. Values it doesn't know are kept on a write. A missing
    /// or unreadable file reads as empty, and every write goes to a temp file first and is then moved over the old one, so
    /// a crash never leaves half a file.
    /// </summary>
    public class JsonSettingsBackend : ISettingsBackend
    {
        public static readonly string DefaultPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "keycuts", "settings.json");

        public JsonSettingsBackend(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Path { get; }

        public object Read(string name)
        {
            var node = Load()[name];

            if (node is JsonArray array)
            {
                return array.All(item => item is JsonValue value && value.GetValueKind() == JsonValueKind.String)
                    ? array.Select(item => item.GetValue<string>()).ToArray()
                    : null;
            }

            if (node is JsonValue scalar)
            {
                if (scalar.GetValueKind() == JsonValueKind.String)
                {
                    return scalar.GetValue<string>();
                }

                if (scalar.GetValueKind() == JsonValueKind.Number && scalar.TryGetValue(out int number))
                {
                    return number;
                }
            }

            return null;
        }

        public void WriteString(string name, string value)
        {
            Write(name, JsonValue.Create(value));
        }

        public void WriteDword(string name, int value)
        {
            Write(name, JsonValue.Create(value));
        }

        public void WriteMultiString(string name, string[] value)
        {
            Write(name, new JsonArray(value.Select(item => (JsonNode)JsonValue.Create(item)).ToArray()));
        }

        public void DeleteValues(IEnumerable<string> names)
        {
            try
            {
                var settings = Load();
                var removed = false;

                foreach (var name in names)
                {
                    removed |= settings.Remove(name);
                }

                if (removed)
                {
                    Save(settings);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Left for the next call
            }
        }

        private void Write(string name, JsonNode value)
        {
            var settings = Load();
            settings[name] = value;
            Save(settings);
        }

        private JsonObject Load()
        {
            try
            {
                return File.Exists(Path) && JsonNode.Parse(File.ReadAllText(Path)) is JsonObject settings ? settings : new JsonObject();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                return new JsonObject();
            }
        }

        private void Save(JsonObject settings)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));

            var temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(temp, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, Path, true);
            }
            finally
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>The settings key, <c>HKCU\Software\TeamRalon\keycuts</c>. The installer writes and reads some of its values too.</summary>
    [SupportedOSPlatform("windows")]
    public class RegistrySettingsBackend : ISettingsBackend
    {
        private static readonly RegistryKey CurrentUserContext = Registry.CurrentUser;

        public object Read(string name)
        {
            try
            {
                using var appNameKey = CurrentUserContext.OpenSubKey(RegistryStuff.StartPath);

                return appNameKey?.GetValue(name);
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return null;
            }
        }

        public void WriteString(string name, string value)
        {
            Write(name, value, RegistryValueKind.String);
        }

        public void WriteDword(string name, int value)
        {
            Write(name, value, RegistryValueKind.DWord);
        }

        public void WriteMultiString(string name, string[] value)
        {
            Write(name, value, RegistryValueKind.MultiString);
        }

        public void DeleteValues(IEnumerable<string> names)
        {
            try
            {
                using var appNameKey = CurrentUserContext.OpenSubKey(RegistryStuff.StartPath, true);

                if (appNameKey == null)
                {
                    return;
                }

                foreach (var name in names)
                {
                    appNameKey.DeleteValue(name, false);
                }
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                // Left for the next call
            }
        }

        private static void Write(string name, object value, RegistryValueKind kind)
        {
            using var appNameKey = CurrentUserContext.CreateSubKey(RegistryStuff.StartPath, true);

            appNameKey.SetValue(name, value, kind);
        }
    }
}
