using System;
using System.Collections.Generic;
using Mmzkworks.muLogger;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mmzkworks.muEventHub.Tests
{
    public class EventFlowTests
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

        private EventHub _hub;

        [SetUp]
        public void SetUp()
        {
            _hub = new EventHub(logger: new NullLogger());
        }

        private void PublishAll(params int[] values)
        {
            foreach (var value in values) _hub.Publish(new Ping(value));
            _hub.Dispatch();
        }

        [Test]
        public void Where_PassesOnlyMatchingEvents()
        {
            var received = new List<int>();
            _hub.On<Ping>()
                .Where(e => e.Value > 1)
                .Where(e => e.Value % 2 == 0)
                .Subscribe(e => received.Add(e.Value));

            PublishAll(1, 2, 3, 4);

            CollectionAssert.AreEqual(new[] { 2, 4 }, received);
        }

        [Test]
        public void Select_TransformsEvents()
        {
            var received = new List<string>();
            _hub.On<Ping>()
                .Select(e => e.Value * 10)
                .Where(v => v > 10)
                .Select(v => $"v{v}")
                .Subscribe(s => received.Add(s));

            PublishAll(1, 2, 3);

            CollectionAssert.AreEqual(new[] { "v20", "v30" }, received);
        }

        [Test]
        public void DistinctBy_PassesFirstEventPerKey()
        {
            var received = new List<int>();
            _hub.On<Ping>()
                .DistinctBy(e => e.Value % 3)
                .Subscribe(e => received.Add(e.Value));

            PublishAll(1, 4, 2, 5, 3, 6);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, received);
        }

        [Test]
        public void Publish_RaisesFollowUpInSameDispatch()
        {
            var received = new List<(int value, Type cause, int depth)>();
            _hub.On<Ping>()
                .Where(e => e.Value > 0)
                .Publish(e => new Pong(e.Value + 100));
            _hub.Subscribe<Pong>((e, ctx) => received.Add((e.Value, ctx.Info.CauseType, ctx.Info.Depth)));

            PublishAll(0, 1);

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual((101, typeof(Ping), 1), received[0]);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var count = 0;
            var subscription = _hub.On<Ping>().Subscribe(_ => count++);

            PublishAll(1);
            subscription.Dispose();
            PublishAll(2);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void NothingIsSubscribedUntilTerminal()
        {
            var count = 0;
            _hub.On<Ping>().Where(_ => { count++; return true; });

            PublishAll(1);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Default_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => default(EventFlow<Ping, Ping>).Where(_ => true));
        }

        [Test]
        public void PublishAndDispatch_ThroughFlow_DoNotAllocate()
        {
            var threshold = 0;
            var handled = 0;
            _hub.On<Ping>()
                .Where(e => e.Value > threshold)          // captures a local
                .Select(e => e.Value * 2)
                .DistinctBy(v => v % 8)
                .Subscribe(_ => handled++);
            _hub.On<Ping>()
                .Where(e => e.Value % 2 == 0)
                .Publish(e => new Pong(e.Value));
            _hub.On<Pong>().Subscribe((e, ctx) => { });

            void Frame()
            {
                for (var i = 0; i < 16; i++) _hub.Publish(new Ping(i));
                _hub.Dispatch();
            }

            // Warm up: creates channels, grows queues and fills the DistinctBy set.
            Frame();

            Assert.That(Frame, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(4, handled);
        }
    }
}
