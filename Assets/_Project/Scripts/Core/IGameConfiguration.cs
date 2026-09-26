namespace TheLastWatch.Core
{
    public interface IGameConfiguration
    {
        BuildConfiguration BuildConfiguration { get; }
        string InitialSceneName { get; }
        bool AutoLoadInitialScene { get; }
        bool VerboseLogging { get; }
    }
}
