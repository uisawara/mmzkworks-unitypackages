using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Runs validation attributes on components of GameObjects in loaded scenes and Prefab Mode,
    /// and SceneRules on the GameObjects themselves, for display in the Hierarchy window.
    ///
    /// Only GameObjects with validated components are visited (all GameObjects if there are SceneRules):
    /// they are found per validated component type, and their problems are added up the parent chain to give
    /// per-object child counts.
    /// Property changes re-validate just the changed object (plus objects whose validations depend on other
    /// objects); structural changes rebuild the index. Play Mode is skipped unless enabled from the menu.
    /// </summary>
    /// <remarks>
    /// muHierarchy calls <see cref="GetSeverityIncludingChildren"/> via reflection. Keep its signature stable.
    /// </remarks>
    [InitializeOnLoad]
    public static class SceneValidation
    {
        private const string PlayModeMenuPath = "Tools/muValidation/Validate In Play Mode";
        private const string PlayModeKey = "muValidation.ValidateInPlayMode";
        private const double PlayModeRebuildSeconds = 0.5;

        private readonly struct ChildCounts
        {
            public readonly int Errors;
            public readonly int Warnings;

            public ChildCounts(int errors, int warnings)
            {
                Errors = errors;
                Warnings = warnings;
            }
        }

        // GameObject instance ID -> result for its own components. Only GameObjects with validated components.
        private static readonly Dictionary<int, ValidationSummary> OwnResults = new Dictionary<int, ValidationSummary>();

        // GameObject instance ID -> number of descendants with errors / only warnings. Only ancestors of invalid objects.
        private static readonly Dictionary<int, ChildCounts> ChildResults = new Dictionary<int, ChildCounts>();

        // GameObjects whose validations depend on other objects; re-validated after any change.
        private static readonly HashSet<GameObject> DependentObjects = new HashSet<GameObject>();

        // GameObjects tagged EditorOnly at the last build. Changing that tag changes the prefab rule result
        // of all descendants, so it triggers a rebuild.
        private static readonly HashSet<int> EditorOnlyObjects = new HashSet<int>();

        private static bool _dirty = true;
        private static double _lastBuildTime;

        static SceneValidation()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            Undo.undoRedoPerformed -= Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
            EditorApplication.delayCall += () => Menu.SetChecked(PlayModeMenuPath, ValidateInPlayMode);
        }

        /// <summary>
        /// Validate scene objects during Play Mode too. Off by default: runtime state changes constantly,
        /// and rebuilding results every 0.5 seconds costs frame time.
        /// </summary>
        public static bool ValidateInPlayMode
        {
            get => EditorPrefs.GetBool(PlayModeKey, false);
            set
            {
                EditorPrefs.SetBool(PlayModeKey, value);
                Menu.SetChecked(PlayModeMenuPath, value);
                Invalidate();
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        [MenuItem(PlayModeMenuPath, false, 2007)]
        private static void ToggleValidateInPlayMode()
        {
            ValidateInPlayMode = !ValidateInPlayMode;
        }

        /// <summary>
        /// Marks all results stale. They are rebuilt on next use.
        /// </summary>
        public static void Invalidate()
        {
            _dirty = true;
        }

        /// <summary>
        /// Returns the validation result for the GameObject's own components.
        /// </summary>
        public static ValidationSummary GetSummary(GameObject go)
        {
            if (go == null || !EnsureBuilt()) return ValidationSummary.Valid;
            return OwnResults.TryGetValue(go.GetInstanceID(), out var summary) ? summary : ValidationSummary.Valid;
        }

        /// <summary>
        /// Returns the result for the GameObject, including a summary line for descendants with problems.
        /// </summary>
        public static ValidationSummary GetSummaryIncludingChildren(GameObject go)
        {
            var summary = GetSummary(go);
            if (go == null || !ChildResults.TryGetValue(go.GetInstanceID(), out var counts)) return summary;

            if (counts.Errors > 0)
            {
                summary = ValidationSummary.Combine(summary, ValidationSummary.Error($"{counts.Errors} child object(s) have validation errors"));
            }

            if (counts.Warnings > 0)
            {
                var message = $"{counts.Warnings} child object(s) have validation warnings";
                summary = ValidationSummary.Combine(summary, new ValidationSummary(ValidationSeverity.Warning, message));
            }

            return summary;
        }

        /// <summary>
        /// Entry point for muHierarchy (called via reflection, so it uses only primitive types).
        /// Returns the severity as int (0 = valid, 1 = warning, 2 = error) and the tooltip message.
        /// </summary>
        public static int GetSeverityIncludingChildren(GameObject go, out string message)
        {
            var summary = GetSummaryIncludingChildren(go);
            message = summary.Message;
            return (int)summary.Severity;
        }

        // Returns false when validation is off (Play Mode without the option).
        private static bool EnsureBuilt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !ValidateInPlayMode) return false;
            if (!_dirty) return true;

            // In Play Mode, rebuild at most every PlayModeRebuildSeconds and show the previous results meanwhile.
            var now = EditorApplication.timeSinceStartup;
            if (EditorApplication.isPlaying && now - _lastBuildTime < PlayModeRebuildSeconds) return true;

            Build();
            _lastBuildTime = now;
            return true;
        }

        private static void Build()
        {
            _dirty = false;
            OwnResults.Clear();
            ChildResults.Clear();
            DependentObjects.Clear();
            EditorOnlyObjects.Clear();

            foreach (var go in FindValidatedObjects())
            {
                if (go.tag == SceneRules.EditorOnlyTag) EditorOnlyObjects.Add(go.GetInstanceID());
                var summary = Validate(go, out var dependsOnOthers);
                OwnResults[go.GetInstanceID()] = summary;
                if (dependsOnOthers) DependentObjects.Add(go);
                AddToAncestors(go, summary.Severity, 1);
            }

            // Play Mode results are not refreshed by change events, so keep them rebuilding.
            if (EditorApplication.isPlaying) _dirty = true;
        }

        private static HashSet<GameObject> FindValidatedObjects()
        {
            var result = new HashSet<GameObject>();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var stageRoot = stage != null ? stage.prefabContentsRoot : null;

            // SceneRules apply to every GameObject.
            if (SceneRulesRegistry.HasRules)
            {
                foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!EditorUtility.IsPersistent(transform) && (transform.hideFlags & HideFlags.HideInHierarchy) == 0)
                    {
                        result.Add(transform.gameObject);
                    }
                }

                if (stageRoot != null)
                {
                    foreach (var transform in stageRoot.GetComponentsInChildren<Transform>(true)) result.Add(transform.gameObject);
                }
            }

            var types = AttributeValidator.ValidatedComponentTypes;
            if (types.Length == 0) return result;

            // Loaded scenes. Includes subclasses, so a type may be visited more than once; the set dedupes.
            foreach (var type in types)
            {
                foreach (var found in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (found is Component component && !EditorUtility.IsPersistent(component)) result.Add(component.gameObject);
                }
            }

            // Prefab Mode objects live in a preview scene that FindObjectsByType does not search.
            if (stageRoot != null)
            {
                foreach (var component in stageRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && AttributeValidator.HasValidations(component.GetType())) result.Add(component.gameObject);
                }
            }

            return result;
        }

        private static ValidationSummary Validate(GameObject go, out bool dependsOnOthers)
        {
            dependsOnOthers = false;
            var result = new ValidationResult();
            foreach (var component in go.GetComponents<MonoBehaviour>())
            {
                // Missing scripts come back as null.
                if (component == null) continue;
                var type = component.GetType();
                if (!AttributeValidator.HasValidations(type)) continue;

                dependsOnOthers |= AttributeValidator.DependsOnOtherObjects(type);
                AttributeValidator.Validate(component, type.Name, result);
            }

            SceneRulesRegistry.Validate(go, result);
            return ValidationSummary.From(result);
        }

        /// <summary>
        /// Re-validates one GameObject and updates its ancestors' counts. Its parent chain must not have
        /// changed since the last build (parent changes trigger a rebuild instead).
        /// </summary>
        private static void Refresh(GameObject go)
        {
            if (go == null) return;

            var id = go.GetInstanceID();
            OwnResults.TryGetValue(id, out var previous);

            var summary = Validate(go, out var dependsOnOthers);
            if (dependsOnOthers) DependentObjects.Add(go);
            else DependentObjects.Remove(go);

            // Keep entries only for GameObjects with validated components (all GameObjects if there are SceneRules).
            if (SceneRulesRegistry.HasRules || HasValidatedComponent(go)) OwnResults[id] = summary;
            else OwnResults.Remove(id);

            if (previous.Severity == summary.Severity) return;
            AddToAncestors(go, previous.Severity, -1);
            AddToAncestors(go, summary.Severity, 1);
        }

        private static bool HasValidatedComponent(GameObject go)
        {
            foreach (var component in go.GetComponents<MonoBehaviour>())
            {
                if (component != null && AttributeValidator.HasValidations(component.GetType())) return true;
            }

            return false;
        }

        private static void AddToAncestors(GameObject go, ValidationSeverity severity, int delta)
        {
            if (severity == ValidationSeverity.None) return;

            for (var parent = go.transform.parent; parent != null; parent = parent.parent)
            {
                var id = parent.gameObject.GetInstanceID();
                ChildResults.TryGetValue(id, out var counts);
                counts = severity == ValidationSeverity.Error
                    ? new ChildCounts(counts.Errors + delta, counts.Warnings)
                    : new ChildCounts(counts.Errors, counts.Warnings + delta);

                if (counts.Errors <= 0 && counts.Warnings <= 0) ChildResults.Remove(id);
                else ChildResults[id] = counts;
            }
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (_dirty || EditorApplication.isPlaying) return;

            var changed = new HashSet<GameObject>();
            for (var i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var properties);
                        AddGameObject(changed, properties.instanceId);
                        break;

                    case ObjectChangeKind.ChangeGameObjectStructure:
                        stream.GetChangeGameObjectStructureEvent(i, out var structure);
                        AddGameObject(changed, structure.instanceId);
                        break;

                    // Do not affect scene validation (asset results are handled by AssetValidation).
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                    case ObjectChangeKind.CreateAssetObject:
                    case ObjectChangeKind.DestroyAssetObject:
                    case ObjectChangeKind.ChangeChildrenOrder:
                        break;

                    // Parent changes, creation, deletion, prefab updates, scene changes...
                    default:
                        Invalidate();
                        return;
                }
            }

            if (changed.Count == 0) return;

            if (SceneRulesRegistry.HasRules)
            {
                foreach (var go in changed)
                {
                    if ((go.tag == SceneRules.EditorOnlyTag) == EditorOnlyObjects.Contains(go.GetInstanceID())) continue;
                    Invalidate();
                    EditorApplication.RepaintHierarchyWindow();
                    return;
                }
            }

            foreach (var go in changed) Refresh(go);

            // Names or existence of other objects may have changed.
            DependentObjects.RemoveWhere(go => go == null);
            foreach (var go in new List<GameObject>(DependentObjects))
            {
                if (!changed.Contains(go)) Refresh(go);
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private static void AddGameObject(HashSet<GameObject> set, int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go != null && !EditorUtility.IsPersistent(go)) set.Add(go);
        }

        // In edit mode ObjectChangeEvents reports changes in detail, so this is only needed in Play Mode,
        // where runtime changes produce no ObjectChangeEvents.
        private static void OnHierarchyChanged()
        {
            if (EditorApplication.isPlaying) Invalidate();
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) => Invalidate();

        private static void OnSceneClosed(UnityEngine.SceneManagement.Scene scene) => Invalidate();

        // Save As changes the scene path, and with it which SceneRules apply.
        private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            if (SceneRulesRegistry.HasRules) Invalidate();
        }

        private static void OnNewSceneCreated(UnityEngine.SceneManagement.Scene scene, NewSceneSetup setup, NewSceneMode mode) => Invalidate();

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            Invalidate();
        }

        private static void OnPrefabStageChanged(PrefabStage stage)
        {
            Invalidate();
        }

        private class Postprocessor : AssetPostprocessor
        {
            // Deleted assets turn references into missing ones. Changed prefabs arrive as
            // UpdatePrefabInstances in ObjectChangeEvents, so plain imports need no rebuild.
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (deleted.Length > 0) Invalidate();
            }
        }
    }
}
