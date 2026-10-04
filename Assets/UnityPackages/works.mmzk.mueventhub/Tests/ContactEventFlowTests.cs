using System;
using System.Collections.Generic;
using System.Reflection;
using Mmzkworks.muLogger;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Mmzkworks.muEventHub.Tests
{
    public class ContactEventFlowTests
    {
        private static readonly FieldInfo KindField =
            typeof(EventActor).GetField("_kind", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<GameObject> _created = new();
        private EventHub _hub;
        private int _passed;

        [SetUp]
        public void SetUp()
        {
            _hub = new EventHub(logger: new NullLogger());
            _passed = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private EventActor Actor(string kind)
        {
            var go = new GameObject(kind);
            _created.Add(go);
            var actor = go.AddComponent<EventActor>();
            KindField.SetValue(actor, kind);
            return actor;
        }

        private void Count(EventFlow<ContactEvent, ContactEvent> flow) => flow.Subscribe(_ => _passed++);

        private int Send(EventActor self, EventActor other, ContactPhase phase = ContactPhase.Enter, bool isTrigger = true)
        {
            var before = _passed;
            _hub.Publish(new ContactEvent(self, other, phase, isTrigger, Vector3.zero));
            _hub.Dispatch();
            return _passed - before;
        }

        [Test]
        public void WhereContactPhaseIs_FiltersByPhase()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");

            Count(_hub.On<ContactEvent>().WhereContactPhaseIsEnter());
            Assert.AreEqual(1, Send(gem, player, ContactPhase.Enter));
            Assert.AreEqual(0, Send(gem, player, ContactPhase.Stay));
            Assert.AreEqual(0, Send(gem, player, ContactPhase.Exit));

            SetUp();
            Count(_hub.On<ContactEvent>().WhereContactPhaseIsStay());
            Assert.AreEqual(1, Send(gem, player, ContactPhase.Stay));
            Assert.AreEqual(0, Send(gem, player, ContactPhase.Enter));

            SetUp();
            Count(_hub.On<ContactEvent>().WhereContactPhaseIsExit());
            Assert.AreEqual(1, Send(gem, player, ContactPhase.Exit));
            Assert.AreEqual(0, Send(gem, player, ContactPhase.Stay));

            SetUp();
            Count(_hub.On<ContactEvent>().WhereContactPhaseIs(ContactPhase.Stay));
            Assert.AreEqual(1, Send(gem, player, ContactPhase.Stay));
            Assert.AreEqual(0, Send(gem, player, ContactPhase.Exit));
        }

        [Test]
        public void WhereContactIsTriggerOrCollision_FiltersByIsTrigger()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");

            Count(_hub.On<ContactEvent>().WhereContactIsTrigger());
            Assert.AreEqual(1, Send(gem, player, isTrigger: true));
            Assert.AreEqual(0, Send(gem, player, isTrigger: false));

            SetUp();
            Count(_hub.On<ContactEvent>().WhereContactIsCollision());
            Assert.AreEqual(1, Send(gem, player, isTrigger: false));
            Assert.AreEqual(0, Send(gem, player, isTrigger: true));
        }

        [Test]
        public void WhereContactActorsAreAlive_RejectsDestroyedActors()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");
            Count(_hub.On<ContactEvent>().WhereContactActorsAreAlive());

            Assert.AreEqual(1, Send(gem, player));

            Object.DestroyImmediate(player.gameObject);
            Assert.AreEqual(0, Send(gem, player));
        }

        [Test]
        public void WhereContactKind_MatchesSelfAndOther()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");
            var enemy = Actor("Enemy");

            Count(_hub.On<ContactEvent>().WhereContactSelfKindIs("Gem"));
            Assert.AreEqual(1, Send(gem, player));
            Assert.AreEqual(0, Send(player, gem));

            SetUp();
            Count(_hub.On<ContactEvent>().WhereContactOtherKindIs("Player"));
            Assert.AreEqual(1, Send(gem, player));
            Assert.AreEqual(0, Send(gem, enemy));

            SetUp();
            Count(_hub.On<ContactEvent>().WhereContactSelfAndOtherKindsAre("Gem", "Player"));
            Assert.AreEqual(1, Send(gem, player));
            Assert.AreEqual(0, Send(player, gem));
            Assert.AreEqual(0, Send(gem, enemy));
        }

        [Test]
        public void WhereContactKind_RejectsDestroyedActors()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");
            Count(_hub.On<ContactEvent>().WhereContactSelfAndOtherKindsAre("Gem", "Player"));

            Object.DestroyImmediate(gem.gameObject);

            Assert.AreEqual(0, Send(gem, player));
        }

        [Test]
        public void WhereContactKind_EmptyKind_Throws()
        {
            var flow = _hub.On<ContactEvent>();

            Assert.Throws<ArgumentException>(() => flow.WhereContactSelfKindIs(""));
            Assert.Throws<ArgumentException>(() => flow.WhereContactOtherKindIs(" "));
            Assert.Throws<ArgumentException>(() => flow.WhereContactSelfAndOtherKindsAre("Gem", null));
        }

        [Test]
        public void DistinctByContactSelfActor_PassesFirstContactPerSelf()
        {
            var gemA = Actor("Gem");
            var gemB = Actor("Gem");
            var player = Actor("Player");
            var enemy = Actor("Enemy");
            Count(_hub.On<ContactEvent>().DistinctByContactSelfActor());

            Assert.AreEqual(1, Send(gemA, player));
            Assert.AreEqual(0, Send(gemA, enemy));
            Assert.AreEqual(1, Send(gemB, player));
        }

        [Test]
        public void DistinctByContactSelfAndOtherActors_PassesFirstContactPerPair()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");
            var enemy = Actor("Enemy");
            Count(_hub.On<ContactEvent>().DistinctByContactSelfAndOtherActors());

            Assert.AreEqual(1, Send(gem, player));
            Assert.AreEqual(0, Send(gem, player));
            Assert.AreEqual(1, Send(gem, enemy));
            Assert.AreEqual(1, Send(player, gem));
        }

        [Test]
        public void DistinctByContact_RejectsDestroyedActors()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");
            Count(_hub.On<ContactEvent>().DistinctByContactSelfAndOtherActors());

            Object.DestroyImmediate(player.gameObject);

            Assert.AreEqual(0, Send(gem, player));
        }

        [Test]
        public void ContactFilters_DoNotAllocate()
        {
            var gem = Actor("Gem");
            var player = Actor("Player");
            _hub.On<ContactEvent>()
                .WhereContactPhaseIsEnter()
                .WhereContactIsTrigger()
                .WhereContactSelfAndOtherKindsAre("Gem", "Player")
                .DistinctByContactSelfAndOtherActors()
                .Subscribe(_ => _passed++);

            void Frame()
            {
                for (var i = 0; i < 8; i++)
                {
                    _hub.Publish(new ContactEvent(gem, player, ContactPhase.Enter, true, Vector3.zero));
                    _hub.Publish(new ContactEvent(gem, player, ContactPhase.Stay, true, Vector3.zero));
                }
                _hub.Dispatch();
            }

            // Warm up: creates the channel, grows queues and fills the DistinctBy set.
            Frame();

            Assert.That(Frame, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1, _passed);
        }
    }
}
