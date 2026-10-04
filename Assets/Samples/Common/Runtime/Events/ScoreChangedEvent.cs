using Mmzkworks.muEventHub;

namespace App.Contents
{
    public readonly struct ScoreChangedEvent : IEvent
    {
        public int Previous { get; }
        public int Current { get; }

        public ScoreChangedEvent(int previous, int current)
        {
            Previous = previous;
            Current = current;
        }

        public override string ToString() => $"ScoreChangedEvent({Previous} -> {Current})";
    }
}
