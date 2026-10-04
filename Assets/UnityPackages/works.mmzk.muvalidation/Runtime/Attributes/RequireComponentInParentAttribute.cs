using System;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The GameObject or one of its parents must have a component of the given type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequireComponentInParentAttribute : ValidationAttribute
    {
        public Type ComponentType { get; }

        // Parents can change without this object changing.
        public override bool DependsOnOtherObjects => true;

        public RequireComponentInParentAttribute(Type componentType)
        {
            ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        }

        public override void Validate(ValidationContext context)
        {
            var go = context.GameObject;
            if (go == null) return;
            if (go.GetComponentInParent(ComponentType, true) != null) return;
            context.Report($"Requires {ComponentType.Name} on this GameObject or a parent");
        }
    }
}
