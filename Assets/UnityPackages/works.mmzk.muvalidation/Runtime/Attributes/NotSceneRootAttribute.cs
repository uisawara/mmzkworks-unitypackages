using System;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The GameObject must not be placed at the scene root. Checked in scenes only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class NotSceneRootAttribute : ValidationAttribute
    {
        public override void Validate(ValidationContext context)
        {
            var go = context.GameObject;
            if (go == null || context.Location != ValidationLocation.Scene) return;
            if (go.transform.parent == null) context.Report("Must not be placed at the scene root");
        }
    }
}
