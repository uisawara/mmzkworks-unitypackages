using System;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Metadata of a queued event. Identifies the event and its cause without boxing them.
    /// </summary>
    public readonly struct EventInfo
    {
        /// <summary>Unique within the hub, increasing in publish order.</summary>
        public readonly long Id;
        public readonly Type EventType;

        /// <summary>Id of the event whose rule published this one, or 0 for a root event.</summary>
        public readonly long CauseId;

        /// <summary>Type of the cause event, or null for a root event.</summary>
        public readonly Type CauseType;

        /// <summary>0 for a root event, +1 for each follow-up step.</summary>
        public readonly int Depth;

        public bool IsRoot => CauseId == 0;

        public EventInfo(long id, Type eventType, long causeId, Type causeType, int depth)
        {
            Id = id;
            EventType = eventType;
            CauseId = causeId;
            CauseType = causeType;
            Depth = depth;
        }
    }
}
