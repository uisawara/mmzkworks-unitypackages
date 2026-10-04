using System;
using System.Collections.Generic;
using System.Threading;
using Mmzkworks.muLogger;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Queues events and runs the rules subscribed to them when <see cref="Dispatch"/> is called.
    /// Main thread only.
    /// </summary>
    /// <remarks>
    /// - <see cref="Publish{TEvent}"/> only queues; nothing runs until <see cref="Dispatch"/>.
    /// - Events are processed FIFO, rules in subscription order. Follow-up events published by rules
    ///   are appended to the queue and processed in the same Dispatch.
    /// - Subscribing or disposing inside a rule takes effect from the next event.
    /// - A rule that throws does not stop other rules or events.
    /// - Publish and Dispatch do not allocate once the queues have grown to their working size
    ///   (and the first event of each type has been seen). Subscribe allocates.
    /// </remarks>
    public sealed class EventHub
    {
        public const int DefaultMaxEventsPerDispatch = 1024;
        public const int DefaultMaxChainDepth = 16;

        // Global FIFO order across event types. The event values live in each channel's own queue.
        private readonly Queue<Entry> _order;
        private readonly int _initialCapacity;
        private readonly Context _context;
        private readonly ILogger _logger;
        private Channel[] _channels = Array.Empty<Channel>();
        private long _nextId;
        private bool _dispatching;

        /// <summary>Events beyond this count in one Dispatch stay queued for the next Dispatch.</summary>
        public int MaxEventsPerDispatch { get; set; } = DefaultMaxEventsPerDispatch;

        /// <summary>Follow-up events deeper than this are dropped (protects against rule cycles).</summary>
        public int MaxChainDepth { get; set; } = DefaultMaxChainDepth;

        public IEventHubObserver Observer { get; set; }

        public int PendingCount => _order.Count;

        /// <param name="initialCapacity">Initial queue size, to avoid growing during play.</param>
        /// <param name="logger">Receives rule exceptions and limit warnings when no <see cref="Observer"/> is set.
        /// Defaults to <see cref="LoggerLocator"/>.</param>
        public EventHub(int initialCapacity = 64, ILogger logger = null)
        {
            _logger = logger ?? LoggerLocator.Resolve<EventHub>();
            _initialCapacity = initialCapacity;
            _order = new Queue<Entry>(initialCapacity);
            _context = new Context(this);
        }

        public void Publish<TEvent>(in TEvent e) where TEvent : struct, IEvent
        {
            Enqueue(e, 0, null, 0);
        }

        public IDisposable Subscribe<TEvent>(IEventRule<TEvent> rule) where TEvent : struct, IEvent
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            var channel = GetChannel<TEvent>();
            channel.Add(rule);
            return new Subscription(() => channel.Remove(rule));
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent, IEventContext> handler) where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return Subscribe(new ActionRule<TEvent>(handler));
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return Subscribe(new ActionRule<TEvent>((e, _) => handler(e)));
        }

        /// <summary>
        /// Processes queued events and their follow-ups. Returns the number of events processed.
        /// </summary>
        public int Dispatch()
        {
            if (_dispatching) throw new InvalidOperationException("EventHub.Dispatch is not reentrant.");

            _dispatching = true;
            var processed = 0;
            try
            {
                while (_order.Count > 0)
                {
                    if (processed >= MaxEventsPerDispatch)
                    {
                        ReportError(_order.Peek().Info, new EventHubLimitException(
                            $"Processed {processed} events in one Dispatch; {_order.Count} deferred to the next Dispatch."));
                        break;
                    }

                    var entry = _order.Dequeue();
                    processed++;
                    entry.Channel.DispatchNext(entry.Info, _context);
                }
            }
            finally
            {
                _context.Clear();
                _dispatching = false;
            }

            return processed;
        }

        /// <summary>Drops all queued events.</summary>
        public void Clear()
        {
            _order.Clear();
            foreach (var channel in _channels) channel?.ClearPending();
        }

        private void Enqueue<TEvent>(in TEvent e, long causeId, Type causeType, int depth) where TEvent : struct, IEvent
        {
            var info = new EventInfo(++_nextId, typeof(TEvent), causeId, causeType, depth);

            if (depth > MaxChainDepth)
            {
                ReportError(info, new EventHubLimitException(
                    $"Chain depth {depth} exceeds {MaxChainDepth}; {typeof(TEvent).Name} dropped (caused by {causeType?.Name})."));
                return;
            }

            var channel = GetChannel<TEvent>();
            channel.Pending.Enqueue(e);
            _order.Enqueue(new Entry(channel, info));
            Observer?.OnPublished(e, info);
        }

        private Channel<TEvent> GetChannel<TEvent>() where TEvent : struct, IEvent
        {
            var index = TypeIndex<TEvent>.Value;
            if (index >= _channels.Length)
            {
                Array.Resize(ref _channels, Math.Max(index + 1, _channels.Length * 2));
            }

            var channel = (Channel<TEvent>)_channels[index];
            if (channel == null)
            {
                channel = new Channel<TEvent>(this, _initialCapacity);
                _channels[index] = channel;
            }

            return channel;
        }

        private void ReportError(in EventInfo info, Exception exception)
        {
            if (Observer != null)
            {
                Observer.OnError(info, exception);
            }
            else if (exception is EventHubLimitException)
            {
                _logger.LogWarning(exception.Message);
            }
            else
            {
                _logger.LogError(exception);
            }
        }

        private readonly struct Entry
        {
            public readonly Channel Channel;
            public readonly EventInfo Info;

            public Entry(Channel channel, in EventInfo info)
            {
                Channel = channel;
                Info = info;
            }
        }

        /// <summary>Assigns each event type a small index so channels can be looked up without hashing.</summary>
        private static class TypeIndex
        {
            public static int Count = -1;
        }

        private static class TypeIndex<TEvent>
        {
            public static readonly int Value = Interlocked.Increment(ref TypeIndex.Count);
        }

        private abstract class Channel
        {
            public abstract void DispatchNext(in EventInfo info, Context context);
            public abstract void ClearPending();
        }

        /// <summary>Pending values and rules for one event type.</summary>
        private sealed class Channel<TEvent> : Channel where TEvent : struct, IEvent
        {
            public readonly Queue<TEvent> Pending;
            private readonly EventHub _hub;

            // Copy-on-write: dispatch iterates a snapshot, so Add/Remove during Handle is safe.
            private IEventRule<TEvent>[] _rules = Array.Empty<IEventRule<TEvent>>();

            public Channel(EventHub hub, int capacity)
            {
                _hub = hub;
                Pending = new Queue<TEvent>(capacity);
            }

            public void Add(IEventRule<TEvent> rule)
            {
                var next = new IEventRule<TEvent>[_rules.Length + 1];
                Array.Copy(_rules, next, _rules.Length);
                next[_rules.Length] = rule;
                _rules = next;
            }

            public void Remove(IEventRule<TEvent> rule)
            {
                var index = Array.IndexOf(_rules, rule);
                if (index < 0) return;

                var next = new IEventRule<TEvent>[_rules.Length - 1];
                Array.Copy(_rules, 0, next, 0, index);
                Array.Copy(_rules, index + 1, next, index, _rules.Length - index - 1);
                _rules = next;
            }

            public override void DispatchNext(in EventInfo info, Context context)
            {
                var e = Pending.Dequeue();
                var snapshot = _rules;

                context.Set(info);
                foreach (var rule in snapshot)
                {
                    try
                    {
                        rule.Handle(e, context);
                    }
                    catch (Exception ex)
                    {
                        _hub.ReportError(info, ex);
                    }
                }
                context.Clear();

                _hub.Observer?.OnHandled(e, info, snapshot.Length);
            }

            public override void ClearPending() => Pending.Clear();
        }

        private sealed class ActionRule<TEvent> : IEventRule<TEvent> where TEvent : struct, IEvent
        {
            private readonly Action<TEvent, IEventContext> _handler;

            public ActionRule(Action<TEvent, IEventContext> handler)
            {
                _handler = handler;
            }

            public void Handle(in TEvent e, IEventContext context) => _handler(e, context);
        }

        private sealed class Context : IEventContext
        {
            private readonly EventHub _hub;
            private bool _active;

            public EventInfo Info { get; private set; }

            public Context(EventHub hub)
            {
                _hub = hub;
            }

            public void Set(in EventInfo info)
            {
                Info = info;
                _active = true;
            }

            public void Clear()
            {
                Info = default;
                _active = false;
            }

            public void Publish<TEvent>(in TEvent e) where TEvent : struct, IEvent
            {
                if (!_active)
                {
                    throw new InvalidOperationException(
                        "IEventContext is only valid during IEventRule.Handle. Use EventHub.Publish instead.");
                }

                _hub.Enqueue(e, Info.Id, Info.EventType, Info.Depth + 1);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
