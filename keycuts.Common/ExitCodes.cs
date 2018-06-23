using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public class ExitCodes
    {
        public static string GetName(int number)
        {
            return Enum.GetName(typeof(ExitCode), number);
        }
    }
}
