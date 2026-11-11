using CommandLine;
using CommandLine.Text;
using keycuts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace keycuts.CLI
{
    /// <summary>
    /// The usage text. Help and version go to stdout and succeed; a parse error goes to stderr, with the reason first and
    /// a pointer to --help, so a script can tell them apart.
    /// </summary>
    public static class Usage
    {
        public const string Summary = "Open anything on your computer by nickname.";

        /// <summary>The version without the build metadata the SDK appends after a plus sign.</summary>
        public static string GetVersion()
        {
            var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return info?.Split('+')[0] ?? Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        }

        public static Parser CreateParser()
        {
            return new Parser(with =>
            {
                with.HelpWriter = null;
                with.AutoHelp = true;
                with.AutoVersion = true;
            });
        }

        /// <summary>Set once a usage error has been explained on stderr, so <see cref="Program.Main"/> doesn't add a second line.</summary>
        public static bool WroteError { get; private set; }

        /// <summary>Prints help or version to <paramref name="output"/>, or the reasons a parse failed to <paramref name="error"/>.</summary>
        public static ExitCode Show(ParserResult<object> result, IEnumerable<Error> errors, TextWriter output, TextWriter error)
        {
            var list = errors.ToList();

            if (list.IsVersion())
            {
                output.WriteLine($"keycuts {GetVersion()}");
                return ExitCode.Success;
            }

            if (list.IsHelp())
            {
                // "keycuts --help" asks for the verb list; "keycuts paste --help" for one verb's options
                var topLevel = list.OfType<HelpVerbRequestedError>().Any(e => e.Type == null);
                var verb = result.TypeInfo.Current == typeof(PasteOptions) ? "paste" : "create";

                var text = HelpText.AutoBuild(
                    result,
                    help => Configure(help, topLevel, verb),
                    example => example,
                    verbsIndex: topLevel,
                    maxDisplayWidth: 80).ToString().TrimEnd();

                // The library only lists the long form; -h is accepted too (see Program.Run), so say so, keeping the column
                text = text.Replace("  --help    ", "  -h, --help");

                // The library puts a blank line between the "Commands:" heading and the verb list; drop it
                text = text.Replace("Commands:" + Environment.NewLine + Environment.NewLine, "Commands:" + Environment.NewLine);

                output.WriteLine(topLevel ? text + Environment.NewLine + Environment.NewLine + TopLevelText : text);
                return ExitCode.Success;
            }

            // Short on purpose: what was wrong, then where to look. The full option list is one --help away.
            var reasons = result.TypeInfo.Current == typeof(PasteOptions) && list.OfType<MissingRequiredOptionError>().Any()
                ? "paste needs a keycut or text file name, for example: keycuts paste db"
                : HelpText.RenderParsingErrorsText(
                    result, SentenceBuilder.Create().FormatError, SentenceBuilder.Create().FormatMutuallyExclusiveSetErrors, 0).Trim();
            error.WriteLine($"keycuts: {reasons}");
            error.WriteLine("Run 'keycuts --help' for usage.");
            WroteError = true;
            return ExitCode.BadArguments;
        }

        /// <summary>One line of heading, and a usage line for the command shown; the top level adds the usage block.</summary>
        private static HelpText Configure(HelpText help, bool topLevel, string verb)
        {
            help.Heading = $"keycuts {GetVersion()} - {Summary}{Environment.NewLine}";
            help.Copyright = "";
            help.AddDashesToOption = true;
            help.AdditionalNewLineAfterOption = false;

            if (topLevel)
            {
                help.AddPreOptionsLines(new[]
                {
                    "Usage:",
                    "  keycuts                                         Open the app UI",
                    "  keycuts [create] -d <path> -s <name> [options]  Create a keycut",
                    "  keycuts paste <name>                            Copy destination to clipboard",
                    "    keypaste <name>                                 ",
                    "  keycuts <command> --help                        Help for that command",
                    "  keycuts --help                                  Open this help message",
                    "",
                    "Commands:"
                });
            }
            else
            {
                help.AddPreOptionsLines(new[]
                {
                    verb == "paste" ? "Usage: keycuts paste <name>" : "Usage: keycuts [create] -d <path> -s <name> [options]",
                    "",
                    "Options:"
                });
            }

            return help;
        }

        private static string TopLevelText =>
            "Examples:" + Environment.NewLine +
            "  keycuts -d C:\\Windows\\notepad.exe -s np -f" + Environment.NewLine +
            "  keycuts -d C:\\Path\\To\\Hobbies -s hobbies" + Environment.NewLine +
            "  keycuts -d C:\\Users\\MyUser\\Desktop -s desktop" + Environment.NewLine +
            "  keycuts -d https://mail.google.com -s gmail" + Environment.NewLine +
            "  keycuts paste gmail" + Environment.NewLine +
            "  keypaste gmail";

        /// <summary>The error shown when create is missing what it needs.</summary>
        public static void WriteMissingArguments(TextWriter error)
        {
            error.WriteLine("keycuts: creating a keycut needs both --destination (-d) and --shortcut (-s).");
            error.WriteLine("Run 'keycuts --help' for usage.");
            WroteError = true;
        }
    }
}
