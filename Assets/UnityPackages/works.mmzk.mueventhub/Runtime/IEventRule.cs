namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Decides what an event causes. Rules may change game state and publish follow-up events
    /// through <see cref="IEventContext.Publish{TEvent}"/>.
    /// </summary>
    public interface IEventRule<TEvent> where TEvent : struct, IEvent
    {
        void Handle(in TEvent e, IEventContext context);
    }
}
