using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// Test classes that host WPF views. They share the one Application an AppDomain allows, so they must not create it in parallel.
    /// </summary>
    [CollectionDefinition(Name)]
    public class WpfCollection
    {
        public const string Name = "Wpf";
    }
}
