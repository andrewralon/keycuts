using System.Collections.Generic;

namespace keycuts.Common
{
    /// <summary>
    /// The file a keycut is: its extension, how it's written and how it's read back. Windows keycuts are <c>.bat</c> files
    /// (<see cref="BatFormat"/>); macOS gets a <c>#!/bin/sh</c> script with no extension (docs/macos-port.md, Phase 4).
    /// </summary>
    public interface IScriptFormat
    {
        /// <summary>The keycut file's extension with its dot (<c>.bat</c>), or empty for none.</summary>
        string Extension { get; }

        /// <summary>What starts a comment line, such as the <c>&lt;type&gt;</c> and <c>&lt;target&gt;</c> lines (<c>REM</c>).</summary>
        string CommentPrefix { get; }

        /// <summary>Whether a written keycut must be marked executable before it can run.</summary>
        bool NeedsExecutableBit { get; }

        /// <summary>The lines of a keycut file. Touches no files.</summary>
        List<string> EmitLines(Shortcut shortcut, string description = "");

        /// <summary>Reads lines already read from <paramref name="file"/>, which is only used for its name.</summary>
        ShortcutFile Parse(string file, string[] lines);
    }

    /// <summary>The <c>.bat</c> keycut: <see cref="Runner.BuildShortcutLines"/> writes it and <see cref="ShortcutFile"/> reads it.</summary>
    public sealed class BatFormat : IScriptFormat
    {
        public string Extension => ".bat";

        public string CommentPrefix => "REM";

        public bool NeedsExecutableBit => false;

        public List<string> EmitLines(Shortcut shortcut, string description = "")
        {
            return Runner.BuildShortcutLines(shortcut, description);
        }

        public ShortcutFile Parse(string file, string[] lines)
        {
            return new ShortcutFile(file, lines);
        }
    }

    /// <summary>The format keycuts are written and read in.</summary>
    public static class ScriptFormats
    {
        /// <summary>Every platform uses <see cref="BatFormat"/> until macOS gets its own (Phase 4).</summary>
        public static readonly IScriptFormat Current = new BatFormat();
    }
}
