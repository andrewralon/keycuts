using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// Files sent to the running app on macOS (Phase 7, Phase 0.4): the Finder Quick Action's <c>open -b</c>, a drop on the Dock icon,
    /// or <c>open -a keycuts file</c>. They arrive as <c>FileActivatedEventArgs</c>, never in <c>argv</c>; the first one fills the
    /// creator, as the Explorer verb's path does on Windows, and the window comes forward on it.
    /// </summary>
    public static class MacFileOpen
    {
        public static void Attach(Application app, ShellViewModel shell, MainWindow window)
        {
            if (app.TryGetFeature<IActivatableLifetime>() is { } activation)
            {
                activation.Activated += (sender, e) =>
                {
                    if (e is FileActivatedEventArgs files && Fill(shell.Creator, files.Files.Select(f => f.TryGetLocalPath())))
                    {
                        MacMenuBar.Show(window, 1);
                    }
                };
            }
        }

        /// <summary>Puts the first path in the creator, resolved as a typed or dropped one is (a folder arrives with a trailing slash); false when there is none.</summary>
        public static bool Fill(CreatorViewModel creator, IEnumerable<string?> paths)
        {
            var path = TargetData.TrimFolderSlash(paths.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)));

            if (path == null)
            {
                return false;
            }

            var result = TargetResolver.Resolve(path);
            creator.Target = result.Target;
            creator.TargetKind = result.Kind;
            creator.TargetError = result.Error;
            return true;
        }
    }
}
