using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public enum ExitCode
    {
        Success = 0,
        BadArguments = 1,
        CannotFindFile = 2,
        CannotFindPath = 3,
        CannotOpenFile = 4,
        AccessIsDenied = 5,
        HandleIsInvalid = 6,
        FileAlreadyExists = 10,
        CannotCreateOutputFolder = 11,
        CannotUpdatePath = 12
    }
}
