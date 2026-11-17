using System.Collections.Generic;
using CommandLine;

namespace keycuts.CLI
{
    /// <summary><c>keycuts rm &lt;name&gt;</c> (#173): deletes a keycut as the Manager does, to the Recycle Bin.</summary>
    [Verb("rm", HelpText = "Delete a keycut, to the Recycle Bin, as the manager does.")]
    public class RemoveOptions
    {
        [Value(0, MetaName = "name", Required = true, HelpText = "The keycut to delete.")]
        public string Name { get; set; }
    }

    /// <summary><c>keycuts rename &lt;old&gt; &lt;new&gt; [-f]</c> (#173): the Manager's rename.</summary>
    [Verb("rename", HelpText = "Rename a keycut, as the manager does.")]
    public class RenameOptions
    {
        [Value(0, MetaName = "old", Required = true, HelpText = "The keycut's name now.")]
        public string OldName { get; set; }

        [Value(1, MetaName = "new", Required = true, HelpText = "Its new name. A name another keycut has is refused.")]
        public string NewName { get; set; }

        [Option('f', "force", HelpText = "Don't ask when another command has the new name.")]
        public bool Force { get; set; }
    }

    /// <summary><c>keycuts open &lt;name&gt; [arguments]</c> (#173): runs a keycut as Win+R would.</summary>
    [Verb("open", HelpText = "Open a keycut, as Win+R would.")]
    public class OpenOptions
    {
        [Value(0, MetaName = "name", Required = true, HelpText = "The keycut to open.")]
        public string Name { get; set; }

        [Value(1, MetaName = "arguments", Required = false, HelpText = "Anything after the name is passed to the keycut.")]
        public IEnumerable<string> Arguments { get; set; }
    }
}
