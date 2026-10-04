namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Marker for an event: an immutable record of something that has already happened.
    /// Implement it on a readonly struct and name it in the past tense (e.g. ContactEvent, GemCollectedEvent).
    /// Events are structs so publishing and dispatching them does not allocate.
    /// </summary>
    public interface IEvent
    {
    }
}
