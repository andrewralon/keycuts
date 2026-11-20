using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>Where the keycuts folder stands with a Mac's PATH (#101).</summary>
    public enum ShellPathStatus
    {
        /// <summary>Terminal finds keycuts now: the folder is on this process's PATH or in <c>/etc/paths</c>.</summary>
        OnPath,

        /// <summary>The block is in the profile, but this shell started before it: a new Terminal window finds keycuts.</summary>
        InProfile,

        NotOnPath
    }

    /// <summary><c>keycuts path [status|add|remove]</c> on macOS (Phase 4.9): what <see cref="MacShellPath"/> says and does, in words.</summary>
    public static class KeycutPathCommand
    {
        public static readonly IReadOnlyList<string> Actions = new[] { "status", "add", "remove" };

        public static ExitCode Run(string action, string folder, MacShellPath shell, TextWriter output, TextWriter error)
        {
            var profile = shell.ProfilePath == null ? null : "~/" + Path.GetFileName(shell.ProfilePath);

            switch (string.IsNullOrEmpty(action) ? "status" : action.ToLowerInvariant())
            {
                case "status":
                    var status = shell.Status(folder);
                    output.WriteLine(status switch
                    {
                        ShellPathStatus.OnPath => $"{folder} is on PATH, so Terminal runs your keycuts by name.",
                        ShellPathStatus.InProfile => $"{folder} is in {profile}. Open a new Terminal window to run your keycuts by name.",
                        _ => profile != null
                            ? $"{folder} isn't on PATH. 'keycuts path add' adds it to {profile}, so Terminal runs your keycuts by name."
                            : $"{folder} isn't on PATH. {shell.Instructions(folder)}"
                    });
                    return ExitCode.Success;

                case "add":
                    if (profile == null)
                    {
                        error.WriteLine($"keycuts: keycuts doesn't edit {shell.Shell}'s startup files. {shell.Instructions(folder)}");
                        return ExitCode.CannotUpdatePath;
                    }

                    if (!shell.Add(folder))
                    {
                        error.WriteLine($"keycuts: couldn't add {folder} to PATH in {profile}.");
                        return ExitCode.CannotUpdatePath;
                    }

                    output.WriteLine($"Added {folder} to PATH in {profile}. Open a new Terminal window to run your keycuts by name.");
                    return ExitCode.Success;

                case "remove":
                    var had = shell.ReadBlockFolder() != null;

                    if (!shell.Remove())
                    {
                        error.WriteLine($"keycuts: couldn't change {profile}.");
                        return ExitCode.CannotUpdatePath;
                    }

                    output.WriteLine(had ? $"Took keycuts out of {profile}. New Terminal windows won't find your keycuts by name." : $"keycuts hadn't added anything to {profile ?? "a startup file"}.");
                    return ExitCode.Success;

                default:
                    error.WriteLine($"keycuts: path takes {string.Join(", ", Actions)}.");
                    return ExitCode.BadArguments;
            }
        }
    }

    /// <summary>
    /// The keycuts folder on a Mac's PATH (docs/macos-port.md, Phase 4.9, #101). Only with the user's consent (<see cref="Add"/>): a
    /// marked block at the end of <c>~/.zprofile</c> (zsh, the default) or <c>~/.bash_profile</c> (bash). Other shells (fish,
    /// nushell) get <see cref="Instructions"/> instead. <see cref="Remove"/> takes out exactly what <see cref="Add"/> put in, so the
    /// rest of the profile is byte-identical. The profile is written in place, so a symlinked dotfile stays a symlink. Plain
    /// files and strings, so tests use a temp home.
    /// </summary>
    public class MacShellPath : IPathUpdater
    {
        public const string BeginMarker = "# >>> keycuts >>>";

        public const string EndMarker = "# <<< keycuts <<<";

        /// <param name="home">The home folder.</param>
        /// <param name="shell">The login shell, as <c>$SHELL</c> gives it (<c>/bin/zsh</c>); empty reads as zsh, the macOS default.</param>
        /// <param name="processPath">This process's PATH.</param>
        /// <param name="systemPaths">The entries of <c>/etc/paths</c> and <c>/etc/paths.d</c>.</param>
        public MacShellPath(string home, string shell, string processPath, IEnumerable<string> systemPaths)
        {
            Home = home.TrimEnd('/');
            Shell = string.IsNullOrWhiteSpace(shell) ? "zsh" : Path.GetFileName(shell.TrimEnd('/'));
            ProcessPath = processPath ?? "";
            SystemPaths = systemPaths?.ToList() ?? new List<string>();
        }

        /// <summary>This user and this Mac. Reads only the environment and <c>/etc/paths</c>.</summary>
        public static MacShellPath CreateDefault()
        {
            var system = new List<string>();

            try
            {
                foreach (var file in new[] { "/etc/paths" }.Concat(Directory.Exists("/etc/paths.d") ? Directory.GetFiles("/etc/paths.d").OrderBy(f => f, StringComparer.Ordinal) : Enumerable.Empty<string>()))
                {
                    if (File.Exists(file))
                    {
                        system.AddRange(File.ReadAllLines(file).Select(line => line.Trim()).Where(line => line.Length > 0));
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // The process PATH still counts
            }

            return new MacShellPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetEnvironmentVariable("SHELL"),
                Environment.GetEnvironmentVariable("PATH"), system);
        }

        public string Home { get; }

        /// <summary>The shell's name: <c>zsh</c>, <c>bash</c>, <c>fish</c>, <c>nu</c>…</summary>
        public string Shell { get; }

        public string ProcessPath { get; }

        public IReadOnlyList<string> SystemPaths { get; }

        /// <summary>The file keycuts edits for this shell, or null when it only gives <see cref="Instructions"/>.</summary>
        public string ProfilePath => Shell switch
        {
            "zsh" => Path.Combine(Home, ".zprofile"),
            "bash" => Path.Combine(Home, ".bash_profile"),
            _ => null
        };

        public ShellPathStatus Status(string folder)
        {
            if (ProcessPath.Split(':').Concat(SystemPaths).Any(entry => IsSameFolder(entry, folder)))
            {
                return ShellPathStatus.OnPath;
            }

            return ReadBlockFolder() is string inProfile && IsSameFolder(inProfile, folder) ? ShellPathStatus.InProfile : ShellPathStatus.NotOnPath;
        }

        /// <summary>What to add by hand, for a shell keycuts doesn't edit the profile of; null for zsh and bash.</summary>
        public string Instructions(string folder)
        {
            return Shell switch
            {
                "zsh" or "bash" => null,
                "fish" => $"Run: fish_add_path {ShFormat.Quote(folder)}",
                "nu" => $"Add to your config.nu: $env.path ++= [{ShFormat.Quote(folder)}]",
                _ => $"Add {folder} to PATH in your {Shell} startup file."
            };
        }

        /// <summary>
        /// Puts the block for <paramref name="folder"/> at the end of the profile, replacing one already there. False for a
        /// shell with only <see cref="Instructions"/>, a folder PATH can't hold (a <c>:</c> or a line break), or a write that fails.
        /// </summary>
        public bool Add(string folder)
        {
            if (ProfilePath == null || folder.IndexOfAny(new[] { ':', '\n', '\r' }) >= 0)
            {
                return false;
            }

            return Rewrite(text => WithoutBlock(text) + Block(folder));
        }

        /// <summary>Takes the block out, leaving the rest of the profile as it was. True when there was none to take.</summary>
        public bool Remove()
        {
            return ProfilePath == null || !File.Exists(ProfilePath) || Rewrite(WithoutBlock);
        }

        /// <summary>
        /// For a folder change (Settings, <c>-o</c>): moves an existing block to the new folder. Without one, PATH was never
        /// agreed to, so nothing is added. Never throws.
        /// </summary>
        public bool AddToOrReplace(string oldPath, string newPath)
        {
            return ReadBlockFolder() == null || IsSameFolder(ReadBlockFolder(), newPath) || Add(newPath);
        }

        /// <summary>
        /// The block's text: a blank line first (taken out with it), the markers and one <c>export</c>, a folder under home written
        /// with <c>$HOME</c>. Inside double quotes, so <c>\ " $</c> and backticks in the folder are escaped.
        /// </summary>
        public string Block(string folder)
        {
            var spelled = PathVariables.Substitute(folder.TrimEnd('/'), PathVariables.ForMac(Home));
            var value = spelled.StartsWith("$HOME", StringComparison.Ordinal) ? "$HOME" + ShFormat.EscapeForDoubleQuotes(spelled.Substring(5)) : ShFormat.EscapeForDoubleQuotes(spelled);

            return "\n" + BeginMarker + "\n" +
                "# Added by keycuts so Terminal finds your keycuts. \"keycuts path remove\" takes it out.\n" +
                $"export PATH=\"$PATH:{value}\"\n" +
                EndMarker + "\n";
        }

        /// <summary>The folder the profile's block adds, as written (<c>$HOME</c> expanded), or null when there's no block.</summary>
        public string ReadBlockFolder()
        {
            var text = ReadProfile();

            if (text == null || !FindBlock(text, out var begin, out var end))
            {
                return null;
            }

            const string export = "export PATH=\"$PATH:";

            foreach (var line in text.Substring(begin, end - begin).Split('\n'))
            {
                if (line.StartsWith(export, StringComparison.Ordinal) && line.EndsWith("\"", StringComparison.Ordinal))
                {
                    var words = ShFormat.SplitWords("\"" + line.Substring(export.Length));
                    return words?.Count == 1 ? PathVariables.ExpandHome(words[0], Home) : null;
                }
            }

            return "";
        }

        /// <summary><paramref name="text"/> with every keycuts block taken out, with the blank line <see cref="Block"/> puts before it.</summary>
        public static string WithoutBlock(string text)
        {
            while (FindBlock(text, out var begin, out var end))
            {
                // The blank line Add put before it
                if (begin > 0)
                {
                    begin--;
                }

                text = text.Remove(begin, end - begin);
            }

            return text;
        }

        /// <summary>The first block: from its begin marker's line through its end marker's line and line break. Markers count only as whole lines.</summary>
        private static bool FindBlock(string text, out int begin, out int end)
        {
            begin = -1;
            end = -1;

            for (var from = 0; (begin = text.IndexOf(BeginMarker + "\n", from, StringComparison.Ordinal)) >= 0; from = begin + 1)
            {
                if (begin > 0 && text[begin - 1] != '\n')
                {
                    continue;
                }

                var close = text.IndexOf("\n" + EndMarker, begin, StringComparison.Ordinal);
                var after = close + 1 + EndMarker.Length;

                if (close >= 0 && (after == text.Length || text[after] == '\n'))
                {
                    end = Math.Min(after + 1, text.Length);
                    return true;
                }
            }

            return false;
        }

        private string ReadProfile()
        {
            try
            {
                return ProfilePath != null && File.Exists(ProfilePath) ? File.ReadAllText(ProfilePath) : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private bool Rewrite(Func<string, string> change)
        {
            try
            {
                var text = File.Exists(ProfilePath) ? File.ReadAllText(ProfilePath) : "";
                var updated = change(text);

                if (updated != text)
                {
                    File.WriteAllText(ProfilePath, updated);
                }

                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool IsSameFolder(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
                string.Equals(left.Trim().TrimEnd('/'), right.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
