using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>What the Run box does on every system (Phase 6.4; Windows, issue #306): load the keycuts, and run one through <see cref="Desktops.Current"/>.</summary>
    public static class RunBoxActions
    {
        /// <summary>Loads the folder's keycuts off the UI thread and hands them to the box when they are in.</summary>
        public static async Task LoadAsync(RunBoxViewModel viewModel)
        {
            try
            {
                var keycuts = await Task.Run(() => KeycutRepository.CreateDefault().LoadAllAsync());
                Dispatcher.UIThread.Post(() => viewModel.Keycuts = keycuts);
            }
            catch (Exception ex) when (ex is KeycutRepositoryException || ex is System.IO.IOException)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: the Run box couldn't load the keycuts: " + ex.Message);
            }
        }

        /// <summary>Runs the keycut detached, so its own lines count the run.</summary>
        public static void Run(Keycut keycut)
        {
            try
            {
                Desktops.Current.RunKeycut(keycut.FilePath);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException || ex is InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't run \"" + keycut.FilePath + "\": " + ex.Message);
            }
        }
    }
}
