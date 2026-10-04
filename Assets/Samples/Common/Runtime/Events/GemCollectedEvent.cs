using Mmzkworks.muEventHub;

namespace App.Contents
{
    public readonly struct GemCollectedEvent : IEvent
    {
        public EventActor Collector { get; }
        public EventActor Gem { get; }

        public GemCollectedEvent(EventActor collector, EventActor gem)
        {
            Collector = collector;
            Gem = gem;
        }

        public override string ToString() => $"GemCollectedEvent({Collector} got {Gem})";
    }
}
