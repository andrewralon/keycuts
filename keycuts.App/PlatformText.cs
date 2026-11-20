using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>
    /// Words that name the platform's own things (docs/styleguide.md, Keys): Notepad and File Explorer on Windows, a text editor
    /// and Finder on macOS, and tooltips with the platform's key names (<see cref="PlatformKeys"/>).
    /// </summary>
    public static class PlatformText
    {
        /// <summary>The row menu's item that opens the raw file.</summary>
        public static readonly string EditInEditor = PlatformKeys.IsMac ? "Edit in text editor" : "Edit in Notepad";

        /// <summary>The row menu's item that shows the file in the file manager.</summary>
        public static readonly string ShowFile = PlatformKeys.IsMac ? "Show in Finder" : "Open file location";

        /// <summary>Where to edit a keycut the form can't: "in Notepad", "in a text editor".</summary>
        public static readonly string InEditor = PlatformKeys.IsMac ? "in a text editor" : "in Notepad";

        public static readonly string UndoDeleteTip = PlatformKeys.Tip("Undo the delete", "Z");

        /// <summary>Settings: "Right-click context menus" on Windows, the Finder Quick Action on macOS (Phase 7 installs it).</summary>
        public static readonly string RightClickName = PlatformKeys.IsMac ? "Finder Quick Action" : "Right-click context menus";

        public static readonly string RightClickNote = PlatformKeys.IsMac
            ? "Add “keycut this!” to Finder’s Quick Actions."
            : "Add “keycut this!” to File Explorer menus.";

        /// <summary>Settings: macOS has one variable, the home folder (Phase 4.6).</summary>
        public static readonly string VariablesName = PlatformKeys.IsMac ? "Use $HOME in paths" : "Use environment variables in paths";

        public static readonly string VariablesNote = PlatformKeys.IsMac
            ? "Save paths in your home folder as $HOME/…. Hold Shift to preview or create a keycut like this, even when off."
            : "Save paths as %USERPROFILE%\\… or similar. Hold Shift to preview or create a keycut like this, even when disabled.";

        public static readonly string VariablesHelp = PlatformKeys.IsMac
            ? "Saves /Users/you/Desktop as $HOME/Desktop, so keycuts work on other accounts. Hold Shift while creating to flip it for one keycut."
            : "Saves C:\\Users\\you\\Desktop as %USERPROFILE%\\Desktop, so keycuts work on other accounts. Hold Shift while creating to flip it for one keycut.";

        /// <summary>
        /// The welcome panel (Phase 5.5): how to run a keycut. On macOS that's the Run box, from its shortcut, or the name in
        /// Terminal; never Spotlight, which can't run them.
        /// </summary>
        public static readonly string WelcomeIntro = PlatformKeys.IsMac
            ? "keycuts lets you open your apps, files, folders, and websites with short names you choose. Create one here, then press ⇧⌘Space and type the name, or type it in Terminal."
            : "keycuts lets you open your apps, files, folders, and websites with short names you choose. Create one here, then press Win+R, type the name, and press Enter to open it.";

        public static readonly string WelcomeCreatedHint = PlatformKeys.IsMac
            ? "Created. Now try it out! Press ⇧⌘Space, type its name, and press Return to open it."
            : "Created. Now try it out! Press Win+R, type its name, and press Enter to open it.";

        /// <summary>After a create, when the folder isn't on PATH: only Terminal needs it on macOS (the Run box finds keycuts itself).</summary>
        public static readonly string WelcomeNotOnPath = "Created, but " + PlatformWords.Launcher + " can’t find it yet: the keycuts folder isn’t on your PATH.";

        public static readonly string AddToPathHelp = "Adds the keycuts folder to PATH, so " + PlatformWords.Launcher + " can find your keycuts";

        /// <summary>The transparent icon background is Windows only: the Dock has no tile behind the icon.</summary>
        public static readonly bool ShowsTransparentIcon = !PlatformKeys.IsMac;
    }
}
