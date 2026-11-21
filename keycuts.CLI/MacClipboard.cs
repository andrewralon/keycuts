using System;
using System.Diagnostics;
using System.IO;

namespace keycuts.CLI
{
    /// <summary>The macOS clipboard for <c>keycuts paste</c>: the text goes to <c>pbcopy</c>'s standard input, as UTF-8.</summary>
    internal static class MacClipboard
    {
        public static void SetText(string text)
        {
            var start = new ProcessStartInfo("/usr/bin/pbcopy")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                StandardInputEncoding = new System.Text.UTF8Encoding(false)
            };

            using var process = Process.Start(start) ?? throw new IOException("pbcopy didn't start.");
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new IOException($"pbcopy failed (exit code {process.ExitCode}).");
            }
        }
    }
}
