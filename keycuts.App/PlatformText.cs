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
    }
}
