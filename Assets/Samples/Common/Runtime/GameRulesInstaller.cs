using System;
using System.Collections.Generic;
using Mmzkworks.muEventHub;
using Mmzkworks.muLogger;
using Mmzkworks.muValidation;
using UnityEngine;
using ILogger = Mmzkworks.muLogger.ILogger;

namespace App.Contents
{
    /// <summary>
    /// Registers this level's rules on the hub while enabled. Put one in each level scene
    /// to give levels different rules.
    /// </summary>
    public sealed class GameRulesInstaller : MonoBehaviour
    {
        private static readonly ILogger Logger = LoggerLocator.Resolve<GameRulesInstaller>();

        [Tooltip("Leave empty to use EventHubRunner.Current.")]
        [SerializeField] private EventHubRunner _runner;
        [SerializeField, NotEmpty] private string _gemKind = "Gem";
        [SerializeField, NotEmpty] private string _collectorKind = "Player";
        [SerializeField, Min(0)] private int _pointsPerGem = 10;

        private readonly List<IDisposable> _subscriptions = new();

        public int Score { get; private set; }

        private void OnEnable()
        {
            var hub = EventHubRunner.Resolve(_runner);
            if (hub == null)
            {
                Logger.LogWarning("No EventHubRunner found; rules are not installed.");
                return;
            }

            // A gem touched by a collector is collected, once.
            // The ContactSensor is on the gem, so Self = gem and Other = collector.
            _subscriptions.Add(hub.On<ContactEvent>()
                .WhereContactPhaseIsEnter()
                .WhereContactSelfAndOtherKindsAre(_gemKind, _collectorKind)
                .DistinctByContactSelfActor()
                .Publish(e => new GemCollectedEvent(e.Other, e.Self)));

            // A collected gem adds to the score and is despawned.
            var gemCollected = hub.On<GemCollectedEvent>();
            _subscriptions.Add(gemCollected.Subscribe((e, context) => AddScore(_pointsPerGem, context)));
            _subscriptions.Add(gemCollected.Publish(e => new DespawnEvent(e.Gem)));

            // A despawned actor is destroyed.
            _subscriptions.Add(hub.On<DespawnEvent>()
                .Where(e => e.Actor != null)
                .Subscribe(e => Destroy(e.Actor.gameObject)));
        }

        private void AddScore(int points, IEventContext context)
        {
            var previous = Score;
            Score += points;
            context.Publish(new ScoreChangedEvent(previous, Score));
        }

        private void OnDisable()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
