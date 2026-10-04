using Mmzkworks.muEventHub;
using UnityEngine;

namespace App.Contents
{
    public sealed class DespawnRule : IEventRule<GemCollectedEvent>
    {
        public void Handle(in GemCollectedEvent e, IEventContext context)
        {
            if (e.Gem == null) return;
            Object.Destroy(e.Gem.gameObject);
        }
    }
}
