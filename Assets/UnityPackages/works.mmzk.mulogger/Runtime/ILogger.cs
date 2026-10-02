using System;

namespace Mmzkworks.muLogger
{
    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception);
    }
}
