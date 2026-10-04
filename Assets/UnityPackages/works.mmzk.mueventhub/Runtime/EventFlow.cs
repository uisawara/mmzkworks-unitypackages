using System;
using System.Collections.Generic;

namespace Mmzkworks.muEventHub
{
    public static class EventFlowExtensions
    {
        /// <summary>
        /// Starts a rule written as a method chain, e.g.
        /// <c>hub.On&lt;ContactEvent&gt;().Where(...).Publish(e =&gt; new ...)</c>.
        /// Nothing is subscribed until a terminal method (Publish / Subscribe) is called.
        /// </summary>
        public static EventFlow<TEvent, TEvent> On<TEvent>(this EventHub hub) where TEvent : struct, IEvent
        {
            if (hub == null) throw new ArgumentNullException(nameof(hub));
            return new EventFlow<TEvent, TEvent>(hub, sink => sink);
        }
    }

    /// <summary>
    /// A rule under construction: events of <typeparamref name="TSource"/> flowing through operators as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// The chain is built once, when the terminal method subscribes it; lambdas and stages are allocated then.
    /// Handling an event only invokes the built stages and does not allocate.
    /// </remarks>
    public readonly struct EventFlow<TSource, T> where TSource : struct, IEvent
    {
        private readonly EventHub _hub;

        // Connects a downstream stage to everything upstream, returning the root stage.
        private readonly Func<FlowSink<T>, FlowSink<TSource>> _connect;

        internal EventFlow(EventHub hub, Func<FlowSink<T>, FlowSink<TSource>> connect)
        {
            _hub = hub;
            _connect = connect;
        }

        /// <summary>Passes only events for which <paramref name="predicate"/> returns true.</summary>
        public EventFlow<TSource, T> Where(Func<T, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var connect = Connect();
            return new EventFlow<TSource, T>(_hub, sink => connect(new WhereSink<T>(predicate, sink)));
        }

        /// <summary>Transforms each event.</summary>
        public EventFlow<TSource, TOut> Select<TOut>(Func<T, TOut> selector)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var connect = Connect();
            return new EventFlow<TSource, TOut>(_hub, sink => connect(new SelectSink<T, TOut>(selector, sink)));
        }

        /// <summary>
        /// Passes only the first event for each key. Keys are kept while subscribed.
        /// </summary>
        public EventFlow<TSource, T> DistinctBy<TKey>(Func<T, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            var connect = Connect();
            return new EventFlow<TSource, T>(_hub, sink => connect(new DistinctBySink<T, TKey>(keySelector, sink)));
        }

        /// <summary>
        /// Subscribes the chain; each passing event publishes a follow-up event caused by the source event.
        /// </summary>
        public IDisposable Publish<TOut>(Func<T, TOut> selector) where TOut : struct, IEvent
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return Subscribe(new PublishSink<T, TOut>(selector));
        }

        /// <summary>Subscribes the chain, calling <paramref name="action"/> for each passing event.</summary>
        public IDisposable Subscribe(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return Subscribe(new ActionSink<T>((e, _) => action(e)));
        }

        /// <summary>Subscribes the chain, calling <paramref name="action"/> for each passing event.</summary>
        public IDisposable Subscribe(Action<T, IEventContext> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return Subscribe(new ActionSink<T>(action));
        }

        private IDisposable Subscribe(FlowSink<T> terminal)
        {
            if (_hub == null) throw new InvalidOperationException("EventFlow must be started with EventHub.On<T>().");
            return _hub.Subscribe(new FlowRule<TSource>(Connect()(terminal)));
        }

        private Func<FlowSink<T>, FlowSink<TSource>> Connect()
        {
            return _connect ?? throw new InvalidOperationException("EventFlow must be started with EventHub.On<T>().");
        }
    }

    internal abstract class FlowSink<T>
    {
        public abstract void OnNext(T e, IEventContext context);
    }

    internal sealed class FlowRule<TEvent> : IEventRule<TEvent> where TEvent : struct, IEvent
    {
        private readonly FlowSink<TEvent> _root;

        public FlowRule(FlowSink<TEvent> root)
        {
            _root = root;
        }

        public void Handle(in TEvent e, IEventContext context) => _root.OnNext(e, context);
    }

    internal sealed class WhereSink<T> : FlowSink<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly FlowSink<T> _next;

        public WhereSink(Func<T, bool> predicate, FlowSink<T> next)
        {
            _predicate = predicate;
            _next = next;
        }

        public override void OnNext(T e, IEventContext context)
        {
            if (_predicate(e)) _next.OnNext(e, context);
        }
    }

    internal sealed class SelectSink<T, TOut> : FlowSink<T>
    {
        private readonly Func<T, TOut> _selector;
        private readonly FlowSink<TOut> _next;

        public SelectSink(Func<T, TOut> selector, FlowSink<TOut> next)
        {
            _selector = selector;
            _next = next;
        }

        public override void OnNext(T e, IEventContext context) => _next.OnNext(_selector(e), context);
    }

    internal sealed class DistinctBySink<T, TKey> : FlowSink<T>
    {
        private readonly Func<T, TKey> _keySelector;
        private readonly FlowSink<T> _next;
        private readonly HashSet<TKey> _seen = new();

        public DistinctBySink(Func<T, TKey> keySelector, FlowSink<T> next)
        {
            _keySelector = keySelector;
            _next = next;
        }

        public override void OnNext(T e, IEventContext context)
        {
            if (_seen.Add(_keySelector(e))) _next.OnNext(e, context);
        }
    }

    internal sealed class PublishSink<T, TOut> : FlowSink<T> where TOut : struct, IEvent
    {
        private readonly Func<T, TOut> _selector;

        public PublishSink(Func<T, TOut> selector)
        {
            _selector = selector;
        }

        public override void OnNext(T e, IEventContext context) => context.Publish(_selector(e));
    }

    internal sealed class ActionSink<T> : FlowSink<T>
    {
        private readonly Action<T, IEventContext> _action;

        public ActionSink(Action<T, IEventContext> action)
        {
            _action = action;
        }

        public override void OnNext(T e, IEventContext context) => _action(e, context);
    }
}
