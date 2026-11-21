using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace keycuts.GUI.Themes
{
    /// <summary>The Windows system colors the high-contrast palette uses (WPF's <c>SystemColors.*ColorKey</c>).</summary>
    public enum SystemColorName
    {
        Window,
        WindowText,
        HotTrack,
        GrayText,
        Control,
        Highlight,
        HighlightText
    }

    /// <summary>
    /// Avalonia has no <c>SystemColors</c>, so a high-contrast brush names the system color it takes
    /// (<c>themes:SystemColor.Name="WindowText"</c>) and <see cref="Fill"/> sets it, when high contrast comes on and whenever
    /// Windows reports a change. The brushes are the palette's own objects, so everything that uses them follows.
    /// </summary>
    public sealed class SystemColor
    {
        public static readonly AttachedProperty<SystemColorName?> NameProperty =
            AvaloniaProperty.RegisterAttached<SystemColor, SolidColorBrush, SystemColorName?>("Name");

        SystemColor()
        {
        }

        public static SystemColorName? GetName(SolidColorBrush brush)
        {
            return brush.GetValue(NameProperty);
        }

        public static void SetName(SolidColorBrush brush, SystemColorName? value)
        {
            brush.SetValue(NameProperty, value);
        }

        /// <summary>Sets every named brush in <paramref name="palette"/> to its system color, as <paramref name="read"/> gives it.</summary>
        public static void Fill(IResourceDictionary palette, Func<SystemColorName, Color> read)
        {
            // By key: Values holds deferred entries that aren't built yet, and TryGetValue builds them
            foreach (var key in palette.Keys.ToList())
            {
                if (palette.TryGetValue(key, out var value) && value is SolidColorBrush brush && GetName(brush) is { } name)
                {
                    brush.Color = read(name);
                }
            }
        }

        /// <summary>The color Windows uses now: the high-contrast scheme's while high contrast is on.</summary>
        [SupportedOSPlatform("windows")]
        public static Color ReadWindows(SystemColorName name)
        {
            var colorRef = GetSysColor(Index(name));
            return Color.FromRgb((byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF));
        }

        /// <summary>
        /// macOS has no system color table, so its high contrast (Increase contrast) takes fixed colors (docs/styleguide.md, In
        /// Avalonia): black on white with the light appearance, white on black with the dark one, and the accent for links and
        /// the selection, at 4.5:1 or better on its background.
        /// </summary>
        public static Color ReadMac(SystemColorName name, bool dark)
        {
            var accent = dark ? Color.Parse("#6a9eff") : Color.Parse("#2563eb");
            return name switch
            {
                SystemColorName.Window or SystemColorName.Control => dark ? Colors.Black : Colors.White,
                SystemColorName.WindowText => dark ? Colors.White : Colors.Black,
                SystemColorName.GrayText => dark ? Color.Parse("#a0a0a0") : Color.Parse("#5e5e5e"),
                SystemColorName.HotTrack or SystemColorName.Highlight => accent,
                SystemColorName.HighlightText => dark ? Colors.Black : Colors.White,
                _ => dark ? Colors.White : Colors.Black
            };
        }

        /// <summary>GetSysColor's index for each name: the ones WPF's <c>SystemColors</c> read.</summary>
        public static int Index(SystemColorName name)
        {
            return name switch
            {
                SystemColorName.Window => 5,
                SystemColorName.WindowText => 8,
                SystemColorName.HotTrack => 26,
                SystemColorName.GrayText => 17,
                SystemColorName.Control => 15,
                SystemColorName.Highlight => 13,
                SystemColorName.HighlightText => 14,
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }

        [DllImport("user32.dll")]
        [SupportedOSPlatform("windows")]
        static extern uint GetSysColor(int index);
    }
}
