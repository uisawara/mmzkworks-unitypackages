using Mmzkworks.muValidation;
using UnityEngine;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// Identifies an actor in events so rules can tell who interacted with whom.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EventActor : MonoBehaviour
    {
        [SerializeField, NotEmpty] private string _kind;

        /// <summary>Category used by rules to match actors (e.g. "Player", "Gem").</summary>
        public string Kind => _kind;

        /// <summary>Unique for this actor while it exists.</summary>
        public int ActorId => gameObject.GetInstanceID();

        public bool Is(string kind) => _kind == kind;

        public override string ToString() => $"{_kind}:{name}";
    }
}
