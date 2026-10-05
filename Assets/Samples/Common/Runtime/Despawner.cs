using Mmzkworks.muEventHub;
using UnityEngine;

namespace App.Contents
{
    /// <summary>
    /// Receives <see cref="DespawnEvent"/> for this actor and removes it from the game.
    /// Put effects or pooling here instead of destroying, per actor type.
    /// </summary>
    [RequireComponent(typeof(EventActor))]
    [DisallowMultipleComponent]
    public sealed class Despawner : MonoBehaviour
    {
        public void OnDespawn(in DespawnEvent e)
        {
            Destroy(gameObject);
        }
    }
}
