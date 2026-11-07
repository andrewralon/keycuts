using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using keycuts.Common;
using Xunit;

namespace keycuts.Tests
{
    public class ContextMenuRegistrarTests
    {
        private const string Handler = @"C:\apps\keycuts\keycuts.GUI.exe";

        private class FakeClassesStore : IClassesStore
        {
            public Dictionary<string, string> Keys { get; } = new Dictionary<string, string>();

            public int FailAfterWrites { get; set; } = int.MaxValue;

            public bool DeniesEverything { get; set; }

            public bool KeyExists(string path)
            {
                ThrowIfDenied();

                return Keys.ContainsKey(path);
            }

            public void SetDefaultValue(string path, string value)
            {
                ThrowIfDenied();

                if (Keys.Count >= FailAfterWrites)
                {
                    throw new UnauthorizedAccessException();
                }

                Keys[path] = value;
            }

            public void DeleteTree(string path)
            {
                ThrowIfDenied();

                foreach (var key in Keys.Keys.Where(key => key == path || key.StartsWith(path + "\\")).ToList())
                {
                    Keys.Remove(key);
                }
            }

            private void ThrowIfDenied()
            {
                if (DeniesEverything)
                {
                    throw new UnauthorizedAccessException();
                }
            }
        }

        [Fact]
        public void Register_writes_every_verb_with_the_explicit_handler()
        {
            var store = new FakeClassesStore();
            var registrar = new ContextMenuRegistrar(store, Handler);

            Assert.True(registrar.Register());
            Assert.True(registrar.IsRegistered());
            Assert.Equal("keycut this!", store.Keys[@"*\shell\keycuts"]);
            Assert.Equal($"\"{Handler}\" \"%1\"", store.Keys[@"*\shell\keycuts\command"]);
            Assert.Equal($"\"{Handler}\" \"%1\"", store.Keys[@"Directory\shell\keycuts\command"]);
            Assert.Equal($"\"{Handler}\" \"%1\"", store.Keys[@"Folder\shell\keycuts\command"]);
            Assert.Equal($"\"{Handler}\" \"%V\"", store.Keys[@"Directory\Background\shell\keycuts\command"]);
        }

        [Fact]
        public void Unregister_removes_every_verb()
        {
            var store = new FakeClassesStore();
            var registrar = new ContextMenuRegistrar(store, Handler);
            registrar.Register();

            Assert.True(registrar.Unregister());
            Assert.False(registrar.IsRegistered());
            Assert.Empty(store.Keys);
        }

        [Fact]
        public void Partial_registration_counts_as_not_registered()
        {
            var store = new FakeClassesStore();
            var registrar = new ContextMenuRegistrar(store, Handler);
            registrar.Register();

            store.DeleteTree(@"Folder\shell\keycuts");

            Assert.False(registrar.IsRegistered());
        }

        [Fact]
        public void Denied_access_returns_false_instead_of_throwing()
        {
            var registrar = new ContextMenuRegistrar(new FakeClassesStore { DeniesEverything = true }, Handler);

            Assert.False(registrar.IsRegistered());
            Assert.False(registrar.Register());
            Assert.False(registrar.Unregister());
        }

        [Fact]
        public void Failed_register_rolls_back_the_verbs_it_wrote()
        {
            var store = new FakeClassesStore { FailAfterWrites = 3 };
            var registrar = new ContextMenuRegistrar(store, Handler);

            Assert.False(registrar.Register());
            Assert.Empty(store.Keys);
        }

        [Fact]
        public void Installer_writes_the_same_verbs_as_the_app()
        {
            var wxs = XDocument.Load(FindRepoFile(@"installer\Package.wxs"));
            var installerKeys = wxs.Descendants()
                .Where(element => element.Name.LocalName == "RegistryKey")
                .Select(element => (string)element.Attribute("Key"))
                .Where(key => key.StartsWith(@"Software\Classes\") && !key.EndsWith(@"\command"))
                .Select(key => key.Substring(@"Software\Classes\".Length))
                .OrderBy(key => key);

            Assert.Equal(ContextMenuRegistrar.VerbKeys.OrderBy(key => key), installerKeys);
        }

        internal static string FindRepoFile(string relativePath)
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var candidate = Path.Combine(folder.FullName, relativePath);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException(relativePath);
        }
    }
}
