using System;
using UnityEngine;

namespace Mmzkworks.muLogger
{
    /// <summary>
    /// Resolves loggers by name. Replace the factory at startup to change which logger is used everywhere.
    /// </summary>
    public static class LoggerLocator
    {
        private static readonly Func<string, ILogger> DefaultFactory = name => new UnityLogger(name);

        private static volatile Func<string, ILogger> _factory = DefaultFactory;

        public static void SetFactory(Func<string, ILogger> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static void ResetFactory()
        {
            _factory = DefaultFactory;
        }

        public static ILogger Resolve(string name)
        {
            return _factory(name);
        }

        public static ILogger Resolve<T>()
        {
            return Resolve(GetTypeName(typeof(T)));
        }

        private static string GetTypeName(Type type)
        {
            var name = type.Name;
            var backtick = name.IndexOf('`');
            return backtick < 0 ? name : name.Substring(0, backtick);
        }

        // Static state survives entering Play Mode when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            ResetFactory();
        }
    }
}
