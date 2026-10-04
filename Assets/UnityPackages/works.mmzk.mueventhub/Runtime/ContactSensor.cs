using Mmzkworks.muLogger;
using Mmzkworks.muValidation;
using UnityEngine;
using ILogger = Mmzkworks.muLogger.ILogger;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Publishes a <see cref="ContactEvent"/> when this collider touches another <see cref="EventActor"/>.
    /// Holds no game logic: what the contact causes is decided by rules on the hub.
    /// Put it on the object that receives physics callbacks (the collider, or the Rigidbody owner).
    /// </summary>
    [RequireComponentInParent(typeof(EventActor))]
    [ReceivesPhysicsCallbacks]
    public sealed class ContactSensor : MonoBehaviour
    {
        private static readonly ILogger Logger = LoggerLocator.Resolve<ContactSensor>();

        [Tooltip("Leave empty to use EventHubRunner.Current.")]
        [SerializeField] private EventHubRunner _runner;
        [SerializeField] private bool _enter = true;
        [SerializeField] private bool _stay;
        [SerializeField] private bool _exit;

        private EventActor _self;

        private void Awake()
        {
            _self = GetComponentInParent<EventActor>();
            if (_self == null)
            {
                Logger.LogWarning($"{name} has no EventActor in its parents.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_enter) Report(other, ContactPhase.Enter, true, ClosestPoint(other));
        }

        private void OnTriggerStay(Collider other)
        {
            if (_stay) Report(other, ContactPhase.Stay, true, ClosestPoint(other));
        }

        private void OnTriggerExit(Collider other)
        {
            if (_exit) Report(other, ContactPhase.Exit, true, ClosestPoint(other));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_enter) Report(collision.collider, ContactPhase.Enter, false, ContactPoint(collision));
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_stay) Report(collision.collider, ContactPhase.Stay, false, ContactPoint(collision));
        }

        private void OnCollisionExit(Collision collision)
        {
            if (_exit) Report(collision.collider, ContactPhase.Exit, false, ContactPoint(collision));
        }

        private void Report(Collider other, ContactPhase phase, bool isTrigger, Vector3 point)
        {
            if (_self == null) return;

            var otherActor = other.GetComponentInParent<EventActor>();
            if (otherActor == null || otherActor == _self) return;

            var hub = EventHubRunner.Resolve(_runner);
            if (hub == null) return;

            hub.Publish(new ContactEvent(_self, otherActor, phase, isTrigger, point));
        }

        private Vector3 ClosestPoint(Collider other) => other.bounds.ClosestPoint(transform.position);

        private Vector3 ContactPoint(Collision collision) =>
            collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
    }
}
