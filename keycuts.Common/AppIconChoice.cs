using System;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// The app icon chosen in Settings. The first two follow the theme (light or dark); the rest never change.
    /// BlackAndWhite comes first, so an unset value is the default.
    /// </summary>
    public enum AppIconChoice
    {
        /// <summary>Porcelain in light mode, Onyx in dark mode.</summary>
        BlackAndWhite,

        /// <summary>Cobalt in light mode, Twilight in dark mode.</summary>
        Accent,

        Porcelain,

        Onyx,

        Cobalt,

        Twilight,

        /// <summary>keycuts' original Team Ralon icon.</summary>
        Classic
    }

    public static class AppIconChoices
    {
        /// <summary>
        /// Reads the registry value: a choice's name in any case. A missing or unknown value, a number included, means the
        /// default, so a bad value can't stop the app from starting.
        /// </summary>
        public static AppIconChoice Parse(string value)
        {
            var name = Enum.GetNames(typeof(AppIconChoice)).FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
            return name == null ? AppIconChoice.BlackAndWhite : (AppIconChoice)Enum.Parse(typeof(AppIconChoice), name);
        }
    }
}
