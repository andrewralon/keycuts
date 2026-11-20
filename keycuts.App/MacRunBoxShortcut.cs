using System.Runtime.Versioning;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>Settings' Run box shortcut in the app (Phase 6.2): stored in the settings file and registered with Carbon.</summary>
    [SupportedOSPlatform("macos")]
    public sealed class MacRunBoxShortcut : IRunBoxShortcut
    {
        public RunBoxHotkey? Current
        {
            get { return MacHotKey.Current; }
        }

        public int Status
        {
            get { return MacHotKey.Status; }
        }

        public int Apply(RunBoxHotkey? hotkey)
        {
            RegistryStuff.SetRunBoxHotkey(hotkey);
            return MacHotKey.Register(hotkey);
        }

        public void Pause(bool paused)
        {
            MacHotKey.Pause(paused);
        }
    }
}
