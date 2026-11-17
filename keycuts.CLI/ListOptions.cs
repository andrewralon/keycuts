using CommandLine;

namespace keycuts.CLI
{
    /// <summary><c>keycuts list [&lt;search&gt;] [--usage] [--sort name|runs|last-run]</c> (#173, #237): the keycuts in the keycuts folder.</summary>
    [Verb("list", HelpText = "List the keycuts: name, type, target and description.")]
    public class ListOptions
    {
        [Value(0, MetaName = "search", Required = false,
            HelpText = "Only keycuts whose name, type, target or description holds this text (any case).")]
        public string Search { get; set; }

        [Option("usage", HelpText = "Add Runs and Last run (ISO local time). A dash means the keycut doesn't count its runs.")]
        public bool Usage { get; set; }

        [Option("sort", MetaValue = "name|runs|last-run", HelpText = "The order: name (the default), runs (most first) or last-run (latest first).")]
        public string Sort { get; set; }

        [Option("json", HelpText = "JSON for scripts: name, type, target, description, runs, lastRun and file, always.")]
        public bool Json { get; set; }
    }
}
