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
        private ScoreRule _scoreRule;

        public int Score => _scoreRule?.Score ?? 0;

        private void OnEnable()
        {
            var hub = EventHubRunner.Resolve(_runner);
            if (hub == null)
            {
                Logger.LogWarning("No EventHubRunner found; rules are not installed.");
                return;
            }

            _scoreRule ??= new ScoreRule(_pointsPerGem);

            _subscriptions.Add(hub.Subscribe(new GemPickupRule(_gemKind, _collectorKind)));
            _subscriptions.Add(hub.Subscribe(_scoreRule));
            _subscriptions.Add(hub.Subscribe(new DespawnRule()));
        }

        private void OnDisable()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
