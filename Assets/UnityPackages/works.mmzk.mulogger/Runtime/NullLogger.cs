using System;

namespace Mmzkworks.muLogger
{
    public sealed class NullLogger : ILogger
    {
        public void Log(string message)
        { }

        public void LogWarning(string message)
        { }

        public void LogError(string message)
        { }

        public void LogError(Exception exception)
        { }
    }
}
