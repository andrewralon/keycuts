using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace keycuts.CLI
{
    /// <summary>Creating a keycut is the default verb, so <c>keycuts -d … -s …</c> works without naming it.</summary>
    [Verb("create", isDefault: true, HelpText = "Create a keycut (the default: \"create\" can be left out).")]
    public class Options
    {
        [Option('d', "destination", Required = false, MetaValue = "<path>", HelpText = "What the keycut opens: a file, folder, URL or shell: path.")]
        public string Destination { get; set; }

        [Option('s', "shortcut", Required = false, MetaValue = "<name>", HelpText = "The keycut's name, typed in Win+R (or a full path for the .bat).")]
        public string Shortcut { get; set; }

        [Option('w', "openwithapp", Required = false, MetaValue = "<program>", HelpText = "A program to open the destination file with.")]
        public string OpenWithApp { get; set; }

        [Option('o', "outputfolder", Required = false, MetaValue = "<folder>", HelpText = "Where to put the keycut when --shortcut isn't a full path.")]
        public string OutputFolder { get; set; }

        [Option('f', "force", HelpText = "Don't ask: replace a keycut with the same name, and create it even if another command has the name.")]
        public bool Force { get; set; }
    }
}
