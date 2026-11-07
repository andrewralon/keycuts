using System;

namespace keycuts.GUI.Themes
{
    /// <summary>
    /// WCAG 2.x contrast math on sRGB colors given as #rrggbb.
    /// </summary>
    public static class ColorContrast
    {
        public static double Ratio(string foregroundHex, string backgroundHex)
        {
            var a = RelativeLuminance(foregroundHex);
            var b = RelativeLuminance(backgroundHex);
            var lighter = Math.Max(a, b);
            var darker = Math.Min(a, b);
            return (lighter + 0.05) / (darker + 0.05);
        }

        public static double RelativeLuminance(string hex)
        {
            var (r, g, b) = Parse(hex);
            return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
        }

        static double Linearize(int channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static (int R, int G, int B) Parse(string hex)
        {
            var s = hex.TrimStart('#');
            if (s.Length != 6)
            {
                throw new FormatException($"Expected #rrggbb but got '{hex}'.");
            }

            return (Convert.ToInt32(s.Substring(0, 2), 16), Convert.ToInt32(s.Substring(2, 2), 16), Convert.ToInt32(s.Substring(4, 2), 16));
        }
    }
}
