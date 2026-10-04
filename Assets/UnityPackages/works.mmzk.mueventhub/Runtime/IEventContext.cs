namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Passed to a rule while it handles an event. Only valid during that Handle call.
    /// </summary>
    public interface IEventContext
    {
        /// <summary>The event being handled.</summary>
        EventInfo Info { get; }

        /// <summary>
        /// Publishes a follow-up event caused by the current one.
        /// It is processed later in the same Dispatch.
        /// </summary>
        void Publish<TEvent>(in TEvent e) where TEvent : struct, IEvent;
    }
}
