using System;
using System.Collections.Generic;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// The Run box's global shortcut (Phase 6.2, docs/styleguide.md, Decisions): a key and its modifiers, stored as text
    /// (<c>Shift+Cmd+Space</c>), shown as macOS writes it (<c>⇧⌘Space</c>), and registered with Carbon by key code. macOS 15 and
    /// later take a global shortcut only with ⌘ or ⌃ (<see cref="IsAllowed"/>). On Windows (issue #306) the same record holds
    /// Ctrl, Alt (<see cref="Option"/>), Shift and Win (<see cref="Command"/>), is written <c>Ctrl+Alt+Space</c>
    /// (<see cref="WindowsDisplay"/>) and is registered by virtual key (<c>RegisterHotKey</c>). Pure, so it's tested everywhere.
    /// </summary>
    public sealed record RunBoxHotkey(string Key, bool Control, bool Option, bool Shift, bool Command)
    {
        /// <summary>⇧⌘Space (plan correction 2: not a macOS default, one key from Spotlight).</summary>
        public static readonly RunBoxHotkey Default = new("Space", false, false, true, true);

        /// <summary>Ctrl+Alt+Space on Windows (issue #306): Win+R and Win+Space are Windows' own, Alt+Space is the window menu.</summary>
        public static readonly RunBoxHotkey WindowsDefault = new("Space", true, true, false, false);

        /// <summary>The shortcut a start with nothing stored gets on this system.</summary>
        public static RunBoxHotkey DefaultForThisSystem => OperatingSystem.IsWindows() ? WindowsDefault : Default;

        // RegisterHotKey's modifier bits (winuser.h): MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN, MOD_NOREPEAT
        public const uint WindowsAlt = 0x1, WindowsControl = 0x2, WindowsShift = 0x4, WindowsWin = 0x8, WindowsNoRepeat = 0x4000;

        // Carbon's modifier bits (Events.h): cmdKey, shiftKey, optionKey, controlKey
        public const uint CarbonCommand = 0x100, CarbonShift = 0x200, CarbonOption = 0x800, CarbonControl = 0x1000;

        /// <summary>The keys a shortcut can use and their virtual key codes (kVK_* in Events.h), for any keyboard layout.</summary>
        public static readonly IReadOnlyDictionary<string, uint> KeyCodes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 0x00,
            ["S"] = 0x01,
            ["D"] = 0x02,
            ["F"] = 0x03,
            ["H"] = 0x04,
            ["G"] = 0x05,
            ["Z"] = 0x06,
            ["X"] = 0x07,
            ["C"] = 0x08,
            ["V"] = 0x09,
            ["B"] = 0x0B,
            ["Q"] = 0x0C,
            ["W"] = 0x0D,
            ["E"] = 0x0E,
            ["R"] = 0x0F,
            ["Y"] = 0x10,
            ["T"] = 0x11,
            ["1"] = 0x12,
            ["2"] = 0x13,
            ["3"] = 0x14,
            ["4"] = 0x15,
            ["6"] = 0x16,
            ["5"] = 0x17,
            ["="] = 0x18,
            ["9"] = 0x19,
            ["7"] = 0x1A,
            ["-"] = 0x1B,
            ["8"] = 0x1C,
            ["0"] = 0x1D,
            ["]"] = 0x1E,
            ["O"] = 0x1F,
            ["U"] = 0x20,
            ["["] = 0x21,
            ["I"] = 0x22,
            ["P"] = 0x23,
            ["L"] = 0x25,
            ["J"] = 0x26,
            ["'"] = 0x27,
            ["K"] = 0x28,
            [";"] = 0x29,
            ["\\"] = 0x2A,
            [","] = 0x2B,
            ["/"] = 0x2C,
            ["N"] = 0x2D,
            ["M"] = 0x2E,
            ["."] = 0x2F,
            ["Space"] = 0x31,
            ["`"] = 0x32,
            ["F1"] = 0x7A,
            ["F2"] = 0x78,
            ["F3"] = 0x63,
            ["F4"] = 0x76,
            ["F5"] = 0x60,
            ["F6"] = 0x61,
            ["F7"] = 0x62,
            ["F8"] = 0x64,
            ["F9"] = 0x65,
            ["F10"] = 0x6D,
            ["F11"] = 0x67,
            ["F12"] = 0x6F
        };

        /// <summary>The same keys' Windows virtual-key codes (VK_*): letters and digits by their character, then the OEM keys of a US layout.</summary>
        public static readonly IReadOnlyDictionary<string, uint> WindowsKeyCodes = BuildWindowsKeyCodes();

        private static IReadOnlyDictionary<string, uint> BuildWindowsKeyCodes()
        {
            var codes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
            {
                ["Space"] = 0x20,
                [";"] = 0xBA,
                ["="] = 0xBB,
                [","] = 0xBC,
                ["-"] = 0xBD,
                ["."] = 0xBE,
                ["/"] = 0xBF,
                ["`"] = 0xC0,
                ["["] = 0xDB,
                ["\\"] = 0xDC,
                ["]"] = 0xDD,
                ["'"] = 0xDE
            };

            for (var letter = 'A'; letter <= 'Z'; letter++)
            {
                codes[letter.ToString()] = letter;
            }

            for (var digit = '0'; digit <= '9'; digit++)
            {
                codes[digit.ToString()] = digit;
            }

            for (var number = 1; number <= 12; number++)
            {
                codes["F" + number] = (uint)(0x6F + number);
            }

            return codes;
        }

        /// <summary>Windows takes a global shortcut with any modifier but Shift alone (that would take a capital letter), on a key it has a code for.</summary>
        public bool IsAllowedOnWindows => (Control || Option || Command) && WindowsKeyCodes.ContainsKey(Key);

        public uint WindowsKeyCode => WindowsKeyCodes.TryGetValue(Key, out var code) ? code : uint.MaxValue;

        /// <summary>For <c>RegisterHotKey</c>; holding the keys down doesn't repeat the shortcut.</summary>
        public uint WindowsModifiers =>
            (Option ? WindowsAlt : 0) | (Control ? WindowsControl : 0) | (Shift ? WindowsShift : 0) | (Command ? WindowsWin : 0) | WindowsNoRepeat;

        /// <summary>As Windows writes it: <c>Ctrl+Alt+Shift+Win+Space</c>.</summary>
        public string WindowsDisplay =>
            string.Join("+", new[] { Control ? "Ctrl" : null, Option ? "Alt" : null, Shift ? "Shift" : null, Command ? "Win" : null, Key }.Where(p => p != null));

        /// <summary>macOS takes a global shortcut only with ⌘ or ⌃, and only on a key it has a code for here.</summary>
        public bool IsAllowed => (Command || Control) && KeyCodes.ContainsKey(Key);

        public uint KeyCode => KeyCodes.TryGetValue(Key, out var code) ? code : uint.MaxValue;

        public uint CarbonModifiers =>
            (Command ? CarbonCommand : 0) | (Shift ? CarbonShift : 0) | (Option ? CarbonOption : 0) | (Control ? CarbonControl : 0);

        /// <summary>As macOS writes it: the symbols in the order ⌃ ⌥ ⇧ ⌘, then the key (<c>⇧⌘Space</c>).</summary>
        public string Display =>
            (Control ? "⌃" : "") + (Option ? "⌥" : "") + (Shift ? "⇧" : "") + (Command ? "⌘" : "") + Key;

        /// <summary>As the settings file keeps it: <c>Shift+Cmd+Space</c>.</summary>
        public string Stored =>
            string.Join("+", new[] { Control ? "Ctrl" : null, Option ? "Option" : null, Shift ? "Shift" : null, Command ? "Cmd" : null, Key }.Where(p => p != null));

        /// <summary>Reads <see cref="Stored"/> (or <see cref="WindowsDisplay"/>'s form, which Windows stores) back; null for empty text (no shortcut) or anything it can't read.</summary>
        public static RunBoxHotkey Parse(string text)
        {
            var parts = (text ?? "").Split('+', StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts[^1].Length == 0)
            {
                return null;
            }

            // "+" as the key itself would split away; no shortcut uses it
            var key = KeyCodes.Keys.FirstOrDefault(k => string.Equals(k, parts[^1], StringComparison.OrdinalIgnoreCase));
            var modifiers = parts.Take(parts.Length - 1).Select(p => p.ToLowerInvariant()).ToList();
            if (key == null || modifiers.Any(m => m is not ("ctrl" or "option" or "alt" or "shift" or "cmd" or "win")))
            {
                return null;
            }

            return new RunBoxHotkey(key, modifiers.Contains("ctrl"), modifiers.Contains("option") || modifiers.Contains("alt"), modifiers.Contains("shift"), modifiers.Contains("cmd") || modifiers.Contains("win"));
        }
    }
}
