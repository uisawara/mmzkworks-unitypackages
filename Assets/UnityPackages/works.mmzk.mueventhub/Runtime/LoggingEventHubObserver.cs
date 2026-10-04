using System;
using Mmzkworks.muLogger;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Writes event flow to an <see cref="ILogger"/>, indented by chain depth. Allocates; for debugging only.
    /// </summary>
    public sealed class LoggingEventHubObserver : IEventHubObserver
    {
        private readonly ILogger _logger;

        /// <param name="logger">Defaults to <see cref="LoggerLocator"/>.</param>
        public LoggingEventHubObserver(ILogger logger = null)
        {
            _logger = logger ?? LoggerLocator.Resolve<EventHub>();
        }

        public void OnPublished<TEvent>(in TEvent e, in EventInfo info) where TEvent : struct, IEvent
        {
            var indent = new string(' ', info.Depth * 2);
            _logger.Log(info.IsRoot
                ? $"#{info.Id} {indent}{e}"
                : $"#{info.Id} {indent}{e}  <- #{info.CauseId} {info.CauseType.Name}");
        }

        public void OnHandled<TEvent>(in TEvent e, in EventInfo info, int ruleCount) where TEvent : struct, IEvent
        {
        }

        public void OnError(in EventInfo info, Exception exception)
        {
            if (exception is EventHubLimitException)
            {
                _logger.LogWarning($"{exception.Message} (#{info.Id} {info.EventType.Name})");
            }
            else
            {
                _logger.LogError(exception);
            }
        }
    }
}
