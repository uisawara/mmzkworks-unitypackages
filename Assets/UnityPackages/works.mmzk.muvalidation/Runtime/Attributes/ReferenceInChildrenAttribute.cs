using System;
using UnityEngine;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The referenced GameObject / Component must be this GameObject or one of its descendants.
    /// References outside the hierarchy, or to assets, are reported. Null is allowed
    /// (combine with RequireReference to forbid it). For arrays and lists, each element is checked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class ReferenceInChildrenAttribute : ValidationAttribute
    {
        public override void Validate(ValidationContext context)
        {
            var root = context.GameObject;
            if (root == null) return;

            foreach (var element in context.Elements())
            {
                if (!(element.Value is UnityEngine.Object reference) || reference == null) continue;

                var prefix = string.IsNullOrEmpty(element.Key) ? "" : element.Key + " ";
                var target = reference is GameObject go ? go : reference is Component component ? component.gameObject : null;
                if (target == null)
                {
                    context.Report($"{prefix}must reference an object in this hierarchy, not \"{reference.name}\"");
                }
                else if (!target.transform.IsChildOf(root.transform))
                {
                    context.Report($"{prefix}references \"{target.name}\" outside this hierarchy");
                }
            }
        }
    }
}
