using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muEventHub.Tests
{
    // In Edit Mode, AddComponent does not call Awake, which reproduces a runner whose Awake has not run yet
    // (its late execution order lets other components' Awake / OnEnable run first).
    public class EventHubRunnerTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            // Runner lookup searches the open scenes; skip when they already contain one.
            Assume.That(Object.FindAnyObjectByType<EventHubRunner>(), Is.Null, "The open scene already has an EventHubRunner.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Hub_IsAvailableBeforeAwake()
        {
            _go = new GameObject("EventHub");
            var runner = _go.AddComponent<EventHubRunner>();

            Assert.IsNotNull(runner.Hub);
            Assert.AreSame(runner.Hub, runner.Hub);
            Assert.AreSame(runner.Hub, EventHubRunner.Resolve(runner));
        }

        [Test]
        public void Resolve_FindsRunnerBeforeItsAwake()
        {
            _go = new GameObject("EventHub");
            var runner = _go.AddComponent<EventHubRunner>();

            Assert.AreSame(runner, EventHubRunner.Current);
            Assert.AreSame(runner.Hub, EventHubRunner.Resolve());
        }

        [Test]
        public void Resolve_WithoutRunner_ReturnsNull()
        {
            Assert.IsNull(EventHubRunner.Resolve());
        }
    }
}
