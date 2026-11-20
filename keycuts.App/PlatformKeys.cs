using System;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace keycuts.GUI
{
    /// <summary>
    /// The app's keys on each platform (docs/styleguide.md, Keys): ⌘ on macOS where Windows uses Ctrl, and the menus' gestures and
    /// the tooltips' key names to match. Avalonia shows a gesture in the platform's own way (⌃ for Control on a Mac), so a gesture
    /// must carry the platform's modifier, not Control.
    /// </summary>
    public static class PlatformKeys
    {
        public static readonly bool IsMac = OperatingSystem.IsMacOS();

        /// <summary>⌘ on macOS, Ctrl elsewhere.</summary>
        public static readonly KeyModifiers Command = IsMac ? KeyModifiers.Meta : KeyModifiers.Control;

        /// <summary><see cref="Command"/> as raw input, for tests that press keys.</summary>
        public static readonly RawInputModifiers RawCommand = IsMac ? RawInputModifiers.Meta : RawInputModifiers.Control;

        public static readonly KeyGesture CopyTarget = new(Key.C, Command);

        public static readonly KeyGesture Edit = new(Key.E, Command);

        public static readonly KeyGesture EditInEditor = new(Key.E, Command | KeyModifiers.Shift);

        public static readonly KeyGesture ShowFile = new(Key.O, Command | KeyModifiers.Shift);

        /// <summary>Delete on Windows; ⌘⌫ on macOS, as Finder moves to the Trash (forward delete works too, a plain ⌫ doesn't).</summary>
        public static readonly KeyGesture Delete = IsMac ? new(Key.Back, KeyModifiers.Meta) : new(Key.Delete);

        /// <summary>A key in a tooltip or an automation name, as the platform writes it: <c>Ctrl+1</c>, <c>⌘1</c>.</summary>
        public static string Name(string key, bool shift = false)
        {
            return IsMac ? (shift ? "⇧⌘" : "⌘") + key : "Ctrl+" + (shift ? "Shift+" : "") + key;
        }

        /// <summary>A tooltip with its key in parentheses: "creator (Ctrl+1)", "creator (⌘1)".</summary>
        public static string Tip(string text, string key)
        {
            return text + " (" + Name(key) + ")";
        }
    }
}
