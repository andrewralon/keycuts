using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutsTR
{
    class Options
    {
        [Option('d', "destination", Required = false, HelpText = "The destination path to be opened with a shortcut.")]
        public string Destination { get; set; }

        [Option('s', "shortcut", Required = false, HelpText = "The shortcut filename (or full path) used to open the destination.")]
        public string Shortcut { get; set; }

        [Option('a', "openwithapp", Required = false, HelpText = "The path to a program to open the destination file.")]
        public string OpenWithAppPath { get; set; }

        [Option('f', "force", DefaultValue = false, HelpText = "Force overwrite the shortcut file if it already exists.")]
        public bool Force { get; set; }

        [Option('o', "defaultfolder", Required = false, HelpText = "Default folder for shortcuts created without a full path.")]
        public string DefaultFolder { get; set; }

		//[HelpOption(HelpText = "Displays this help screen.")]
		//public string GetUsage()
		//{
		//	return HelpText.AutoBuild(this,
		//	  (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current), true);
		//}
	}
}
