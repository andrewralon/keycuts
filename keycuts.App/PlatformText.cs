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

        /// <summary>The transparent icon background is Windows only: the Dock has no tile behind the icon.</summary>
        public static readonly bool ShowsTransparentIcon = !PlatformKeys.IsMac;
    }
}
