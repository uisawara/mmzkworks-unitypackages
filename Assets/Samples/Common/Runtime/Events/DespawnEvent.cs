using Mmzkworks.muEventHub;

namespace App.Contents
{
    /// <summary>
    /// <see cref="Actor"/> is to be removed from the game.
    /// </summary>
    public readonly struct DespawnEvent : IEvent
    {
        public EventActor Actor { get; }

        public DespawnEvent(EventActor actor)
        {
            Actor = actor;
        }

        public override string ToString() => $"DespawnEvent({Actor})";
    }
}
