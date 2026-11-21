using CommandLine;

namespace keycuts.CLI
{
    /// <summary><c>keycuts path [status|add|remove]</c> (#101): whether the keycuts folder is on PATH, and adding or removing it.</summary>
    [Verb("path", HelpText = "Say whether the keycuts folder is on PATH, or add it or take it out.")]
    public class PathOptions
    {
        [Value(0, MetaName = "action", Required = false, HelpText = "status (the default), add or remove.")]
        public string Action { get; set; }
    }
}
