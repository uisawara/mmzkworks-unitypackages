using System;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Monitoring hook for logging and debug views. Methods are generic so events are not boxed.
    /// </summary>
    public interface IEventHubObserver
    {
        /// <summary>An event was queued.</summary>
        void OnPublished<TEvent>(in TEvent e, in EventInfo info) where TEvent : struct, IEvent;

        /// <summary>All rules for an event have run.</summary>
        void OnHandled<TEvent>(in TEvent e, in EventInfo info, int ruleCount) where TEvent : struct, IEvent;

        /// <summary>A rule threw, or a dispatch limit was hit (<see cref="EventHubLimitException"/>).</summary>
        void OnError(in EventInfo info, Exception exception);
    }

    public sealed class EventHubLimitException : Exception
    {
        public EventHubLimitException(string message) : base(message)
        {
        }
    }
}
