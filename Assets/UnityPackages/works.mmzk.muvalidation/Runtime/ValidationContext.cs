using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// Where the validated object lives. Some checks only make sense in a scene.
    /// </summary>
    public enum ValidationLocation
    {
        /// <summary>A ScriptableObject asset.</summary>
        Asset,

        /// <summary>A component in a prefab asset or in Prefab Mode.</summary>
        Prefab,

        /// <summary>A component on a GameObject in a scene.</summary>
        Scene,
    }

    /// <summary>
    /// What a ValidationAttribute checks, and where it reports problems.
    /// </summary>
    public sealed class ValidationContext
    {
        private readonly ValidationResult _result;

        public ValidationContext(Object target, ValidationLocation location, FieldInfo field, object value,
            ValidationSeverity severity, ValidationResult result)
        {
            Target = target;
            Location = location;
            Field = field;
            Value = value;
            Severity = severity;
            _result = result;
        }

        /// <summary>The Component or ScriptableObject being validated.</summary>
        public Object Target { get; }

        /// <summary>The GameObject of the target, or null for a ScriptableObject.</summary>
        public GameObject GameObject => Target is Component component ? component.gameObject : null;

        public ValidationLocation Location { get; }

        /// <summary>The field the attribute is on, or null for a class attribute.</summary>
        public FieldInfo Field { get; }

        /// <summary>The value of <see cref="Field"/>. Null for a class attribute.</summary>
        public object Value { get; }

        /// <summary>Severity used by <see cref="Report(string)"/>.</summary>
        public ValidationSeverity Severity { get; }

        public void Report(string message) => _result.Add(Severity, message);

        public void Report(ValidationSeverity severity, string message) => _result.Add(severity, message);

        /// <summary>
        /// Same target and field, with another severity and (optionally) another result to report to.
        /// Used to run nested validations.
        /// </summary>
        public ValidationContext With(ValidationSeverity severity, ValidationResult result = null)
        {
            return new ValidationContext(Target, Location, Field, Value, severity, result ?? _result);
        }

        /// <summary>
        /// The field value as a sequence: each element with its label ("[0]", "[1]", ...) for arrays and lists,
        /// or the value itself with an empty label.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Elements()
        {
            if (Value is IList list && !(Value is string))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    yield return new KeyValuePair<string, object>($"[{i}]", list[i]);
                }
            }
            else
            {
                yield return new KeyValuePair<string, object>("", Value);
            }
        }
    }
}
