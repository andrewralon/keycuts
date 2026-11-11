using System;
using System.Collections.Generic;
using System.Text;

namespace keycuts.Common
{
    public enum ExitCode
    {
        NotStarted = -1,
        Success = 0,
        BadArguments = 1,
        CannotFindFile = 2,
        CannotFindPath = 3,
        CannotOpenFile = 4,
        AccessIsDenied = 5,
        HandleIsInvalid = 6,
        FileAlreadyExists = 10,
        CannotCreateOutputFolder = 11,
        CannotUpdatePath = 12,
        CannotSetClipboard = 13,
        /// <summary>The name needs a y / n answer (a replace or a name conflict, #153), but input is redirected.</summary>
        ConfirmationNeeded = 14
    }
}
