using System;
using UnityEngine;

namespace Mmzkworks.muSceneManager
{
    /// <summary>
    /// Identifier of a scene, based on the scene name (or path) passed to UnityEngine.SceneManagement.SceneManager.
    /// default(SceneId) is invalid and represents "no scene".
    /// Serializable so that it can be set from the Inspector (a SceneAsset picker is provided by SceneIdDrawer).
    /// </summary>
    [Serializable]
    public struct SceneId : IEquatable<SceneId>
    {
        // Not readonly because Unity cannot serialize readonly fields. Only assigned by the constructor and serialization.
        [SerializeField] private string _name;

        // Normalize so that an empty serialized value behaves the same as default
        public string Name => IsValid ? _name : null;

        public bool IsValid => !string.IsNullOrEmpty(_name);

        public SceneId(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Scene name must not be null or empty.", nameof(name));
            }

            _name = name;
        }

        public bool Equals(SceneId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SceneId other && Equals(other);

        public override int GetHashCode() => Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name);

        public override string ToString() => IsValid ? Name : "(none)";

        public static bool operator ==(SceneId left, SceneId right) => left.Equals(right);

        public static bool operator !=(SceneId left, SceneId right) => !left.Equals(right);
    }
}
