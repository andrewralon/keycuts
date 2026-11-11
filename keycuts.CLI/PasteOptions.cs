using CommandLine;

namespace keycuts.CLI
{
    /// <summary><c>keycuts paste &lt;name&gt;</c> (#97): copies a keycut's destination, or a text file's contents, to the clipboard.</summary>
    [Verb("paste", HelpText = "Copy a keycut's destination (or a text file's contents) to the clipboard, same as keypaste.")]
    public class PasteOptions
    {
        [Value(0, MetaName = "name", Required = true,
            HelpText = "A keycut name or text file, looked up in the current folder, the keycuts folder, then the keycuts.exe folder (.txt and .bat are tried too)")]
        public string Name { get; set; }
    }
}
