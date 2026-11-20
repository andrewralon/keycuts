using System;
using System.Collections.Generic;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// The Run box's global shortcut on macOS (Phase 6.2, docs/styleguide.md, Decisions): a key and its modifiers, stored as text
    /// (<c>Shift+Cmd+Space</c>), shown as macOS writes it (<c>⇧⌘Space</c>), and registered with Carbon by key code. macOS 15 and
    /// later take a global shortcut only with ⌘ or ⌃ (<see cref="IsAllowed"/>). Pure, so it's tested everywhere.
    /// </summary>
    public sealed record RunBoxHotkey(string Key, bool Control, bool Option, bool Shift, bool Command)
    {
        /// <summary>⇧⌘Space (plan correction 2: not a macOS default, one key from Spotlight).</summary>
        public static readonly RunBoxHotkey Default = new("Space", false, false, true, true);

        // Carbon's modifier bits (Events.h): cmdKey, shiftKey, optionKey, controlKey
        public const uint CarbonCommand = 0x100, CarbonShift = 0x200, CarbonOption = 0x800, CarbonControl = 0x1000;

        /// <summary>The keys a shortcut can use and their virtual key codes (kVK_* in Events.h), for any keyboard layout.</summary>
        public static readonly IReadOnlyDictionary<string, uint> KeyCodes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 0x00, ["S"] = 0x01, ["D"] = 0x02, ["F"] = 0x03, ["H"] = 0x04, ["G"] = 0x05, ["Z"] = 0x06, ["X"] = 0x07,
            ["C"] = 0x08, ["V"] = 0x09, ["B"] = 0x0B, ["Q"] = 0x0C, ["W"] = 0x0D, ["E"] = 0x0E, ["R"] = 0x0F, ["Y"] = 0x10,
            ["T"] = 0x11, ["1"] = 0x12, ["2"] = 0x13, ["3"] = 0x14, ["4"] = 0x15, ["6"] = 0x16, ["5"] = 0x17, ["="] = 0x18,
            ["9"] = 0x19, ["7"] = 0x1A, ["-"] = 0x1B, ["8"] = 0x1C, ["0"] = 0x1D, ["]"] = 0x1E, ["O"] = 0x1F, ["U"] = 0x20,
            ["["] = 0x21, ["I"] = 0x22, ["P"] = 0x23, ["L"] = 0x25, ["J"] = 0x26, ["'"] = 0x27, ["K"] = 0x28, [";"] = 0x29,
            ["\\"] = 0x2A, [","] = 0x2B, ["/"] = 0x2C, ["N"] = 0x2D, ["M"] = 0x2E, ["."] = 0x2F, ["Space"] = 0x31, ["`"] = 0x32,
            ["F1"] = 0x7A, ["F2"] = 0x78, ["F3"] = 0x63, ["F4"] = 0x76, ["F5"] = 0x60, ["F6"] = 0x61, ["F7"] = 0x62,
            ["F8"] = 0x64, ["F9"] = 0x65, ["F10"] = 0x6D, ["F11"] = 0x67, ["F12"] = 0x6F
        };

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

        /// <summary>Reads <see cref="Stored"/> back; null for empty text (no shortcut) or anything it can't read.</summary>
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
            if (key == null || modifiers.Any(m => m is not ("ctrl" or "option" or "shift" or "cmd")))
            {
                return null;
            }

            return new RunBoxHotkey(key, modifiers.Contains("ctrl"), modifiers.Contains("option"), modifiers.Contains("shift"), modifiers.Contains("cmd"));
        }
    }
}
