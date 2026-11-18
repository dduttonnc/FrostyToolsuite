namespace FrostySdk.Interfaces
{
    public interface ILogger
    {
        void Log(string text, params object[] vars);
        void LogWarning(string text, params object[] vars);
        void LogError(string text, params object[] vars);
    }

    // [DAI fix] optional, for a splash screen that lists every superbundle being loaded at once
    public interface ILoadingListLogger
    {
        void LogLoadingList(string text);
    }
}
