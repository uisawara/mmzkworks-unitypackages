using System;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Common filters for <see cref="ContactEvent"/> flows. Named WhereContact... / DistinctByContact...
    /// so they are easy to find with code completion.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="EventFlow{TSource,T}.Where"/> and <see cref="EventFlow{TSource,T}.DistinctBy{TKey}"/>,
    /// so they are allocated once when subscribing and do not allocate per event.
    /// Filters on Kind or ActorId reject contacts whose actor has been destroyed.
    /// </remarks>
    public static class ContactEventFlowExtensions
    {
        public static EventFlow<TSource, ContactEvent> WhereContactPhaseIsEnter<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow.Where(e => e.Phase == ContactPhase.Enter);
        }

        public static EventFlow<TSource, ContactEvent> WhereContactPhaseIsStay<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow.Where(e => e.Phase == ContactPhase.Stay);
        }

        public static EventFlow<TSource, ContactEvent> WhereContactPhaseIsExit<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow.Where(e => e.Phase == ContactPhase.Exit);
        }

        public static EventFlow<TSource, ContactEvent> WhereContactPhaseIs<TSource>(
            this EventFlow<TSource, ContactEvent> flow, ContactPhase phase) where TSource : struct, IEvent
        {
            return flow.Where(e => e.Phase == phase);
        }

        public static EventFlow<TSource, ContactEvent> WhereContactIsTrigger<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow.Where(e => e.IsTrigger);
        }

        public static EventFlow<TSource, ContactEvent> WhereContactIsCollision<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow.Where(e => !e.IsTrigger);
        }

        /// <summary>Both Self and Other still exist (actors may be destroyed before dispatch).</summary>
        public static EventFlow<TSource, ContactEvent> WhereContactActorsAreAlive<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow.Where(e => e.Self != null && e.Other != null);
        }

        public static EventFlow<TSource, ContactEvent> WhereContactSelfKindIs<TSource>(
            this EventFlow<TSource, ContactEvent> flow, string kind) where TSource : struct, IEvent
        {
            RequireKind(kind, nameof(kind));
            return flow.Where(e => e.Self != null && e.Self.Is(kind));
        }

        public static EventFlow<TSource, ContactEvent> WhereContactOtherKindIs<TSource>(
            this EventFlow<TSource, ContactEvent> flow, string kind) where TSource : struct, IEvent
        {
            RequireKind(kind, nameof(kind));
            return flow.Where(e => e.Other != null && e.Other.Is(kind));
        }

        public static EventFlow<TSource, ContactEvent> WhereContactSelfAndOtherKindsAre<TSource>(
            this EventFlow<TSource, ContactEvent> flow, string selfKind, string otherKind) where TSource : struct, IEvent
        {
            RequireKind(selfKind, nameof(selfKind));
            RequireKind(otherKind, nameof(otherKind));
            return flow.Where(e => e.Self != null && e.Other != null && e.Self.Is(selfKind) && e.Other.Is(otherKind));
        }

        /// <summary>Passes only the first contact of each Self actor. Keys are kept while subscribed.</summary>
        public static EventFlow<TSource, ContactEvent> DistinctByContactSelfActor<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow
                .Where(e => e.Self != null)
                .DistinctBy(e => e.Self.ActorId);
        }

        /// <summary>Passes only the first contact of each (Self, Other) pair. Keys are kept while subscribed.</summary>
        public static EventFlow<TSource, ContactEvent> DistinctByContactSelfAndOtherActors<TSource>(
            this EventFlow<TSource, ContactEvent> flow) where TSource : struct, IEvent
        {
            return flow
                .Where(e => e.Self != null && e.Other != null)
                .DistinctBy(e => (e.Self.ActorId, e.Other.ActorId));
        }

        private static void RequireKind(string kind, string paramName)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Kind must not be empty.", paramName);
        }
    }
}
