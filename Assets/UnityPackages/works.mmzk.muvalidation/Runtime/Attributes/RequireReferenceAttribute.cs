using System;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The field must not be null (or a missing / destroyed reference).
    /// For arrays and lists, each element is checked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class RequireReferenceAttribute : ValidationAttribute
    {
        public override void Validate(ValidationContext context)
        {
            foreach (var element in context.Elements())
            {
                if (!IsNull(element.Value)) continue;
                context.Report(string.IsNullOrEmpty(element.Key) ? "Must be set" : $"{element.Key} must be set");
            }
        }

        private static bool IsNull(object value)
        {
            // UnityEngine.Object overloads == to also treat destroyed / missing objects as null.
            if (value is UnityEngine.Object unityObject) return unityObject == null;
            return value == null;
        }
    }
}
