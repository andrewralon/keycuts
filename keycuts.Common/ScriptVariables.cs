using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace keycuts.Common
{
    /// <summary>A variable a resolved target used: its value at the launch line, or unknown (it stays <c>%NAME%</c>).</summary>
    public sealed record ScriptVariable(string Name, string Value, bool Known);

    /// <summary>
    /// A keycut's target with its script's variables filled in (#212). Display only: it is never the target, never written, and
    /// never compared with the file.
    /// </summary>
    public sealed record ResolvedTarget(string Text, IReadOnlyList<ScriptVariable> Variables, bool AssumesNoArguments);

    /// <summary>
    /// Fills in a hand-written keycut's target from the <c>SET</c> lines before its launch line (#212), so the Manager can show
    /// <c>https://github.com/GolfNowEng</c> rather than <c>%BASE%%QUERY%</c>. The lines are read the way cmd would run them, as far as
    /// that is safe, and nothing is ever run:
    /// <list type="bullet">
    /// <item><c>SET NAME=value</c>, <c>SET "NAME=value"</c> and <c>SET NAME=</c>. In a value <c>%%</c> is <c>%</c> and, outside quotes,
    /// <c>^&amp;</c> is <c>&amp;</c>; <c>%NAME%</c> is the value set before, else the environment's.</item>
    /// <item>The no-arguments case, which is what Win+R runs: <c>%1</c>, <c>%~1</c> and <c>%*</c> are empty, and <c>IF [NOT] "%~1"==""</c>
    /// (any argument number, with or without the <c>~</c>) is decided on that, its block or command taken or skipped.</item>
    /// <item>Anything else is unknown, and a variable it might set stays <c>%NAME%</c>, so nothing is invented: a <c>SET</c> in any
    /// other <c>IF</c>, <c>SET /A</c> and <c>SET /P</c>, a <c>!NAME!</c> value under delayed expansion, <c>%0</c>, and a substring or
    /// replace (<c>%NAME:~0,3%</c>). After a label, <c>GOTO</c>, <c>CALL</c>, <c>FOR</c> or <c>ENDLOCAL</c> every script variable is unknown.</item>
    /// </list>
    /// A target is only resolved when it names a variable the script sets: one that only names environment variables (a folder
    /// saved as <c>%USERPROFILE%\…</c>, #31) is shown as written.
    /// </summary>
    public static class ScriptVariables
    {
        private static readonly Regex SetLine = new Regex(@"^SET(?<switch>\s+/[AP]\b)?\s+(?<rest>.*)$", RegexOptions.IgnoreCase);

        private static readonly Regex AnySet = new Regex(@"\bSET(\s+/[AP])?\s+""?(?<name>[^=""\s]+)=", RegexOptions.IgnoreCase);

        // IF [NOT] "%~1"=="" and IF "%1"=="" (either side), then the rest: a command, or a block opened with (
        private static readonly Regex NoArgumentIf = new Regex(
            @"^IF\s+(?<not>NOT\s+)?(?:""%~?(?:\d|\*)""\s*==\s*""""|""""\s*==\s*""%~?(?:\d|\*)"")\s*(?<rest>.*)$", RegexOptions.IgnoreCase);

        private static readonly Regex StopsEvaluation = new Regex(@"^(?::|GOTO\b|CALL\b|FOR\b|ENDLOCAL\b)", RegexOptions.IgnoreCase);

        private static readonly Regex VariableReference = new Regex(@"%(?<name>[A-Za-z_][^%:\s]*)%");

        private sealed class Value
        {
            public string Text;
            public bool Known;
        }

        private sealed class State
        {
            public Dictionary<string, Value> Variables = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
            public bool Stopped;
            public bool DelayedExpansion;
            public bool UsedNoArguments;
            public Func<string, string> Environment;
        }

        /// <summary>
        /// The target with the script's variables filled in, or null when there is nothing to fill in: the target names no variable
        /// the lines before the launch line set, or filling in changes nothing.
        /// </summary>
        /// <param name="lines">The keycut's lines.</param>
        /// <param name="target">The target as the parser read it from the launch line.</param>
        /// <param name="environment">An environment variable's value, or null when it isn't set.</param>
        public static ResolvedTarget Resolve(IReadOnlyList<string> lines, string target, Func<string, string> environment)
        {
            var written = (target ?? "").Trim();
            if (lines == null || written.IndexOf('%') < 0)
            {
                return null;
            }

            var launch = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (ShortcutFile.IsLaunchLine(lines[i]))
                {
                    launch = i;
                    break;
                }
            }

            if (launch < 0)
            {
                return null;
            }

            var state = new State { Environment = environment ?? (_ => null) };
            Run(lines, 0, launch, true, false, state, out _);

            // Only a target that names a variable set before the launch line is the script's to fill in
            var named = VariableReference.Matches(written).Select(m => m.Groups["name"].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var fromScript = named.Where(name => state.Variables.ContainsKey(name)).ToList();
            if (fromScript.Count == 0)
            {
                return null;
            }

            var text = Unescape(Expand(written, state.Variables, state)).Trim();
            if (string.Equals(text, written, StringComparison.Ordinal))
            {
                return null;
            }

            var used = fromScript.Select(name => state.Variables[name])
                .Zip(fromScript, (value, name) => new ScriptVariable(name, value.Known ? value.Text : null, value.Known))
                .ToList();

            return new ResolvedTarget(text, used, state.UsedNoArguments);
        }

        /// <summary>Runs lines [start, end). <paramref name="active"/>: true runs them, false skips them, null means maybe.</summary>
        private static void Run(IReadOnlyList<string> lines, int start, int end, bool? active, bool inBlock, State state, out int next)
        {
            // cmd expands %…% in a whole block before running it, so a block sees the values from before it
            var snapshot = Copy(state.Variables);
            var expandFrom = inBlock ? snapshot : state.Variables;
            var i = start;

            while (i < end)
            {
                var line = Statement(lines[i]);
                i++;

                if (line.Length == 0 || IsComment(line))
                {
                    continue;
                }

                if (line.StartsWith(")", StringComparison.Ordinal))
                {
                    // The caller's block ends here
                    next = i - 1;
                    return;
                }

                if (StopsEvaluation.IsMatch(line))
                {
                    if (active != false)
                    {
                        Stop(state);
                    }

                    continue;
                }

                if (line.StartsWith("SETLOCAL", StringComparison.OrdinalIgnoreCase))
                {
                    if (active != false && line.IndexOf("ENABLEDELAYEDEXPANSION", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        state.DelayedExpansion = true;
                    }

                    continue;
                }

                if (line.StartsWith("IF", StringComparison.OrdinalIgnoreCase) && line.Length > 2 && char.IsWhiteSpace(line[2]))
                {
                    i = If(lines, line, i, end, active, state, expandFrom);
                    continue;
                }

                Command(line, active, state, expandFrom);
            }

            next = end;
        }

        /// <summary>An IF line, and its block when it opens one. Returns the index after everything it covered.</summary>
        private static int If(IReadOnlyList<string> lines, string line, int i, int end, bool? active, State state, Dictionary<string, Value> expandFrom)
        {
            bool? taken;
            string rest;
            var match = NoArgumentIf.Match(line);

            if (match.Success)
            {
                state.UsedNoArguments = true;
                taken = match.Groups["not"].Success ? false : true;
                rest = match.Groups["rest"].Value.Trim();
            }
            else
            {
                // Some other condition: whatever it covers may or may not run. Its command or block is whatever follows the
                // condition, found by its opening parenthesis, or, on one line, by the SET in it.
                taken = null;
                var open = line.EndsWith("(", StringComparison.Ordinal);
                rest = open ? "(" : line;
            }

            var thenActive = active == false ? false : active == null || taken == null ? (bool?)null : taken.Value;
            var elseActive = active == false ? false : active == null || taken == null ? (bool?)null : !taken.Value;

            if (rest == "(")
            {
                Run(lines, i, end, thenActive, true, state, out var close);
                if (close >= end)
                {
                    return end;
                }

                // ) or ) ELSE (
                var closing = Statement(lines[close]);
                var after = close + 1;
                if (Regex.IsMatch(closing, @"^\)\s*ELSE\s*\($", RegexOptions.IgnoreCase))
                {
                    Run(lines, after, end, elseActive, true, state, out var closeElse);
                    return Math.Min(end, closeElse + 1);
                }

                return after;
            }

            if (rest.Length > 0)
            {
                if (taken == null)
                {
                    // A one-line IF with a condition we don't decide: the SET in it makes its variable unknown
                    foreach (Match set in AnySet.Matches(rest))
                    {
                        Unknown(state, set.Groups["name"].Value, thenActive);
                    }
                }
                else
                {
                    Command(rest, thenActive, state, expandFrom);
                }
            }

            return i;
        }

        /// <summary>One command outside an IF; only SET matters.</summary>
        private static void Command(string line, bool? active, State state, Dictionary<string, Value> expandFrom)
        {
            var set = SetLine.Match(line);
            if (!set.Success)
            {
                return;
            }

            var rest = set.Groups["rest"].Value;
            string name, value;
            bool quoted;

            if (rest.StartsWith("\"", StringComparison.Ordinal))
            {
                // SET "NAME=value": the value ends at the last quote
                var last = rest.LastIndexOf('"');
                var inner = last > 0 ? rest.Substring(1, last - 1) : rest.Substring(1);
                var equals = inner.IndexOf('=');
                if (equals <= 0)
                {
                    return;
                }

                name = inner.Substring(0, equals);
                value = inner.Substring(equals + 1);
                quoted = true;
            }
            else
            {
                var equals = rest.IndexOf('=');
                if (equals <= 0)
                {
                    return;
                }

                name = rest.Substring(0, equals);
                value = rest.Substring(equals + 1);
                quoted = false;
            }

            name = name.Trim();
            if (name.Length == 0 || name.IndexOfAny(new[] { ' ', '%', '!' }) >= 0)
            {
                return;
            }

            if (active == false)
            {
                return;
            }

            if (active == null || state.Stopped || set.Groups["switch"].Success || (state.DelayedExpansion && value.IndexOf('!') >= 0))
            {
                Unknown(state, name, active);
                return;
            }

            var expanded = Expand(value, expandFrom, state);
            state.Variables[name] = new Value { Text = quoted ? expanded : Unescape(expanded), Known = true };
        }

        /// <summary>
        /// cmd's %-expansion for display: <c>%%</c> is <c>%</c>, an argument is empty (Win+R passes none), a known variable is its
        /// value, an unknown one stays <c>%NAME%</c>, and so does anything we don't expand (<c>%0</c>, <c>%NAME:~0,3%</c>).
        /// </summary>
        private static string Expand(string text, Dictionary<string, Value> variables, State state)
        {
            var result = new StringBuilder(text.Length);
            var i = 0;

            while (i < text.Length)
            {
                var c = text[i];
                if (c != '%')
                {
                    result.Append(c);
                    i++;
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == '%')
                {
                    result.Append('%');
                    i += 2;
                    continue;
                }

                // %1, %~1, %~dp1, %*: arguments. %0 is the script itself, which a display can't name, so it stays.
                var argument = Regex.Match(text.Substring(i), @"^%(?:~[a-zA-Z]*)?(?<n>\d|\*)");
                if (argument.Success)
                {
                    if (argument.Groups["n"].Value == "0")
                    {
                        result.Append(argument.Value);
                    }
                    else
                    {
                        state.UsedNoArguments = true;
                    }

                    i += argument.Length;
                    continue;
                }

                var close = text.IndexOf('%', i + 1);
                if (close < 0)
                {
                    result.Append(text, i, text.Length - i);
                    break;
                }

                var name = text.Substring(i + 1, close - i - 1);
                var reference = text.Substring(i, close - i + 1);

                if (name.Length == 0 || name.IndexOf(':') >= 0 || name.IndexOfAny(new[] { ' ', '\t' }) >= 0)
                {
                    result.Append(reference);
                }
                else if (variables.TryGetValue(name, out var value))
                {
                    result.Append(value.Known ? value.Text : reference);
                }
                else
                {
                    result.Append(state.Environment(name) ?? reference);
                }

                i = close + 1;
            }

            return result.ToString();
        }

        /// <summary>Carets outside quotes: <c>^&amp;</c> is <c>&amp;</c>, <c>^^</c> is <c>^</c>. Inside quotes a caret is itself.</summary>
        private static string Unescape(string text)
        {
            var result = new StringBuilder(text.Length);
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    result.Append(c);
                }
                else if (c == '^' && !inQuotes && i + 1 < text.Length)
                {
                    result.Append(text[++i]);
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        private static void Unknown(State state, string name, bool? active)
        {
            if (active != false && !string.IsNullOrWhiteSpace(name))
            {
                state.Variables[name.Trim()] = new Value { Known = false };
            }
        }

        private static void Stop(State state)
        {
            state.Stopped = true;
            foreach (var value in state.Variables.Values)
            {
                value.Known = false;
            }
        }

        private static Dictionary<string, Value> Copy(Dictionary<string, Value> variables)
        {
            return variables.ToDictionary(pair => pair.Key, pair => new Value { Text = pair.Value.Text, Known = pair.Value.Known }, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>A line as a statement: trimmed, with cmd's leading <c>@</c> dropped.</summary>
        private static string Statement(string line)
        {
            return (line ?? "").Trim().TrimStart('@').Trim();
        }

        private static bool IsComment(string line)
        {
            return line.StartsWith("::", StringComparison.Ordinal) ||
                Regex.IsMatch(line, @"^REM(\s|$)", RegexOptions.IgnoreCase);
        }
    }
}
