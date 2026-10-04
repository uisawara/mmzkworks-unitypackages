using System;
using System.Collections.Generic;
using Mmzkworks.muLogger;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mmzkworks.muEventHub.Tests
{
    public class EventHubTests
    {
        private readonly struct Ping : IEvent
        {
            public readonly int Value;
            public Ping(int value) => Value = value;
        }

        private readonly struct Pong : IEvent
        {
            public readonly int Value;
            public Pong(int value) => Value = value;
        }

        private sealed class RecordingObserver : IEventHubObserver
        {
            public readonly List<EventInfo> Published = new();
            public readonly List<EventInfo> Handled = new();
            public readonly List<Exception> Errors = new();

            public void OnPublished<TEvent>(in TEvent e, in EventInfo info) where TEvent : struct, IEvent => Published.Add(info);
            public void OnHandled<TEvent>(in TEvent e, in EventInfo info, int ruleCount) where TEvent : struct, IEvent => Handled.Add(info);
            public void OnError(in EventInfo info, Exception exception) => Errors.Add(exception);
        }

        private sealed class RecordingLogger : ILogger
        {
            public readonly List<string> Warnings = new();
            public readonly List<Exception> Errors = new();

            public void Log(string message) { }
            public void LogWarning(string message) => Warnings.Add(message);
            public void LogError(string message) { }
            public void LogError(Exception exception) => Errors.Add(exception);
        }

        private sealed class CountingRule : IEventRule<Ping>
        {
            public int Count;
            public void Handle(in Ping e, IEventContext context)
            {
                Count++;
                if (e.Value > 0) context.Publish(new Pong(e.Value));
            }
        }

        private EventHub _hub;
        private RecordingObserver _observer;

        [SetUp]
        public void SetUp()
        {
            _observer = new RecordingObserver();
            _hub = new EventHub { Observer = _observer };
        }

        [Test]
        public void Publish_DoesNotInvokeUntilDispatch()
        {
            var calls = 0;
            _hub.Subscribe<Ping>(_ => calls++);

            _hub.Publish(new Ping(1));
            Assert.AreEqual(0, calls);
            Assert.AreEqual(1, _hub.PendingCount);

            Assert.AreEqual(1, _hub.Dispatch());
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, _hub.PendingCount);
        }

        [Test]
        public void Dispatch_ProcessesEventsFifoAndRulesInSubscriptionOrder()
        {
            var log = new List<string>();
            _hub.Subscribe<Ping>(e => log.Add($"a{e.Value}"));
            _hub.Subscribe<Ping>(e => log.Add($"b{e.Value}"));

            _hub.Publish(new Ping(1));
            _hub.Publish(new Ping(2));
            _hub.Dispatch();

            CollectionAssert.AreEqual(new[] { "a1", "b1", "a2", "b2" }, log);
        }

        [Test]
        public void Dispatch_OnlyInvokesRulesForExactEventType()
        {
            var pings = 0;
            _hub.Subscribe<Ping>(_ => pings++);

            _hub.Publish(new Pong(1));
            _hub.Dispatch();

            Assert.AreEqual(0, pings);
        }

        [Test]
        public void FollowUpEvents_AreProcessedInSameDispatchAfterQueuedEvents()
        {
            var log = new List<string>();
            _hub.Subscribe<Ping>((e, ctx) =>
            {
                log.Add($"ping{e.Value}");
                ctx.Publish(new Pong(e.Value));
            });
            _hub.Subscribe<Pong>((e, ctx) =>
            {
                log.Add($"pong{e.Value}");
                Assert.AreEqual(1, ctx.Info.Depth);
                Assert.AreEqual(typeof(Ping), ctx.Info.CauseType);
            });

            _hub.Publish(new Ping(1));
            _hub.Publish(new Ping(2));
            Assert.AreEqual(4, _hub.Dispatch());

            CollectionAssert.AreEqual(new[] { "ping1", "ping2", "pong1", "pong2" }, log);
            Assert.AreEqual(0, _observer.Errors.Count);
        }

        [Test]
        public void Observer_ReceivesCausality()
        {
            _hub.Subscribe<Ping>((e, ctx) => ctx.Publish(new Pong(e.Value)));

            _hub.Publish(new Ping(1));
            _hub.Dispatch();

            Assert.AreEqual(2, _observer.Published.Count);
            var root = _observer.Published[0];
            var followUp = _observer.Published[1];
            Assert.IsTrue(root.IsRoot);
            Assert.AreEqual(typeof(Ping), root.EventType);
            Assert.AreEqual(root.Id, followUp.CauseId);
            Assert.AreEqual(typeof(Ping), followUp.CauseType);
            Assert.AreEqual(typeof(Pong), followUp.EventType);
            Assert.AreEqual(1, followUp.Depth);
            Assert.AreEqual(2, _observer.Handled.Count);
        }

        [Test]
        public void ChainDepthLimit_StopsCycles()
        {
            _hub.MaxChainDepth = 4;
            var handled = 0;
            _hub.Subscribe<Ping>((e, ctx) =>
            {
                handled++;
                ctx.Publish(new Ping(e.Value + 1));
            });

            _hub.Publish(new Ping(0));
            _hub.Dispatch();

            Assert.AreEqual(5, handled); // depth 0..4
            Assert.AreEqual(0, _hub.PendingCount);
            Assert.AreEqual(1, _observer.Errors.Count);
            Assert.IsInstanceOf<EventHubLimitException>(_observer.Errors[0]);
        }

        [Test]
        public void EventCountLimit_DefersRemainingToNextDispatch()
        {
            _hub.MaxEventsPerDispatch = 2;
            var handled = 0;
            _hub.Subscribe<Ping>(_ => handled++);

            for (var i = 0; i < 5; i++) _hub.Publish(new Ping(i));

            Assert.AreEqual(2, _hub.Dispatch());
            Assert.AreEqual(3, _hub.PendingCount);
            Assert.IsInstanceOf<EventHubLimitException>(_observer.Errors[0]);

            _hub.Dispatch();
            _hub.Dispatch();
            Assert.AreEqual(5, handled);
            Assert.AreEqual(0, _hub.PendingCount);
        }

        [Test]
        public void SubscribeDuringHandle_TakesEffectFromNextEvent()
        {
            var late = 0;
            var subscribed = false;
            _hub.Subscribe<Ping>(_ =>
            {
                if (subscribed) return;
                subscribed = true;
                _hub.Subscribe<Ping>(__ => late++);
            });

            _hub.Publish(new Ping(1));
            _hub.Publish(new Ping(2));
            _hub.Dispatch();

            Assert.AreEqual(1, late);
        }

        [Test]
        public void DisposeDuringHandle_TakesEffectFromNextEvent()
        {
            var calls = 0;
            IDisposable second = null;
            _hub.Subscribe<Ping>(_ => second.Dispose());
            second = _hub.Subscribe<Ping>(_ => calls++);

            _hub.Publish(new Ping(1));
            _hub.Publish(new Ping(2));
            _hub.Dispatch();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var calls = 0;
            var subscription = _hub.Subscribe<Ping>(_ => calls++);
            subscription.Dispose();
            subscription.Dispose();

            _hub.Publish(new Ping(1));
            _hub.Dispatch();

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ThrowingRule_DoesNotStopOtherRulesOrEvents()
        {
            var calls = 0;
            _hub.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            _hub.Subscribe<Ping>(_ => calls++);

            _hub.Publish(new Ping(1));
            _hub.Publish(new Ping(2));
            _hub.Dispatch();

            Assert.AreEqual(2, calls);
            Assert.AreEqual(2, _observer.Errors.Count);
            Assert.IsInstanceOf<InvalidOperationException>(_observer.Errors[0]);
        }

        [Test]
        public void ContextPublish_OutsideHandle_Throws()
        {
            IEventContext captured = null;
            _hub.Subscribe<Ping>((_, ctx) => captured = ctx);

            _hub.Publish(new Ping(1));
            _hub.Dispatch();

            Assert.Throws<InvalidOperationException>(() => captured.Publish(new Pong(1)));
        }

        [Test]
        public void Dispatch_IsNotReentrant()
        {
            Exception caught = null;
            _hub.Subscribe<Ping>(_ =>
            {
                try { _hub.Dispatch(); }
                catch (Exception e) { caught = e; }
            });

            _hub.Publish(new Ping(1));
            _hub.Dispatch();

            Assert.IsInstanceOf<InvalidOperationException>(caught);
        }

        [Test]
        public void Clear_DropsQueuedEventsOfAllTypes()
        {
            var calls = 0;
            _hub.Subscribe<Ping>(_ => calls++);

            _hub.Publish(new Ping(1));
            _hub.Publish(new Pong(1));
            _hub.Clear();
            Assert.AreEqual(0, _hub.PendingCount);

            _hub.Publish(new Ping(2));
            _hub.Dispatch();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void FifoOrder_IsKeptAcrossEventTypes()
        {
            var log = new List<string>();
            _hub.Subscribe<Ping>(e => log.Add($"ping{e.Value}"));
            _hub.Subscribe<Pong>(e => log.Add($"pong{e.Value}"));

            _hub.Publish(new Ping(1));
            _hub.Publish(new Pong(2));
            _hub.Publish(new Ping(3));
            _hub.Dispatch();

            CollectionAssert.AreEqual(new[] { "ping1", "pong2", "ping3" }, log);
        }

        [Test]
        public void WithoutObserver_ErrorsGoToLogger()
        {
            var logger = new RecordingLogger();
            var hub = new EventHub(logger: logger) { MaxChainDepth = 1 };
            hub.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            hub.Subscribe<Pong>((e, ctx) => ctx.Publish(new Pong(e.Value)));

            hub.Publish(new Ping(1));
            hub.Publish(new Pong(1));
            hub.Dispatch();

            Assert.AreEqual(1, logger.Errors.Count);
            Assert.IsInstanceOf<InvalidOperationException>(logger.Errors[0]);
            Assert.AreEqual(1, logger.Warnings.Count);
        }

        [Test]
        public void PublishAndDispatch_DoNotAllocate()
        {
            var hub = new EventHub(initialCapacity: 64, logger: new NullLogger());
            var rule = new CountingRule();
            hub.Subscribe(rule);
            hub.Subscribe<Pong>((e, ctx) => { });

            void Frame()
            {
                for (var i = 0; i < 16; i++) hub.Publish(new Ping(i));
                hub.Dispatch();
            }

            // Warm up: creates channels and grows queues to their working size.
            Frame();

            Assert.That(Frame, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(32, rule.Count);
        }
    }
}
