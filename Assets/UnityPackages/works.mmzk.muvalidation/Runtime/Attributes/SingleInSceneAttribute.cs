using System;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The scene must contain only one component of this type (inactive objects included). Checked in scenes only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SingleInSceneAttribute : ValidationAttribute
    {
        public override bool DependsOnOtherObjects => true;

        public override void Validate(ValidationContext context)
        {
            var go = context.GameObject;
            if (go == null || context.Location != ValidationLocation.Scene || !go.scene.IsValid()) return;

            var type = context.Target.GetType();
            var count = 0;
            foreach (var root in go.scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren(type, true).Length;
            }

            if (count > 1) context.Report($"Scene \"{go.scene.name}\" has {count} {type.Name}; only one is allowed");
        }
    }
}
