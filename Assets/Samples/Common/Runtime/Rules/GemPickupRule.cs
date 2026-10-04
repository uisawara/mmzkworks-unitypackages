using System.Collections.Generic;
using Mmzkworks.muEventHub;

namespace App.Contents
{
    /// <summary>
    /// A gem touched by a collector is collected, once.
    /// Expects the <see cref="ContactSensor"/> on the gem (Self = gem, Other = collector).
    /// </summary>
    public sealed class GemPickupRule : IEventRule<ContactEvent>
    {
        private readonly string _gemKind;
        private readonly string _collectorKind;
        private readonly HashSet<int> _collected = new();

        public GemPickupRule(string gemKind, string collectorKind)
        {
            _gemKind = gemKind;
            _collectorKind = collectorKind;
        }

        public void Handle(in ContactEvent e, IEventContext context)
        {
            if (e.Phase != ContactPhase.Enter) return;
            if (e.Self == null || e.Other == null) return;
            if (!e.Self.Is(_gemKind) || !e.Other.Is(_collectorKind)) return;

            // Several contacts can be queued in the same frame; collect each gem only once.
            if (!_collected.Add(e.Self.ActorId)) return;

            context.Publish(new GemCollectedEvent(e.Other, e.Self));
        }
    }
}
