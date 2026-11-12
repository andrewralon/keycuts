using System.Collections.Generic;

namespace keycuts.Common
{
    /// <summary>What the Manager's Edit form changes about a keycut (#180). See <see cref="KeycutRepository.UpdateAsync"/>.</summary>
    public sealed record KeycutEdit(string Target, string Name, string Description)
    {
        /// <summary>
        /// Save a file or folder target with environment variables (#31), like a new keycut does. It applies only when
        /// <see cref="Target"/> was changed: an unchanged target is written exactly as it was.
        /// </summary>
        public bool UseEnvironmentVariables { get; init; }

        /// <summary>The variables for <see cref="UseEnvironmentVariables"/>; null reads them from this machine. Tests pass their own.</summary>
        public IReadOnlyList<PathVariable> PathVariables { get; init; }
    }
}
