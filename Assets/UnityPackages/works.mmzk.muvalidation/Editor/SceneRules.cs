using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Rules for GameObjects in scenes and Prefab Mode: default object names, unique names, prefab instance
    /// names, references to parents, and the layers allowed per tag and per component type. Which scenes they
    /// apply to is set in SceneRulesAssignments assets.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneRules", menuName = "muValidation/Scene Rules")]
    public class SceneRules : ScriptableObject
    {
        [Serializable]
        public class DefaultNameRule
        {
            [Tooltip("Name Unity gives new objects, e.g. Cube. Duplicates such as \"Cube (1)\" count as the same name.")]
            public string name = "";

            [Tooltip("Allow objects to keep this name.")]
            public bool allowed;
        }

        [Serializable]
        public class TagLayerRule
        {
            public string tag = "Untagged";

            [Tooltip("Layers that GameObjects with this tag may be on.")]
            public LayerMask allowedLayers = 1;
        }

        [Serializable]
        public class ComponentLayerRule
        {
            [Tooltip("Full name of the component type, e.g. UnityEngine.Camera.")]
            public string componentType = "";

            [Tooltip("Also apply to components derived from this type.")]
            public bool includeSubclasses = true;

            [Tooltip("Layers that GameObjects with this component may be on.")]
            public LayerMask allowedLayers = 1;
        }

        // Names Unity gives objects created from the GameObject menu. Value: allowed by default.
        internal static readonly KeyValuePair<string, bool>[] UnityDefaultNames =
        {
            new KeyValuePair<string, bool>("GameObject", false),
            new KeyValuePair<string, bool>("Cube", false),
            new KeyValuePair<string, bool>("Sphere", false),
            new KeyValuePair<string, bool>("Capsule", false),
            new KeyValuePair<string, bool>("Cylinder", false),
            new KeyValuePair<string, bool>("Plane", false),
            new KeyValuePair<string, bool>("Quad", false),
            new KeyValuePair<string, bool>("Terrain", false),
            new KeyValuePair<string, bool>("Camera", false),
            new KeyValuePair<string, bool>("Main Camera", true),
            new KeyValuePair<string, bool>("Directional Light", true),
            new KeyValuePair<string, bool>("Point Light", false),
            new KeyValuePair<string, bool>("Spot Light", false),
            new KeyValuePair<string, bool>("Area Light", false),
            new KeyValuePair<string, bool>("Reflection Probe", false),
            new KeyValuePair<string, bool>("Light Probe Group", false),
            new KeyValuePair<string, bool>("Audio Source", false),
            new KeyValuePair<string, bool>("Particle System", false),
            new KeyValuePair<string, bool>("Canvas", true),
            new KeyValuePair<string, bool>("EventSystem", true),
            new KeyValuePair<string, bool>("Panel", false),
            new KeyValuePair<string, bool>("Image", false),
            new KeyValuePair<string, bool>("RawImage", false),
            new KeyValuePair<string, bool>("Text", false),
            new KeyValuePair<string, bool>("Text (TMP)", false),
            new KeyValuePair<string, bool>("Text (Legacy)", false),
            new KeyValuePair<string, bool>("Button", false),
            new KeyValuePair<string, bool>("Button (Legacy)", false),
            new KeyValuePair<string, bool>("Toggle", false),
            new KeyValuePair<string, bool>("Slider", false),
            new KeyValuePair<string, bool>("Scrollbar", false),
            new KeyValuePair<string, bool>("Dropdown", false),
            new KeyValuePair<string, bool>("InputField", false),
            new KeyValuePair<string, bool>("InputField (TMP)", false),
            new KeyValuePair<string, bool>("Scroll View", false),
            new KeyValuePair<string, bool>("Sprite", false),
        };

        internal const string EditorOnlyTag = "EditorOnly";

        // "Cube (1)" -> "Cube"
        private static readonly Regex DuplicateSuffix = new Regex(@" \(\d+\)$");

        // full name -> component type, shared by all assets. Rebuilt on domain reload.
        private static Dictionary<string, Type> _componentTypes;

        public ValidationSeverity severity = ValidationSeverity.Error;

        [Tooltip("Names Unity gives new objects, and whether objects may keep them.")]
        public DefaultNameRule[] defaultNames = Array.Empty<DefaultNameRule>();

        [Tooltip("Tags that GameObjects may not use.")]
        [TagField]
        public string[] forbiddenTags = Array.Empty<string>();

        [Tooltip("Layers that GameObjects may not be on.")]
        public LayerMask forbiddenLayers;

        [Tooltip("Layers allowed per tag.")]
        public TagLayerRule[] tagLayers = Array.Empty<TagLayerRule>();

        [Tooltip("Layers allowed per component type.")]
        public ComponentLayerRule[] componentLayers = Array.Empty<ComponentLayerRule>();

        [Tooltip("GameObjects must be part of a prefab instance. Objects tagged EditorOnly, and their children, are exempt.")]
        public bool requirePrefabInstance;

        [Tooltip("Root GameObjects in the same scene (or Prefab Mode) may not share a name. Child objects are not checked.")]
        public bool uniqueNames;

        [Tooltip("Prefab instances must have the name of their prefab asset. A \" (1)\" style suffix added on duplication is allowed.")]
        public bool matchPrefabNames;

        [Tooltip("Script fields may not reference a parent (or any ancestor) GameObject or its components. References from a parent to its children are allowed.")]
        public bool noReferencesToParents;

        [NonSerialized] private HashSet<string> _forbiddenNames;

        public bool HasRules =>
            (defaultNames?.Length ?? 0) + (forbiddenTags?.Length ?? 0) + (tagLayers?.Length ?? 0) + (componentLayers?.Length ?? 0) > 0
            || forbiddenLayers.value != 0
            || requirePrefabInstance
            || uniqueNames
            || matchPrefabNames
            || noReferencesToParents;

        /// <summary>
        /// Adds the problems of the GameObject to <paramref name="into"/>.
        /// </summary>
        public void Validate(GameObject go, ValidationResult into)
        {
            if (go == null || severity == ValidationSeverity.None) return;

            _forbiddenNames ??= BuildForbiddenNames();
            var baseName = DuplicateSuffix.Replace(go.name, "");
            if (_forbiddenNames.Contains(baseName))
            {
                into.Add(severity, $"{name}: Default name \"{baseName}\" is not allowed");
            }

            // Not CompareTag: it logs an error for tags missing from the Tag Manager.
            if (forbiddenTags != null && !string.IsNullOrEmpty(go.tag) && Array.IndexOf(forbiddenTags, go.tag) >= 0)
            {
                into.Add(severity, $"{name}: Tag \"{go.tag}\" is not allowed");
            }

            if (IsInMask(forbiddenLayers, go.layer))
            {
                into.Add(severity, $"{name}: Layer \"{LayerName(go.layer)}\" is not allowed");
            }

            if (tagLayers != null)
            {
                foreach (var rule in tagLayers)
                {
                    if (rule == null || string.IsNullOrEmpty(rule.tag) || go.tag != rule.tag) continue;
                    if (IsInMask(rule.allowedLayers, go.layer)) continue;
                    into.Add(severity, $"{name}: Tag \"{rule.tag}\" must be on layer(s) {FormatMask(rule.allowedLayers)} (now: {LayerName(go.layer)})");
                }
            }

            if (componentLayers != null)
            {
                foreach (var rule in componentLayers)
                {
                    if (rule == null || IsInMask(rule.allowedLayers, go.layer)) continue;
                    var type = ResolveComponentType(rule.componentType);
                    if (type == null || !HasComponent(go, type, rule.includeSubclasses)) continue;
                    into.Add(severity, $"{name}: {type.Name} must be on layer(s) {FormatMask(rule.allowedLayers)} (now: {LayerName(go.layer)})");
                }
            }

            if (requirePrefabInstance && !IsExemptFromPrefabRule(go))
            {
                if (PrefabUtility.IsAddedGameObjectOverride(go))
                {
                    into.Add(severity, $"{name}: Added to a prefab instance; add it to the prefab instead");
                }
                else if (!PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    into.Add(severity, $"{name}: Must be part of a prefab instance");
                }
            }

            if (uniqueNames && go.transform.parent == null)
            {
                var count = SceneNameIndex.CountRoots(go);
                if (count > 1) into.Add(severity, $"{name}: Name \"{go.name}\" is used by {count} root objects in the scene");
            }

            if (matchPrefabNames && PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source != null && DuplicateSuffix.Replace(go.name, "") != source.name)
                {
                    into.Add(severity, $"{name}: Name must match the prefab \"{source.name}\"");
                }
            }

            if (noReferencesToParents && go.transform.parent != null)
            {
                AddParentReferences(go, into);
            }
        }

        // Reports script fields on the GameObject that reference one of its ancestors.
        private void AddParentReferences(GameObject go, ValidationResult into)
        {
            foreach (var component in go.GetComponents<MonoBehaviour>())
            {
                // Missing scripts come back as null.
                if (component == null) continue;

                using (var serializedObject = new SerializedObject(component))
                {
                    var property = serializedObject.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;

                        var reference = property.objectReferenceValue;
                        var target = reference is GameObject referenced ? referenced : reference is Component referencedComponent ? referencedComponent.gameObject : null;
                        if (target == null || target == go || !go.transform.IsChildOf(target.transform)) continue;

                        into.Add(severity, $"{name}: {component.GetType().Name}.{property.propertyPath} references parent \"{target.name}\"");
                    }
                }
            }
        }

        /// <summary>
        /// True for objects tagged EditorOnly, or under one (they are stripped from builds).
        /// </summary>
        internal static bool IsUnderEditorOnly(GameObject go)
        {
            for (var transform = go.transform; transform != null; transform = transform.parent)
            {
                // Not CompareTag: it logs an error for tags missing from the Tag Manager.
                if (transform.gameObject.tag == EditorOnlyTag) return true;
            }

            return false;
        }

        // Prefab Mode contents are all part of the prefab being edited.
        private static bool IsExemptFromPrefabRule(GameObject go)
        {
            return PrefabStageUtility.GetPrefabStage(go) != null || IsUnderEditorOnly(go);
        }

        /// <summary>
        /// Returns why the GameObject may not take the tag (on its current layer), or null if it may.
        /// </summary>
        public string GetTagRestriction(GameObject go, string tag)
        {
            if (go == null || string.IsNullOrEmpty(tag) || severity == ValidationSeverity.None) return null;

            if (forbiddenTags != null && Array.IndexOf(forbiddenTags, tag) >= 0)
            {
                return $"{name}: Tag \"{tag}\" is not allowed";
            }

            if (tagLayers != null)
            {
                foreach (var rule in tagLayers)
                {
                    if (rule == null || rule.tag != tag || IsInMask(rule.allowedLayers, go.layer)) continue;
                    return $"{name}: Tag \"{tag}\" must be on layer(s) {FormatMask(rule.allowedLayers)}";
                }
            }

            return null;
        }

        /// <summary>
        /// Returns why the GameObject may not move to the layer (with its current tag and components), or null if it may.
        /// </summary>
        public string GetLayerRestriction(GameObject go, int layer)
        {
            if (go == null || severity == ValidationSeverity.None) return null;

            if (IsInMask(forbiddenLayers, layer))
            {
                return $"{name}: Layer \"{LayerName(layer)}\" is not allowed";
            }

            if (tagLayers != null)
            {
                foreach (var rule in tagLayers)
                {
                    if (rule == null || string.IsNullOrEmpty(rule.tag) || go.tag != rule.tag) continue;
                    if (IsInMask(rule.allowedLayers, layer)) continue;
                    return $"{name}: Tag \"{rule.tag}\" must be on layer(s) {FormatMask(rule.allowedLayers)}";
                }
            }

            if (componentLayers != null)
            {
                foreach (var rule in componentLayers)
                {
                    if (rule == null || IsInMask(rule.allowedLayers, layer)) continue;
                    var type = ResolveComponentType(rule.componentType);
                    if (type == null || !HasComponent(go, type, rule.includeSubclasses)) continue;
                    return $"{name}: {type.Name} must be on layer(s) {FormatMask(rule.allowedLayers)}";
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the component type with the given full name, or null if there is none.
        /// </summary>
        public static Type ResolveComponentType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            if (_componentTypes == null)
            {
                _componentTypes = new Dictionary<string, Type>();
                foreach (var type in AllComponentTypes())
                {
                    if (type.FullName != null) _componentTypes[type.FullName] = type;
                }
            }

            return _componentTypes.TryGetValue(fullName, out var result) ? result : null;
        }

        internal static IEnumerable<Type> AllComponentTypes()
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .Append(typeof(Component))
                .Where(type => !type.IsGenericTypeDefinition);
        }

        internal void ResetDefaultNames()
        {
            defaultNames = UnityDefaultNames
                .Select(pair => new DefaultNameRule { name = pair.Key, allowed = pair.Value })
                .ToArray();
            _forbiddenNames = null;
        }

        private HashSet<string> BuildForbiddenNames()
        {
            var result = new HashSet<string>();
            if (defaultNames == null) return result;

            foreach (var rule in defaultNames)
            {
                if (rule != null && !rule.allowed && !string.IsNullOrEmpty(rule.name)) result.Add(rule.name);
            }

            return result;
        }

        private static bool HasComponent(GameObject go, Type type, bool includeSubclasses)
        {
            if (includeSubclasses) return go.GetComponent(type) != null;

            foreach (var component in go.GetComponents(type))
            {
                if (component != null && component.GetType() == type) return true;
            }

            return false;
        }

        private static bool IsInMask(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;

        internal static string[] LayerNames(LayerMask mask)
        {
            var names = new List<string>();
            for (var layer = 0; layer < 32; layer++)
            {
                if (IsInMask(mask, layer)) names.Add(LayerName(layer));
            }

            return names.ToArray();
        }

        private static string FormatMask(LayerMask mask)
        {
            var names = new List<string>();
            for (var layer = 0; layer < 32; layer++)
            {
                if (IsInMask(mask, layer)) names.Add(LayerName(layer));
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }

        private static string LayerName(int layer)
        {
            var layerName = LayerMask.LayerToName(layer);
            return string.IsNullOrEmpty(layerName) ? layer.ToString() : layerName;
        }

        private void Reset()
        {
            ResetDefaultNames();
        }

        private void OnValidate()
        {
            _forbiddenNames = null;
            SceneRulesRegistry.Invalidate();
        }
    }

    /// <summary>
    /// Number of root GameObjects per name in each scene, for the unique name rule. Built per scene on first use.
    /// Cleared when the hierarchy changes, and by callers that validate a batch of objects, so a batch sees
    /// the current names.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneNameIndex
    {
        // scene handle -> name -> count
        private static readonly Dictionary<int, Dictionary<string, int>> CountsByScene = new Dictionary<int, Dictionary<string, int>>();

        static SceneNameIndex()
        {
            EditorApplication.hierarchyChanged -= Clear;
            EditorApplication.hierarchyChanged += Clear;
        }

        public static void Clear()
        {
            CountsByScene.Clear();
        }

        /// <summary>
        /// Returns the number of root GameObjects in the GameObject's scene with its name, itself included.
        /// Objects hidden from the Hierarchy are not counted.
        /// </summary>
        public static int CountRoots(GameObject go)
        {
            var scene = go.scene;
            if (!scene.IsValid()) return 1;

            if (!CountsByScene.TryGetValue(scene.handle, out var counts))
            {
                counts = new Dictionary<string, int>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if ((root.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                    counts.TryGetValue(root.name, out var current);
                    counts[root.name] = current + 1;
                }

                CountsByScene[scene.handle] = counts;
            }

            return counts.TryGetValue(go.name, out var count) ? count : 1;
        }
    }
}
