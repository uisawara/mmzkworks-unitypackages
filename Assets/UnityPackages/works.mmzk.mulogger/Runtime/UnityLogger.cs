using System;
using System.Threading;
using UnityEngine;

namespace Mmzkworks.muLogger
{
    public sealed class UnityLogger : ILogger
    {
        private static int _mainThreadId;

        public UnityLogger(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Log;

        public void Log(string message)
        {
            if (MinimumLevel > LogLevel.Log) return;
            Debug.Log($"{Prefix("cyan")} {message}");
        }

        public void LogWarning(string message)
        {
            if (MinimumLevel > LogLevel.Warning) return;
            Debug.LogWarning($"{Prefix("yellow")} {message}");
        }

        public void LogError(string message)
        {
            if (MinimumLevel > LogLevel.Error) return;
            Debug.LogError($"{Prefix("red")} {message}");
        }

        public void LogError(Exception exception)
        {
            if (MinimumLevel > LogLevel.Error) return;
            // Wrap to keep the logger name; the console shows the original exception and its stack trace first
            Debug.LogException(new Exception($"{Prefix("red")} {exception.Message}", exception));
        }

        private string Prefix(string color)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var thread = threadId == _mainThreadId ? "" : $"({threadId}) ";
#if UNITY_EDITOR
            return $"{thread}<b><color={color}>[{Name}]</color></b>";
#else
            return $"{thread}[{Name}]";
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureMainThreadId()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
