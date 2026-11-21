using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace keycuts.Tests
{
    // Tests that only mean something on one platform (the registry, PATH, .bat files on Windows; the app bundle and shell
    // scripts on macOS). They show as skipped elsewhere, so a run on either platform lists every test (docs/macos-port.md).

    /// <summary>A fact that runs on Windows only.</summary>
    public sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute([CallerFilePath] string sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "Windows only";
            }
        }
    }

    /// <summary>A theory that runs on Windows only.</summary>
    public sealed class WindowsTheoryAttribute : TheoryAttribute
    {
        public WindowsTheoryAttribute([CallerFilePath] string sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "Windows only";
            }
        }
    }

    /// <summary>A fact that runs on macOS only.</summary>
    public sealed class MacFactAttribute : FactAttribute
    {
        public MacFactAttribute([CallerFilePath] string sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsMacOS())
            {
                Skip = "macOS only";
            }
        }
    }

    /// <summary>A theory that runs on macOS only.</summary>
    public sealed class MacTheoryAttribute : TheoryAttribute
    {
        public MacTheoryAttribute([CallerFilePath] string sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsMacOS())
            {
                Skip = "macOS only";
            }
        }
    }
}
