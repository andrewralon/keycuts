using System;

namespace keycuts.Common
{
    public enum TargetStatus
    {
        Unknown,
        Ok,
        Missing
    }

    /// <summary>One keycut (.bat file) in the output folder, as shown in the manager.</summary>
    public record Keycut
    {
        /// <summary>File name without the .bat extension.</summary>
        public string Name { get; init; }

        public KeycutKind Kind { get; init; }

        public ShortcutType RawType { get; init; }

        /// <summary>May be empty.</summary>
        public string Description { get; init; } = "";

        public string Target { get; init; }

        /// <summary>
        /// For a File keycut: the path of the program, without its arguments (<see cref="ShortcutFile.Program"/>), which is what
        /// the Missing check looks for. Empty when the parser couldn't tell it apart from the arguments; the check then has
        /// nothing specific to look for. Folders and the rest have none.
        /// </summary>
        public string Program { get; init; } = "";

        /// <summary>For a File read from a <c>START "" /D "&lt;folder&gt;" &lt;program&gt;</c> line: the folder (<see cref="ShortcutFile.StartInFolder"/>). Empty otherwise.</summary>
        public string StartInFolder { get; init; } = "";

        /// <summary>For the same: the program as the line wrote it, apart from the folder (<see cref="ShortcutFile.ProgramName"/>).</summary>
        public string ProgramName { get; init; } = "";

        /// <summary>For a File keycut: what follows <see cref="Program"/> on its launch line (<see cref="ShortcutFile.Arguments"/>); often empty.</summary>
        public string Arguments { get; init; } = "";

        public string FilePath { get; init; }

        /// <summary>
        /// Whether the Edit form can save this keycut without losing anything (#202, from <see cref="KeycutRepository.CanEditInForm(string, string[])"/>),
        /// worked out when the file is loaded so the menu can grey out Edit before it opens. False for a hand-edited launch
        /// line, the 0.1.x format and a file that isn't valid UTF-8. A keycut built by hand, not read from a file, counts as
        /// editable: the form checks the file again when it opens, and that check is the one that counts.
        /// </summary>
        public bool CanEditInForm { get; init; } = true;

        /// <summary>
        /// When the .bat file was last written (local time), read when the keycut is loaded. Renaming rewrites the file,
        /// so it updates this. <c>default</c> when unknown (a keycut built by hand, not read from a file).
        /// </summary>
        public DateTime Modified { get; init; }

        /// <summary>Set in the background by <see cref="KeycutRepository.CheckTargetsAsync"/>. Missing means a specific path was looked for and isn't there; anything else the check couldn't pin down stays Unknown.</summary>
        public TargetStatus TargetStatus { get; set; } = TargetStatus.Unknown;

        /// <summary>
        /// Set with <see cref="TargetStatus"/>: the path the check looked for, with environment variables expanded
        /// (<c>C:\Program Files (x86)\Google\Chrome\Application\chrome.exe</c>), or the bare program name it searched
        /// Windows' folders, PATH and App Paths for. Empty until a check has marked the keycut.
        /// </summary>
        public string CheckedPath { get; set; } = "";
    }
}
