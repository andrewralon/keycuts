using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>Seam over the per-user classes hive. Paths are relative to the hive root.</summary>
    public interface IClassesStore
    {
        bool KeyExists(string path);

        void SetDefaultValue(string path, string value);

        void DeleteTree(string path);
    }

    /// <summary>
    /// HKCU\Software\Classes: Explorer merges it over HKLM, and unlike HKCR it is writable without elevation.
    /// </summary>
    public class CurrentUserClassesStore : IClassesStore
    {
        private const string ClassesPath = @"Software\Classes";

        public bool KeyExists(string path)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ClassesPath}\{path}");

            return key != null;
        }

        public void SetDefaultValue(string path, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"{ClassesPath}\{path}", true);

            key.SetValue("", value);
        }

        public void DeleteTree(string path)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesPath}\{path}", false);
        }
    }

    /// <summary>
    /// The Explorer "keycut this!" verbs. Keep these keys in step with installer/Package.wxs, which writes the same ones.
    /// </summary>
    public class ContextMenuRegistrar
    {
        public const string GuiExeName = "keycuts.GUI.exe";

        private const string SelectedItemArgument = "%1";

        private const string CurrentFolderArgument = "%V";

        private static readonly (string Key, string Argument)[] Verbs =
        {
            ($@"Directory\shell\{Runner.AppName}", SelectedItemArgument),
            ($@"Directory\Background\shell\{Runner.AppName}", CurrentFolderArgument),
            ($@"*\shell\{Runner.AppName}", SelectedItemArgument),
            ($@"Folder\shell\{Runner.AppName}", SelectedItemArgument)
        };

        private readonly IClassesStore _store;

        private readonly string _handlerPath;

        public ContextMenuRegistrar()
            : this(new CurrentUserClassesStore(), Path.Combine(AppContext.BaseDirectory, GuiExeName))
        {
        }

        public ContextMenuRegistrar(IClassesStore store, string handlerPath)
        {
            _store = store;
            _handlerPath = handlerPath;
        }

        public static IReadOnlyList<string> VerbKeys
        {
            get { return Verbs.Select(verb => verb.Key).ToList(); }
        }

        /// <summary>True only when every verb is present, so a partial registration gets repaired on the next save.</summary>
        public bool IsRegistered()
        {
            try
            {
                return Verbs.All(verb => _store.KeyExists($@"{verb.Key}\command"));
            }
            catch (Exception e) when (IsRegistryFailure(e))
            {
                return false;
            }
        }

        /// <summary>Adds every verb, or none of them. False on failure; never throws for registry errors.</summary>
        public bool Register()
        {
            try
            {
                foreach (var (key, argument) in Verbs)
                {
                    _store.SetDefaultValue(key, Runner.RightClickContextMenuText);
                    _store.SetDefaultValue($@"{key}\command", $"\"{_handlerPath}\" \"{argument}\"");
                }

                return true;
            }
            catch (Exception e) when (IsRegistryFailure(e))
            {
                Unregister();

                return false;
            }
        }

        /// <summary>Removes every verb. False on failure; never throws for registry errors.</summary>
        public bool Unregister()
        {
            try
            {
                foreach (var (key, _) in Verbs)
                {
                    _store.DeleteTree(key);
                }

                return true;
            }
            catch (Exception e) when (IsRegistryFailure(e))
            {
                return false;
            }
        }

        internal static bool IsRegistryFailure(Exception e)
        {
            return e is UnauthorizedAccessException || e is System.Security.SecurityException || e is IOException;
        }
    }
}
