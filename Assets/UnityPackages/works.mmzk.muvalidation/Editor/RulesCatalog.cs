using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Snapshot of every configured muValidation rule: file name rules, scene rules, their assignments,
    /// and validation attributes declared on Component / ScriptableObject types.
    /// The Rules window and <see cref="ValidationCommandLine"/> both use this.
    /// </summary>
    public sealed class RulesCatalog
    {
        public FileNameRuleSet[] FileNames = Array.Empty<FileNameRuleSet>();
        public SceneRuleSet[] Scenes = Array.Empty<SceneRuleSet>();
        public AssignmentSet[] Assignments = Array.Empty<AssignmentSet>();
        public AttributeTypeEntry[] Attributes = Array.Empty<AttributeTypeEntry>();

        /// <summary>
        /// Reads rule assets under Assets and validation attributes from loaded types.
        /// </summary>
        public static RulesCatalog Collect()
        {
            return new RulesCatalog
            {
                FileNames = CollectFileNames(),
                Scenes = CollectScenes(),
                Assignments = CollectAssignments(),
                Attributes = CollectAttributes(),
            };
        }

        /// <summary>
        /// Pretty JSON with fileNameRules, sceneRules, sceneRulesAssignments and validationAttributes.
        /// </summary>
        public string ToJson()
        {
            var root = new JsonObject();
            root.Set("fileNameRules", ToArray(FileNames, item => item.ToJsonObject()));
            root.Set("sceneRules", ToArray(Scenes, item => item.ToJsonObject()));
            root.Set("sceneRulesAssignments", ToArray(Assignments, item => item.ToJsonObject()));
            root.Set("validationAttributes", ToArray(Attributes, item => item.ToJsonObject()));
            return root.ToString();
        }

        /// <summary>
        /// Writes <see cref="ToJson"/> as UTF-8. Creates the directory if it is missing.
        /// </summary>
        public void Write(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is required.", nameof(path));
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
        }

        private static FileNameRuleSet[] CollectFileNames()
        {
            var result = new List<FileNameRuleSet>();
            foreach (var path in AssetPaths<FileNameRules>())
            {
                var asset = AssetDatabase.LoadAssetAtPath<FileNameRules>(path);
                if (asset == null) continue;

                result.Add(new FileNameRuleSet
                {
                    Path = path,
                    Name = asset.name,
                    Folder = asset.Folder,
                    AllowOtherFiles = asset.allowOtherFiles,
                    ValidateFolders = asset.validateFolders,
                    Rules = CollectFileRules(asset),
                });
            }

            return result.ToArray();
        }

        private static FileNamePatternRule[] CollectFileRules(FileNameRules asset)
        {
            if (asset.rules == null) return Array.Empty<FileNamePatternRule>();

            var result = new List<FileNamePatternRule>();
            foreach (var rule in asset.rules)
            {
                if (rule == null) continue;
                var folder = asset.ResolveFolder(rule);
                result.Add(new FileNamePatternRule
                {
                    Path = rule.path ?? "",
                    Folder = folder,
                    FolderExists = folder != null && AssetDatabase.IsValidFolder(folder),
                    Patterns = CollectPatterns(rule.patterns),
                });
            }

            return result.ToArray();
        }

        private static FileNamePattern[] CollectPatterns(string[] patterns)
        {
            if (patterns == null) return Array.Empty<FileNamePattern>();

            var result = new List<FileNamePattern>();
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                var entry = new FileNamePattern { Pattern = pattern, Valid = true };
                try
                {
                    _ = new Regex(pattern);
                }
                catch (ArgumentException e)
                {
                    entry.Valid = false;
                    entry.Error = e.Message;
                }

                result.Add(entry);
            }

            return result.ToArray();
        }

        private static SceneRuleSet[] CollectScenes()
        {
            var assets = new List<SceneRules>();
            var paths = new List<string>();
            foreach (var path in AssetPaths<SceneRules>())
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneRules>(path);
                if (asset == null) continue;
                assets.Add(asset);
                paths.Add(path);
            }

            var result = new List<SceneRuleSet>();
            for (var i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                result.Add(new SceneRuleSet
                {
                    Path = paths[i],
                    Name = asset.name,
                    Severity = asset.severity.ToString(),
                    HasRules = asset.HasRules,
                    AssignedTo = SceneRulesRegistry.GetAssignedPaths(asset).OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                    DefaultNames = CollectDefaultNames(asset.defaultNames),
                    ForbiddenTags = NonNull(asset.forbiddenTags),
                    ForbiddenLayers = SceneRules.LayerNames(asset.forbiddenLayers),
                    TagLayers = CollectTagLayers(asset.tagLayers),
                    ComponentLayers = CollectComponentLayers(asset.componentLayers),
                    RequirePrefabInstance = asset.requirePrefabInstance,
                    UniqueNames = asset.uniqueNames,
                    MatchPrefabNames = asset.matchPrefabNames,
                    NoReferencesToParents = asset.noReferencesToParents,
                });
            }

            return result.ToArray();
        }

        private static DefaultNameEntry[] CollectDefaultNames(SceneRules.DefaultNameRule[] rules)
        {
            if (rules == null) return Array.Empty<DefaultNameEntry>();
            var result = new List<DefaultNameEntry>();
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                result.Add(new DefaultNameEntry { Name = rule.name ?? "", Allowed = rule.allowed });
            }

            return result.ToArray();
        }

        private static TagLayerEntry[] CollectTagLayers(SceneRules.TagLayerRule[] rules)
        {
            if (rules == null) return Array.Empty<TagLayerEntry>();
            var result = new List<TagLayerEntry>();
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                result.Add(new TagLayerEntry
                {
                    Tag = rule.tag ?? "",
                    AllowedLayers = SceneRules.LayerNames(rule.allowedLayers),
                });
            }

            return result.ToArray();
        }

        private static ComponentLayerEntry[] CollectComponentLayers(SceneRules.ComponentLayerRule[] rules)
        {
            if (rules == null) return Array.Empty<ComponentLayerEntry>();
            var result = new List<ComponentLayerEntry>();
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                result.Add(new ComponentLayerEntry
                {
                    ComponentType = rule.componentType ?? "",
                    Resolved = SceneRules.ResolveComponentType(rule.componentType) != null,
                    IncludeSubclasses = rule.includeSubclasses,
                    AllowedLayers = SceneRules.LayerNames(rule.allowedLayers),
                });
            }

            return result.ToArray();
        }

        private static AssignmentSet[] CollectAssignments()
        {
            var result = new List<AssignmentSet>();
            foreach (var path in AssetPaths<SceneRulesAssignments>())
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneRulesAssignments>(path);
                if (asset == null) continue;
                result.Add(new AssignmentSet
                {
                    Path = path,
                    Name = asset.name,
                    Assignments = CollectAssignmentEntries(asset.assignments),
                });
            }

            return result.ToArray();
        }

        private static AssignmentEntry[] CollectAssignmentEntries(SceneRulesAssignments.Assignment[] assignments)
        {
            if (assignments == null) return Array.Empty<AssignmentEntry>();

            var result = new List<AssignmentEntry>();
            foreach (var assignment in assignments)
            {
                if (assignment == null) continue;
                var rules = new List<string>();
                var missing = 0;
                if (assignment.rules != null)
                {
                    foreach (var rule in assignment.rules)
                    {
                        var rulePath = rule != null ? AssetDatabase.GetAssetPath(rule) : null;
                        if (string.IsNullOrEmpty(rulePath)) missing++;
                        else rules.Add(rulePath);
                    }
                }

                var folder = SceneRulesAssignments.NormalizePath(assignment.path);
                result.Add(new AssignmentEntry
                {
                    Path = folder,
                    Exists = AssignmentExists(folder),
                    Rules = rules.ToArray(),
                    MissingRules = missing,
                });
            }

            return result.ToArray();
        }

        private static AttributeTypeEntry[] CollectAttributes()
        {
            var result = new List<AttributeTypeEntry>();
            var types = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Concat(TypeCache.GetTypesDerivedFrom<ScriptableObject>())
                .Where(type => type != null && !(type.IsGenericType && !type.IsGenericTypeDefinition))
                .OrderBy(type => type.FullName ?? type.Name, StringComparer.Ordinal);

            foreach (var type in types)
            {
                var bindings = AttributeValidator.GetDeclaredBindings(type);
                if (bindings.Length == 0) continue;

                var rules = new List<AttributeRule>();
                var stack = new HashSet<ValidationAttribute>();
                foreach (var binding in bindings)
                {
                    var rule = DescribeAttribute(binding.Attribute, binding.MemberPath, stack, 0);
                    if (rule != null) rules.Add(rule);
                }

                result.Add(new AttributeTypeEntry
                {
                    TypeName = type.FullName ?? type.Name,
                    Kind = typeof(ScriptableObject).IsAssignableFrom(type) ? "ScriptableObject" : "Component",
                    Assembly = type.Assembly.GetName().Name,
                    ScriptType = type,
                    Attributes = rules.ToArray(),
                });
            }

            return result.ToArray();
        }

        private const int MaxCompositeDepth = 8;

        private static AttributeRule DescribeAttribute(ValidationAttribute attribute, string member, HashSet<ValidationAttribute> stack, int depth)
        {
            if (attribute == null) return null;

            var rule = new AttributeRule
            {
                Member = member,
                TypeName = attribute.GetType().FullName ?? attribute.GetType().Name,
                Severity = attribute.Severity.ToString(),
                DependsOnOtherObjects = attribute.DependsOnOtherObjects,
                Properties = ReadProperties(attribute),
            };

            if (!(attribute is CompositeValidationAttribute)) return rule;
            if (depth >= MaxCompositeDepth || !stack.Add(attribute))
            {
                rule.Error = depth >= MaxCompositeDepth ? "Composite validations are nested too deeply" : "Cycle in composite validations";
                rule.Children = Array.Empty<AttributeRule>();
                return rule;
            }

            try
            {
                var children = new List<AttributeRule>();
                foreach (var child in InvokeCreateValidations(attribute))
                {
                    var described = DescribeAttribute(child, null, stack, depth + 1);
                    if (described != null) children.Add(described);
                }

                rule.Children = children.ToArray();
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException invocation ? invocation.InnerException ?? e : e;
                rule.Error = inner.Message;
                rule.Children = Array.Empty<AttributeRule>();
            }
            finally
            {
                stack.Remove(attribute);
            }

            return rule;
        }

        private static ValidationAttribute[] InvokeCreateValidations(ValidationAttribute attribute)
        {
            var method = FindCreateValidations(attribute.GetType());
            if (method == null) throw new MissingMethodException(attribute.GetType().FullName, "CreateValidations");

            var value = method.Invoke(attribute, null) as IEnumerable<ValidationAttribute>;
            if (value == null) return Array.Empty<ValidationAttribute>();
            return value.Where(item => item != null).ToArray();
        }

        private static MethodInfo FindCreateValidations(Type type)
        {
            while (type != null && type != typeof(ValidationAttribute))
            {
                var method = type.GetMethod("CreateValidations", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null) return method;
                type = type.BaseType;
            }

            return null;
        }

        private static PropertyEntry[] ReadProperties(ValidationAttribute attribute)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var entries = new List<PropertyEntry>();
            var type = attribute.GetType();

            foreach (var property in type.GetProperties(flags))
            {
                if (SkipProperty(property)) continue;
                entries.Add(new PropertyEntry { Name = property.Name, Value = ReadMember(() => property.GetValue(attribute)) });
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.DeclaringType == typeof(Attribute)) continue;
                if (typeof(ValidationAttribute).IsAssignableFrom(field.FieldType)) continue;
                if (entries.Exists(entry => entry.Name == field.Name)) continue;
                entries.Add(new PropertyEntry { Name = field.Name, Value = ReadMember(() => field.GetValue(attribute)) });
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return entries.ToArray();
        }

        private static bool SkipProperty(PropertyInfo property)
        {
            if (property.GetIndexParameters().Length > 0 || property.GetGetMethod(false) == null) return true;
            if (property.DeclaringType == typeof(Attribute)) return true;
            if (property.Name == nameof(ValidationAttribute.Severity)) return true;
            if (property.Name == nameof(ValidationAttribute.DependsOnOtherObjects)) return true;
            if (typeof(ValidationAttribute).IsAssignableFrom(property.PropertyType)) return true;
            return false;
        }

        private static object ReadMember(Func<object> read)
        {
            try
            {
                return Normalize(read());
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException invocation ? invocation.InnerException ?? e : e;
                return "<error: " + inner.Message + ">";
            }
        }

        private static object Normalize(object value, int depth = 0)
        {
            if (value == null || value is string || value is bool) return value;
            if (depth > 4) return value.ToString();
            if (value is UnityEngine.Object unityObject) return unityObject != null ? unityObject.name : null;
            if (value is Type type) return type.FullName ?? type.Name;
            if (value is Enum) return value.ToString();

            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case TypeCode.UInt64:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (value is IEnumerable enumerable)
            {
                var items = new List<object>();
                foreach (var item in enumerable) items.Add(Normalize(item, depth + 1));
                return items.ToArray();
            }

            return value.ToString();
        }

        private static bool AssignmentExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path) || File.Exists(path)) return true;
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            return !string.IsNullOrEmpty(root) && File.Exists(Path.GetFullPath(Path.Combine(root, path)));
        }

        private static string[] AssetPaths<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] NonNull(string[] values)
        {
            if (values == null) return Array.Empty<string>();
            return values.Where(value => value != null).ToArray();
        }

        private static JsonArray ToArray<T>(T[] items, Func<T, JsonObject> map)
        {
            var array = new JsonArray();
            if (items == null) return array;
            foreach (var item in items)
            {
                if (item != null) array.Add(map(item));
            }

            return array;
        }

        private static JsonArray Strings(string[] values)
        {
            var array = new JsonArray();
            if (values != null)
            {
                foreach (var value in values) array.Add(value);
            }

            return array;
        }

        public sealed class FileNameRuleSet
        {
            public string Path;
            public string Name;
            public string Folder;
            public bool AllowOtherFiles;
            public bool ValidateFolders;
            public FileNamePatternRule[] Rules = Array.Empty<FileNamePatternRule>();

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("path", Path);
                obj.Set("name", Name);
                obj.Set("folder", Folder);
                obj.Set("allowOtherFiles", AllowOtherFiles);
                obj.Set("validateFolders", ValidateFolders);
                obj.Set("rules", ToArray(Rules, rule => rule.ToJsonObject()));
                return obj;
            }
        }

        public sealed class FileNamePatternRule
        {
            public string Path;
            public string Folder;
            public bool FolderExists;
            public FileNamePattern[] Patterns = Array.Empty<FileNamePattern>();

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("path", Path ?? "");
                obj.Set("folder", Folder);
                obj.Set("folderExists", FolderExists);
                obj.Set("patterns", ToArray(Patterns, pattern => pattern.ToJsonObject()));
                return obj;
            }
        }

        public sealed class FileNamePattern
        {
            public string Pattern;
            public bool Valid;
            public string Error;

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("pattern", Pattern ?? "");
                obj.Set("valid", Valid);
                if (!Valid) obj.Set("error", Error ?? "");
                return obj;
            }
        }

        public sealed class SceneRuleSet
        {
            public string Path;
            public string Name;
            public string Severity;
            public bool HasRules;
            public string[] AssignedTo = Array.Empty<string>();
            public DefaultNameEntry[] DefaultNames = Array.Empty<DefaultNameEntry>();
            public string[] ForbiddenTags = Array.Empty<string>();
            public string[] ForbiddenLayers = Array.Empty<string>();
            public TagLayerEntry[] TagLayers = Array.Empty<TagLayerEntry>();
            public ComponentLayerEntry[] ComponentLayers = Array.Empty<ComponentLayerEntry>();
            public bool RequirePrefabInstance;
            public bool UniqueNames;
            public bool MatchPrefabNames;
            public bool NoReferencesToParents;

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal string DefaultNamesJson() => ToArray(DefaultNames, entry => entry.ToJsonObject()).ToString();
            internal string ForbiddenTagsJson() => Strings(ForbiddenTags).ToString();
            internal string ForbiddenLayersJson() => Strings(ForbiddenLayers).ToString();

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("path", Path);
                obj.Set("name", Name);
                obj.Set("severity", Severity);
                obj.Set("hasRules", HasRules);
                obj.Set("assignedTo", Strings(AssignedTo));
                obj.Set("defaultNames", ToArray(DefaultNames, entry => entry.ToJsonObject()));
                obj.Set("forbiddenTags", Strings(ForbiddenTags));
                obj.Set("forbiddenLayers", Strings(ForbiddenLayers));
                obj.Set("tagLayers", ToArray(TagLayers, entry => entry.ToJsonObject()));
                obj.Set("componentLayers", ToArray(ComponentLayers, entry => entry.ToJsonObject()));
                obj.Set("requirePrefabInstance", RequirePrefabInstance);
                obj.Set("uniqueNames", UniqueNames);
                obj.Set("matchPrefabNames", MatchPrefabNames);
                obj.Set("noReferencesToParents", NoReferencesToParents);
                return obj;
            }
        }

        public sealed class DefaultNameEntry
        {
            public string Name;
            public bool Allowed;

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("name", Name ?? "");
                obj.Set("allowed", Allowed);
                return obj;
            }
        }

        public sealed class TagLayerEntry
        {
            public string Tag;
            public string[] AllowedLayers = Array.Empty<string>();

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("tag", Tag ?? "");
                obj.Set("allowedLayers", Strings(AllowedLayers));
                return obj;
            }
        }

        public sealed class ComponentLayerEntry
        {
            public string ComponentType;
            public bool Resolved;
            public bool IncludeSubclasses;
            public string[] AllowedLayers = Array.Empty<string>();

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("componentType", ComponentType ?? "");
                obj.Set("resolved", Resolved);
                obj.Set("includeSubclasses", IncludeSubclasses);
                obj.Set("allowedLayers", Strings(AllowedLayers));
                return obj;
            }
        }

        public sealed class AssignmentSet
        {
            public string Path;
            public string Name;
            public AssignmentEntry[] Assignments = Array.Empty<AssignmentEntry>();

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("path", Path);
                obj.Set("name", Name);
                obj.Set("assignments", ToArray(Assignments, entry => entry.ToJsonObject()));
                return obj;
            }
        }

        public sealed class AssignmentEntry
        {
            public string Path;
            public bool Exists;
            public string[] Rules = Array.Empty<string>();
            public int MissingRules;

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("path", Path ?? "");
                obj.Set("exists", Exists);
                obj.Set("rules", Strings(Rules));
                obj.Set("missingRules", MissingRules);
                return obj;
            }
        }

        public sealed class AttributeTypeEntry
        {
            public string TypeName;
            public string Kind;
            public string Assembly;
            public AttributeRule[] Attributes = Array.Empty<AttributeRule>();

            /// <summary>Type to ping in the Rules window. Not part of the JSON.</summary>
            internal Type ScriptType;

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                obj.Set("type", TypeName);
                obj.Set("kind", Kind);
                obj.Set("assembly", Assembly);
                obj.Set("attributes", ToArray(Attributes, rule => rule.ToJsonObject()));
                return obj;
            }
        }

        public sealed class AttributeRule
        {
            /// <summary>Empty for a class attribute. Null for a validation nested in a composite.</summary>
            public string Member;
            public string TypeName;
            public string Severity;
            public bool DependsOnOtherObjects;
            public PropertyEntry[] Properties = Array.Empty<PropertyEntry>();

            /// <summary>Set for a composite attribute, including when expansion failed. Null otherwise.</summary>
            public AttributeRule[] Children;
            public string Error;

            public string ShortName
            {
                get
                {
                    var name = TypeName ?? "";
                    var dot = name.LastIndexOf('.');
                    if (dot >= 0) name = name.Substring(dot + 1);
                    const string suffix = "Attribute";
                    if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                    {
                        name = name.Substring(0, name.Length - suffix.Length);
                    }

                    return name;
                }
            }

            private string _json;
            internal string Json => _json ?? (_json = ToJsonObject().ToString());

            internal JsonObject ToJsonObject()
            {
                var obj = new JsonObject();
                if (Member != null) obj.Set("member", Member);
                obj.Set("type", TypeName);
                obj.Set("severity", Severity);
                obj.Set("dependsOnOtherObjects", DependsOnOtherObjects);
                var properties = new JsonObject();
                if (Properties != null)
                {
                    foreach (var property in Properties) properties.Set(property.Name, property.Value);
                }

                obj.Set("properties", properties);
                if (Children != null) obj.Set("children", ToArray(Children, child => child.ToJsonObject()));
                if (!string.IsNullOrEmpty(Error)) obj.Set("error", Error);
                return obj;
            }
        }

        public sealed class PropertyEntry
        {
            public string Name;
            public object Value;
        }
    }
}
