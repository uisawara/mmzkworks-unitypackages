using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public readonly struct GameObjectBuilder
    {
        public GameObject Target { get; }

        public GameObjectBuilder(GameObject target)
        {
            Target = target ? target : throw new ArgumentNullException(nameof(target));
        }

        public static implicit operator GameObject(GameObjectBuilder builder)
        {
            return builder.Target;
        }

        public GameObjectBuilder WithChild(GameObject child)
        {
            child.transform.SetParent(Target.transform);
            return this;
        }

        public GameObjectBuilder WithChildren(params GameObject[] children)
        {
            return WithChildren((IEnumerable<GameObject>) children);
        }

        public GameObjectBuilder WithChildren(IEnumerable<GameObject> children)
        {
            foreach (var child in children)
            {
                child.transform.SetParent(Target.transform);
            }

            return this;
        }

        public GameObjectBuilder WithComponent(Type component)
        {
            Target.AddComponent(component);
            return this;
        }

        public GameObjectBuilder WithComponents(IEnumerable<Type> components)
        {
            foreach (var component in components)
            {
                Target.AddComponent(component);
            }

            return this;
        }

        public GameObjectBuilder WithComponent<TComponent>(Action<TComponent> configure = null)
            where TComponent : Component
        {
            var component = Target.AddComponent<TComponent>();
            configure?.Invoke(component);
            return this;
        }

        public GameObjectBuilder Configure(Action<GameObject> configure)
        {
            configure(Target);
            return this;
        }

        public GameObjectBuilder Configure<TComponent>(Action<TComponent> configure) where TComponent : Component
        {
            var component = Target.GetComponent<TComponent>();
            if (component == null)
            {
                throw new MissingComponentException(
                    $"{typeof(TComponent).Name} is not attached to '{Target.name}'.");
            }

            configure(component);
            return this;
        }
    }
}
