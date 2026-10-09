using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Runs the ValidationAttributes of a Component / ScriptableObject: those on its class, on its fields,
    /// and on fields of nested [Serializable] classes / structs (including arrays and lists of them).
    /// The attributes found per type are cached for the domain lifetime.
    /// </summary>
    public static class AttributeValidator
    {
        private const int MaxNestingDepth = 4;
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private sealed class FieldPlan
        {
            public FieldInfo Field;
            public ValidationAttribute[] Attributes;

            // Fields inside the value, for [Serializable] classes / structs. Null if none have attributes.
            public FieldPlan[] Nested;
            public bool NestedIsList;
        }

        private sealed class TypePlan
        {
            public ValidationAttribute[] ClassAttributes;
            public FieldPlan[] Fields;
            public bool DependsOnOtherObjects;
            public bool HasAny => ClassAttributes.Length > 0 || Fields.Length > 0;
        }

        /// <summary>
        /// One validation attribute declared on a type. <see cref="MemberPath"/> is empty for a class
        /// attribute, otherwise a field name or a nested path such as "stats.hp" or "pages[].item".
        /// </summary>
        public readonly struct AttributeBinding
        {
            public readonly string MemberPath;
            public readonly ValidationAttribute Attribute;

            public AttributeBinding(string memberPath, ValidationAttribute attribute)
            {
                MemberPath = memberPath ?? "";
                Attribute = attribute;
            }
        }

        private static readonly Dictionary<Type, TypePlan> Plans = new Dictionary<Type, TypePlan>();
        private static Type[] _validatedComponentTypes;

        /// <summary>
        /// Non-abstract MonoBehaviour types that have validation attributes. Computed once per domain reload.
        /// </summary>
        public static Type[] ValidatedComponentTypes =>
            _validatedComponentTypes ??= TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && HasValidations(type))
                .ToArray();

        /// <summary>
        /// True if the type has any validation attribute on its class or (nested) fields.
        /// </summary>
        public static bool HasValidations(Type type)
        {
            return type != null && GetPlan(type).HasAny;
        }

        /// <summary>
        /// True if a validation of the type depends on other objects (see ValidationAttribute.DependsOnOtherObjects).
        /// </summary>
        public static bool DependsOnOtherObjects(Type type)
        {
            return type != null && GetPlan(type).DependsOnOtherObjects;
        }

        /// <summary>
        /// Validation attributes declared on this type, not ones inherited from a base type.
        /// Nested serializable fields are included to the same depth validation checks.
        /// </summary>
        public static AttributeBinding[] GetDeclaredBindings(Type type)
        {
            if (type == null) return Array.Empty<AttributeBinding>();

            var result = new List<AttributeBinding>();
            foreach (var attribute in type.GetCustomAttributes(typeof(ValidationAttribute), false).Cast<ValidationAttribute>())
            {
                result.Add(new AttributeBinding("", attribute));
            }

            AddBindings(BuildFields(type, 0, false), "", result);
            return result.ToArray();
        }

        /// <summary>
        /// Validates the target and adds problems to <paramref name="into"/>, prefixed with the label
        /// (and the field path for field attributes).
        /// </summary>
        public static void Validate(UnityEngine.Object target, string label, ValidationResult into)
        {
            if (target == null) return;

            var plan = GetPlan(target.GetType());
            if (!plan.HasAny) return;

            var location = GetLocation(target);
            foreach (var attribute in plan.ClassAttributes)
            {
                Run(attribute, target, location, null, null, label, into);
            }

            ValidateFields(plan.Fields, target, target, location, label, "", into);
        }

        /// <summary>
        /// Where the target lives: a ScriptableObject asset, a prefab (asset or Prefab Mode), or a scene.
        /// </summary>
        public static ValidationLocation GetLocation(UnityEngine.Object target)
        {
            if (!(target is Component component)) return ValidationLocation.Asset;

            var go = component.gameObject;
            if (EditorUtility.IsPersistent(go) || PrefabStageUtility.GetPrefabStage(go) != null) return ValidationLocation.Prefab;
            return ValidationLocation.Scene;
        }

        private static void ValidateFields(FieldPlan[] fields, object owner, UnityEngine.Object target, ValidationLocation location,
            string label, string pathPrefix, ValidationResult into)
        {
            foreach (var field in fields)
            {
                object value;
                try
                {
                    value = field.Field.GetValue(owner);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, target);
                    continue;
                }

                var path = pathPrefix + field.Field.Name;
                var fieldLabel = string.IsNullOrEmpty(label) ? path : $"{label}.{path}";
                foreach (var attribute in field.Attributes)
                {
                    Run(attribute, target, location, field.Field, value, fieldLabel, into);
                }

                if (field.Nested == null || value == null) continue;

                if (field.NestedIsList)
                {
                    var list = (IList)value;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null) ValidateFields(field.Nested, list[i], target, location, label, $"{path}[{i}].", into);
                    }
                }
                else
                {
                    ValidateFields(field.Nested, value, target, location, label, path + ".", into);
                }
            }
        }

        private static void Run(ValidationAttribute attribute, UnityEngine.Object target, ValidationLocation location,
            FieldInfo field, object value, string label, ValidationResult into)
        {
            var result = new ValidationResult();
            try
            {
                attribute.Validate(new ValidationContext(target, location, field, value, attribute.Severity, result));
            }
            catch (Exception e)
            {
                Debug.LogException(e, target);
                result.AddError($"{attribute.GetType().Name} threw {e.GetType().Name}: {e.Message}");
            }

            foreach (var message in result.Messages)
            {
                into.Add(message.Severity, string.IsNullOrEmpty(label) ? message.Text : $"{label}: {message.Text}");
            }
        }

        private static TypePlan GetPlan(Type type)
        {
            if (Plans.TryGetValue(type, out var plan)) return plan;

            plan = new TypePlan
            {
                ClassAttributes = type.GetCustomAttributes(typeof(ValidationAttribute), true).Cast<ValidationAttribute>().ToArray(),
                Fields = BuildFields(type, 0),
            };
            plan.DependsOnOtherObjects = plan.ClassAttributes.Any(a => a.DependsOnOtherObjects) || AnyDependsOnOtherObjects(plan.Fields);
            Plans[type] = plan;
            return plan;
        }

        private static bool AnyDependsOnOtherObjects(FieldPlan[] fields)
        {
            foreach (var field in fields)
            {
                if (field.Attributes.Any(a => a.DependsOnOtherObjects)) return true;
                if (field.Nested != null && AnyDependsOnOtherObjects(field.Nested)) return true;
            }

            return false;
        }

        private static void AddBindings(FieldPlan[] fields, string prefix, List<AttributeBinding> into)
        {
            if (fields == null) return;
            foreach (var field in fields)
            {
                var path = prefix + field.Field.Name;
                foreach (var attribute in field.Attributes)
                {
                    into.Add(new AttributeBinding(path, attribute));
                }

                if (field.Nested != null)
                {
                    AddBindings(field.Nested, field.NestedIsList ? path + "[]." : path + ".", into);
                }
            }
        }

        private static FieldPlan[] BuildFields(Type type, int depth, bool includeBaseTypes = true)
        {
            var result = new List<FieldPlan>();
            for (var t = type; t != null && t != typeof(object) && !IsUnityBaseType(t); t = includeBaseTypes ? t.BaseType : null)
            {
                foreach (var field in t.GetFields(FieldFlags))
                {
                    var attributes = field.GetCustomAttributes(typeof(ValidationAttribute), true).Cast<ValidationAttribute>().ToArray();

                    FieldPlan[] nested = null;
                    var isList = false;
                    if (depth < MaxNestingDepth && IsSerialized(field))
                    {
                        var elementType = GetListElementType(field.FieldType);
                        isList = elementType != null;
                        var valueType = elementType ?? field.FieldType;
                        if (IsNestableType(valueType))
                        {
                            var nestedFields = BuildFields(valueType, depth + 1);
                            if (nestedFields.Length > 0) nested = nestedFields;
                        }
                    }

                    if (attributes.Length == 0 && nested == null) continue;
                    result.Add(new FieldPlan { Field = field, Attributes = attributes, Nested = nested, NestedIsList = isList && nested != null });
                }
            }

            return result.ToArray();
        }

        private static bool IsUnityBaseType(Type type)
        {
            return type == typeof(MonoBehaviour) || type == typeof(ScriptableObject) || type == typeof(Behaviour)
                   || type == typeof(Component) || type == typeof(UnityEngine.Object);
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsNotSerialized) return false;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), false) || field.IsDefined(typeof(SerializeReference), false);
        }

        // [Serializable] user classes / structs whose fields may carry attributes.
        private static bool IsNestableType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)) return false;
            return type.IsSerializable;
        }

        private static Type GetListElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return null;
        }
    }
}
