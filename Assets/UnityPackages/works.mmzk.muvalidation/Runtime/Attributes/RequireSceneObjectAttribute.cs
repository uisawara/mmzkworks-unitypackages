using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The scene containing this component must have a GameObject at the path, e.g. "Systems/EventSystem"
    /// (names separated by /, starting from a root object). If a component type is given, that object must
    /// also have it. Checked in scenes only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequireSceneObjectAttribute : ValidationAttribute
    {
        public string Path { get; }
        public Type ComponentType { get; }

        public override bool DependsOnOtherObjects => true;

        public RequireSceneObjectAttribute(string path, Type componentType = null)
        {
            Path = path ?? "";
            ComponentType = componentType;
        }

        public override void Validate(ValidationContext context)
        {
            var go = context.GameObject;
            if (go == null || context.Location != ValidationLocation.Scene || !go.scene.IsValid()) return;

            foreach (var candidate in Find(go.scene.GetRootGameObjects()))
            {
                if (ComponentType == null || candidate.GetComponent(ComponentType) != null) return;
            }

            var requirement = ComponentType == null ? "" : $" with {ComponentType.Name}";
            context.Report($"Scene \"{go.scene.name}\" must have GameObject \"{Path}\"{requirement}");
        }

        private IEnumerable<GameObject> Find(GameObject[] roots)
        {
            var names = Path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (names.Length == 0) yield break;

            var current = new List<Transform>();
            foreach (var root in roots)
            {
                if (root.name == names[0]) current.Add(root.transform);
            }

            for (var i = 1; i < names.Length && current.Count > 0; i++)
            {
                var next = new List<Transform>();
                foreach (var parent in current)
                {
                    foreach (Transform child in parent)
                    {
                        if (child.name == names[i]) next.Add(child);
                    }
                }

                current = next;
            }

            foreach (var transform in current) yield return transform.gameObject;
        }
    }
}
