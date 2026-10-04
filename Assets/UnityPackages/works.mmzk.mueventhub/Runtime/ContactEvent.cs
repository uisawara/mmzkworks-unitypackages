using UnityEngine;

namespace Mmzkworks.muEventHub
{
    public enum ContactPhase
    {
        Enter,
        Stay,
        Exit
    }

    /// <summary>
    /// <see cref="Self"/> touched <see cref="Other"/>. Published by the <see cref="ContactSensor"/> on Self.
    /// </summary>
    /// <remarks>
    /// Actors may be destroyed before the event is dispatched; rules should check them with == null.
    /// If both actors have a sensor, one contact produces two events (A→B and B→A).
    /// </remarks>
    public readonly struct ContactEvent : IEvent
    {
        public EventActor Self { get; }
        public EventActor Other { get; }
        public ContactPhase Phase { get; }
        public bool IsTrigger { get; }
        public Vector3 Point { get; }

        public ContactEvent(EventActor self, EventActor other, ContactPhase phase, bool isTrigger, Vector3 point)
        {
            Self = self;
            Other = other;
            Phase = phase;
            IsTrigger = isTrigger;
            Point = point;
        }

        public override string ToString() => $"ContactEvent({Self} -> {Other}, {Phase})";
    }
}
