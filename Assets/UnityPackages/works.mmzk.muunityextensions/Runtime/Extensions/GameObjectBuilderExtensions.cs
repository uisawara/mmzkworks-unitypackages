using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public static class GameObjectBuilderExtensions
    {
        public static GameObjectBuilder ToBuilder(this GameObject self)
        {
            return new GameObjectBuilder(self);
        }

        public static GameObjectBuilder WithChild(this GameObject self, GameObject child)
        {
            return self.ToBuilder().WithChild(child);
        }

        public static GameObjectBuilder WithChildren(this GameObject self, params GameObject[] children)
        {
            return self.ToBuilder().WithChildren(children);
        }

        public static GameObjectBuilder WithChildren(this GameObject self, IEnumerable<GameObject> children)
        {
            return self.ToBuilder().WithChildren(children);
        }

        public static GameObjectBuilder WithComponent(this GameObject self, Type component)
        {
            return self.ToBuilder().WithComponent(component);
        }

        public static GameObjectBuilder WithComponents(this GameObject self, IEnumerable<Type> components)
        {
            return self.ToBuilder().WithComponents(components);
        }

        public static GameObjectBuilder WithComponent<TComponent>(this GameObject self,
            Action<TComponent> configure = null) where TComponent : Component
        {
            return self.ToBuilder().WithComponent(configure);
        }

        public static GameObjectBuilder Configure(this GameObject self, Action<GameObject> configure)
        {
            return self.ToBuilder().Configure(configure);
        }

        public static GameObjectBuilder Configure<TComponent>(this GameObject self, Action<TComponent> configure)
            where TComponent : Component
        {
            return self.ToBuilder().Configure(configure);
        }
    }
}
