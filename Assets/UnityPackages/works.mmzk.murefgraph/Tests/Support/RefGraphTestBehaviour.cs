using UnityEngine;

namespace Mmzkworks.muRefgraph.Tests
{
    /// <summary>Test-only component with various object reference fields.</summary>
    /// <remarks>Lives in a non-Editor assembly because MonoBehaviours in Editor-only assemblies cannot be added to GameObjects.</remarks>
    public sealed class RefGraphTestBehaviour : MonoBehaviour
    {
        public GameObject other;
        public GameObject[] others;
        public GameObject self;
        public Transform selfTransform;
        public Object missing;
    }
}
