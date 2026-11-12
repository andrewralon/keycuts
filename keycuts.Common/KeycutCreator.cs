using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public enum CreateOutcome
    {
        Success,
        DuplicateName,
        InvalidName,
        TargetInvalid,
        OutputFolderError,
        PathUpdateError,
        Unexpected
    }

    public class CreateOptions
    {
        /// <summary>
        /// Overwrite an existing keycut with the same name. Null follows the Settings toggle "Ask before replacing a keycut":
        /// replace when it's off, refuse (<see cref="CreateOutcome.DuplicateName"/>) when it's on, so the caller can ask.
        /// </summary>
        public bool? Overwrite { get; set; }

        /// <summary>Null uses the configured output folder.</summary>
        public string OutputFolder { get; set; }

        /// <summary>Optional description stored in the .bat.</summary>
        public string Description { get; set; } = "";

        /// <summary>Null updates the real system PATH.</summary>
        public IPathUpdater PathUpdater { get; set; }

        /// <summary>Save a file or folder target with environment variables (#31). The caller decides (the setting, flipped by Shift).</summary>
        public bool UseEnvironmentVariables { get; set; }

        /// <summary>The variables for <see cref="UseEnvironmentVariables"/>; null reads them from this machine. Tests pass their own.</summary>
        public IReadOnlyList<PathVariable> PathVariables { get; set; }
    }

    public class CreateResult
    {
        public CreateOutcome Outcome { get; private set; }

        /// <summary>Set on success when the created file could be read back.</summary>
        public Keycut Keycut { get; private set; }

        /// <summary>For TargetInvalid: why the target failed.</summary>
        public TargetError TargetError { get; private set; }

        /// <summary>For Unexpected: the exception message, for logging.</summary>
        public string Message { get; private set; }

        public bool IsSuccess => Outcome == CreateOutcome.Success;

        public static CreateResult Success(Keycut keycut)
        {
            return new CreateResult { Outcome = CreateOutcome.Success, Keycut = keycut };
        }

        public static CreateResult Failure(CreateOutcome outcome, string message = null, TargetError targetError = TargetError.None)
        {
            return new CreateResult { Outcome = outcome, Message = message, TargetError = targetError };
        }
    }

    /// <summary>Creates a keycut with a structured result instead of an exit code, off the calling thread.</summary>
    public static class KeycutCreator
    {
        private const int MaxNameLength = 200;

        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// True if <paramref name="name"/> is a plain file name usable for a keycut, and not one of the keycuts commands on
        /// PATH (<see cref="CommandNames"/>), which a keycut would clash with.
        /// </summary>
        public static bool IsValidName(string name)
        {
            if (CommandNames.IsCommandName(name))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > MaxNameLength)
            {
                return false;
            }

            // Covers path separators, drive colons, and wildcards
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            if (name.EndsWith("."))
            {
                return false;
            }

            // "NUL.txt" is reserved too
            var stem = name.Split('.')[0].TrimEnd();

            return !ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        public static Task<CreateResult> CreateAsync(string target, string name, CreateOptions options = null, CancellationToken ct = default)
        {
            return Task.Run(() => CreateCore(target, name, options ?? new CreateOptions(), ct), ct);
        }

        private static CreateResult CreateCore(string target, string name, CreateOptions options, CancellationToken ct)
        {
            try
            {
                if (!IsValidName(name))
                {
                    return CreateResult.Failure(CreateOutcome.InvalidName);
                }

                var resolved = TargetResolver.Resolve(target);

                if (!resolved.IsValid)
                {
                    return CreateResult.Failure(CreateOutcome.TargetInvalid, targetError: resolved.Error);
                }

                ct.ThrowIfCancellationRequested();

                var runner = new Runner();
                var outputFolder = string.IsNullOrWhiteSpace(options.OutputFolder) ? runner.GetOutputFolder() : options.OutputFolder;
                var overwrite = options.Overwrite ?? !runner.GetAskBeforeReplacing();

                if (string.IsNullOrWhiteSpace(outputFolder) || outputFolder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    return CreateResult.Failure(CreateOutcome.OutputFolderError);
                }

                // The ".bat" suffix keeps Shortcut from treating a dot in the name as an extension
                var fileName = name + ShortcutFile.Extension;
                var fullPath = Path.Combine(outputFolder, fileName);

                if (!overwrite && File.Exists(fullPath))
                {
                    return CreateResult.Failure(CreateOutcome.DuplicateName);
                }

                var fixedRunner = new Runner(outputFolder, options.PathUpdater);
                var args = new KeycutArgs(resolved.Target, fileName, force: overwrite)
                {
                    Description = options.Description ?? "",
                    UseEnvironmentVariables = options.UseEnvironmentVariables,
                    PathVariables = options.PathVariables
                };

                switch (fixedRunner.Run(args))
                {
                    case ExitCode.Success:
                        return CreateResult.Success(KeycutRepository.TryParse(fullPath));
                    case ExitCode.FileAlreadyExists:
                        return CreateResult.Failure(CreateOutcome.DuplicateName);
                    case ExitCode.CannotCreateOutputFolder:
                        return CreateResult.Failure(CreateOutcome.OutputFolderError);
                    case ExitCode.CannotUpdatePath:
                        return CreateResult.Failure(CreateOutcome.PathUpdateError);
                    default:
                        return CreateResult.Failure(CreateOutcome.TargetInvalid, targetError: TargetError.Unsupported);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CreateResult.Failure(CreateOutcome.Unexpected, ex.Message);
            }
        }
    }
}
