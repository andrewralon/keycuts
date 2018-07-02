using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace keycuts.Common
{
    class PathSetup
    {
        private static string FixPath(string newPath)
        {
            if (!newPath.EndsWith(";"))
            {
                newPath += ";";
            }
            return newPath;
        }

        public static bool ExistsInSystemPath(string newPath)
        {
            var systemPath = GetSystemPath();
            var result = systemPath.Contains(newPath);
            return result;
        }

        public static string GetSystemPath()
        {
            var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
            if (systemPath == null)
            {
                // Throw new exception?
                Console.WriteLine("Unable to retrieve the current system path. Oops.");
            }
            return systemPath;
        }

        public static bool AddToOrReplaceInSystemPath(string oldPath, string newPath)
        {
            var result = true;

            if (oldPath != newPath)
            {
                if (ExistsInSystemPath(oldPath))
                {
                    result = ReplaceInSystemPath(oldPath, newPath);
                }
                else
                {
                    result = AddToSystemPath(newPath);
                }
            }

            return result;
        }

        public static bool AddToSystemPath(string pathToAdd)
        {
            var result = false;
            pathToAdd = FixPath(pathToAdd);

            var systemPath = GetSystemPath();
            if (!systemPath.Contains(pathToAdd))
            {
                var newSystemPath = string.Format("{0}{1}", systemPath, pathToAdd);
                Environment.SetEnvironmentVariable("PATH", newSystemPath, EnvironmentVariableTarget.Machine);
                result = true;
            }
            else
            {
#if DEBUG
                Console.WriteLine(string.Format("System path already contains \"{0}\"", pathToAdd));
#endif
            }

            return result;
        }

        public static bool RemoveFromSystemPath(string pathToRemove)
        {
            var result = ReplaceInSystemPath(pathToRemove, "");
            return result;
        }

        public static bool ReplaceInSystemPath(string oldPath, string newPath)
        {
            var result = false;
            newPath = FixPath(newPath);
            oldPath = FixPath(oldPath);

            var systemPath = GetSystemPath();
            if (systemPath.Contains(oldPath))
            {
                var newSystemPath = systemPath.Replace(oldPath, newPath);
                Environment.SetEnvironmentVariable("PATH", newSystemPath, EnvironmentVariableTarget.Machine);
                result = true;
            }
            else
            {
#if DEBUG
                Console.WriteLine(string.Format("System path does not contain \"{0}\"", oldPath));
#endif
                result = false;
            }

            return result;
        }
    }
}
