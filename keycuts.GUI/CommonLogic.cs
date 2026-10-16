using keycuts.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.GUI
{
    public class CommonLogic
    {
        public ExitCode CreateShortcut(string destination, string shortcutName, bool force = false)
        {
            // Temporary adapter until the creator view (KC-11) uses KeycutCreator directly
            var options = new CreateOptions { Overwrite = force ? true : (bool?)null };
            var created = Task.Run(() => KeycutCreator.CreateAsync(destination, shortcutName, options)).GetAwaiter().GetResult();

            switch (created.Outcome)
            {
                case CreateOutcome.Success:
                    return ExitCode.Success;
                case CreateOutcome.DuplicateName:
                    return ExitCode.FileAlreadyExists;
                case CreateOutcome.InvalidName:
                    return ExitCode.BadArguments;
                case CreateOutcome.DestinationInvalid:
                    return ExitCode.CannotFindPath;
                case CreateOutcome.OutputFolderError:
                    return ExitCode.CannotCreateOutputFolder;
                case CreateOutcome.PathUpdateError:
                    return ExitCode.CannotUpdatePath;
                default:
                    return ExitCode.NotStarted;
            }
        }

        public void SetOutputFolder(string outputFolder)
        {
            var runner = new Runner();
            runner.SetOutputFolder(outputFolder);
        }
    }
}
