namespace keycuts.Common
{
    /// <summary>The display theme the user chose. System comes first so an unset value means "follow Windows".</summary>
    public enum ThemePreference
    {
        System,
        Light,
        Dark
    }

    public static class ThemePreferences
    {
        /// <summary>A missing or unknown value means System, so a bad registry value can't stop the app from starting.</summary>
        public static ThemePreference Parse(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "light" => ThemePreference.Light,
                "dark" => ThemePreference.Dark,
                _ => ThemePreference.System
            };
        }
    }
}
