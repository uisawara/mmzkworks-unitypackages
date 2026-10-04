using Mmzkworks.muLogger;
using Mmzkworks.muValidation;
using UnityEngine;
using ILogger = Mmzkworks.muLogger.ILogger;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Owns an <see cref="EventHub"/> and dispatches it in LateUpdate, after physics callbacks and Update.
    /// Place one in a resident scene; it registers itself as <see cref="Current"/>.
    /// </summary>
    /// <remarks>
    /// The late execution order also delays this Awake, so other components' Awake / OnEnable can run first.
    /// <see cref="Hub"/> and <see cref="Current"/> are therefore initialized on first access, not in Awake.
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [SingleInScene]
    public sealed class EventHubRunner : MonoBehaviour
    {
        private static readonly ILogger Logger = LoggerLocator.Resolve<EventHubRunner>();

        [Tooltip("Initial queue size per event type. Size it to a busy frame to avoid growing during play.")]
        [SerializeField, Min(1)] private int _initialCapacity = 64;
        [SerializeField, Min(1)] private int _maxEventsPerDispatch = EventHub.DefaultMaxEventsPerDispatch;
        [SerializeField, Min(0)] private int _maxChainDepth = EventHub.DefaultMaxChainDepth;
        [SerializeField] private bool _logEvents;

        private static EventHubRunner _current;
        private EventHub _hub;

        /// <summary>
        /// The registered runner. If none has registered yet (its Awake has not run), an active one is looked up.
        /// </summary>
        public static EventHubRunner Current
        {
            get
            {
                if (_current == null) _current = FindAnyObjectByType<EventHubRunner>();
                return _current;
            }
        }

        /// <summary>Created on first access, so it is available before this Awake.</summary>
        public EventHub Hub => _hub ??= new EventHub(_initialCapacity)
        {
            MaxEventsPerDispatch = _maxEventsPerDispatch,
            MaxChainDepth = _maxChainDepth,
            Observer = _logEvents ? new LoggingEventHubObserver() : null
        };

        /// <summary>
        /// Returns <paramref name="preferred"/>'s hub if set, otherwise <see cref="Current"/>'s, otherwise null.
        /// </summary>
        public static EventHub Resolve(EventHubRunner preferred = null)
        {
            if (preferred != null) return preferred.Hub;
            var current = Current;
            return current != null ? current.Hub : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
        }

        private void Awake()
        {
            if (_current == null)
            {
                _current = this;
            }
            else if (_current != this)
            {
                Logger.LogWarning($"Another EventHubRunner is already Current ({_current.name}); {name} is not registered.");
            }
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        private void LateUpdate()
        {
            Hub.Dispatch();
        }
    }
}
