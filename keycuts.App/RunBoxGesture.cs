using System;
using System.Collections.Generic;
using Avalonia.Input;
using keycuts.Common;

namespace keycuts.GUI
{
    /// <summary>The Run box's shortcut as a menu key, so the menu bar's Run… shows it the way macOS shows a key equivalent.</summary>
    public static class RunBoxGesture
    {
        private static readonly Dictionary<string, Key> Punctuation = new()
        {
            ["="] = Key.OemPlus,
            ["-"] = Key.OemMinus,
            ["]"] = Key.OemCloseBrackets,
            ["["] = Key.OemOpenBrackets,
            ["'"] = Key.OemQuotes,
            [";"] = Key.OemSemicolon,
            ["\\"] = Key.OemPipe,
            [","] = Key.OemComma,
            ["/"] = Key.OemQuestion,
            ["."] = Key.OemPeriod,
            ["`"] = Key.OemTilde
        };

        /// <summary>Null when there is no shortcut or its key has no menu equivalent.</summary>
        public static KeyGesture? From(RunBoxHotkey? hotkey)
        {
            if (hotkey == null || !TryKey(hotkey.Key, out var key))
            {
                return null;
            }

            var modifiers = (hotkey.Command ? KeyModifiers.Meta : 0) | (hotkey.Shift ? KeyModifiers.Shift : 0)
                | (hotkey.Option ? KeyModifiers.Alt : 0) | (hotkey.Control ? KeyModifiers.Control : 0);
            return new KeyGesture(key, modifiers);
        }

        private static bool TryKey(string name, out Key key)
        {
            if (Punctuation.TryGetValue(name, out key))
            {
                return true;
            }

            if (name.Length == 1 && char.IsDigit(name[0]))
            {
                return Enum.TryParse("D" + name, out key);
            }

            return Enum.TryParse(name, true, out key);
        }
    }
}
