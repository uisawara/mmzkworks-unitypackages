using System;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The string field must not be null, empty or whitespace. For arrays and lists, each element is checked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class NotEmptyAttribute : ValidationAttribute
    {
        public override void Validate(ValidationContext context)
        {
            foreach (var element in context.Elements())
            {
                if (element.Value is string s && !string.IsNullOrWhiteSpace(s)) continue;
                context.Report(string.IsNullOrEmpty(element.Key) ? "Must not be empty" : $"{element.Key} must not be empty");
            }
        }
    }
}
