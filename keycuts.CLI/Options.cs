using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.CLI
{
    public class Options
    {
        [Option('d', "destination", Required = false, HelpText = "The destination path to be opened with a shortcut.")]
        public string Destination { get; set; }

        [Option('s', "shortcut", Required = false, HelpText = "The shortcut filename (or full path) used to open the destination.")]
        public string Shortcut { get; set; }

        [Option('w', "openwithapp", Required = false, HelpText = "The program path which opens the destination file.")]
        public string OpenWithApp { get; set; }

        [Option('o', "outputfolder", Required = false, HelpText = "Output folder for shortcuts created without a full path.")]
        public string OutputFolder { get; set; }

        [Option('f', "force", DefaultValue = false, HelpText = "Force overwrite the shortcut file if it already exists.")]
        public bool Force { get; set; }

		//[HelpOption(HelpText = "Displays this help screen.")]
		//public string GetUsage()
		//{
		//	return HelpText.AutoBuild(this,
		//	  (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current), true);
		//}
	}
}
