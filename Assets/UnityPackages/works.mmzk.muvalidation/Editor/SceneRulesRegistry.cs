using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Collects the assignments of all SceneRulesAssignments assets under Assets and applies the assigned
    /// SceneRules to scene GameObjects by scene path. SceneRules that no assignment refers to are not applied.
    /// The lists are rebuilt when a SceneRules or SceneRulesAssignments asset changes, is imported, moved or deleted.
    /// </summary>
    public static class SceneRulesRegistry
    {
        private readonly struct Entry
        {
            public readonly string Path;
            public readonly SceneRules Rules;

            public Entry(string path, SceneRules rules)
            {
                Path = path;
                Rules = rules;
            }
        }

        private static List<Entry> _entries;

        // scene (or prefab) path, "" for unsaved scenes -> rules that apply to it
        private static readonly Dictionary<string, List<SceneRules>> RulesByScene = new Dictionary<string, List<SceneRules>>();

        // scene path -> asset paths of the rules that apply to it, for GetRulePaths
        private static readonly Dictionary<string, string[]> RulePathsByScene = new Dictionary<string, string[]>();

        // Loading an asset triggers its OnValidate, which must not reset the list being built.
        private static bool _building;

        public static void Invalidate()
        {
            if (_building) return;
            _entries = null;
            RulesByScene.Clear();
            RulePathsByScene.Clear();
            SceneValidation.Invalidate();
            EditorApplication.RepaintHierarchyWindow();
        }

        /// <summary>
        /// True if any SceneRules with rules is assigned, i.e. every scene GameObject needs to be validated.
        /// Not per scene: objects in scenes that no assignment covers are visited but get no problems.
        /// </summary>
        public static bool HasRules
        {
            get
            {
                EnsureLoaded();
                return _entries.Any(entry => entry.Rules.HasRules);
            }
        }

        /// <summary>
        /// True if any assigned SceneRules forbids duplicate root names. Renaming a root object then changes the
        /// results of other root objects with the old or new name.
        /// </summary>
        public static bool HasUniqueNameRules
        {
            get
            {
                EnsureLoaded();
                return _entries.Any(entry => entry.Rules.uniqueNames && entry.Rules.severity != ValidationSeverity.None);
            }
        }

        /// <summary>
        /// Adds the problems found by the SceneRules assigned to the GameObject's scene to <paramref name="into"/>.
        /// In Prefab Mode the prefab's path is used.
        /// </summary>
        public static void Validate(GameObject go, ValidationResult into)
        {
            if (go == null) return;
            foreach (var rules in GetRulesFor(GetScenePath(go)))
            {
                if (rules != null) rules.Validate(go, into);
            }
        }

        /// <summary>
        /// Returns the SceneRules assigned to the scene (or prefab) path, each once.
        /// </summary>
        public static List<SceneRules> GetRulesFor(string scenePath)
        {
            EnsureLoaded();
            scenePath ??= "";
            if (RulesByScene.TryGetValue(scenePath, out var result)) return result;

            result = _entries
                .Where(entry => entry.Rules.HasRules && SceneRulesAssignments.Covers(entry.Path, scenePath))
                .Select(entry => entry.Rules)
                .Distinct()
                .ToList();
            RulesByScene[scenePath] = result;
            return result;
        }

        /// <summary>
        /// Entry point for muHierarchy (called via reflection, so it uses only primitive types).
        /// Returns the asset paths of the SceneRules that apply to the scene, or an empty array if none apply.
        /// </summary>
        /// <remarks>muHierarchy looks this method up by name. Keep its signature stable.</remarks>
        public static string[] GetRulePaths(string scenePath)
        {
            scenePath ??= "";
            if (RulePathsByScene.TryGetValue(scenePath, out var cached)) return cached;

            var paths = GetRulesFor(scenePath).Select(AssetDatabase.GetAssetPath).Where(path => !string.IsNullOrEmpty(path)).ToArray();
            RulePathsByScene[scenePath] = paths;
            return paths;
        }

        /// <summary>
        /// Entry point for muHierarchy (called via reflection).
        /// Returns why the GameObject may not take the tag under the SceneRules of its scene, or null if it may.
        /// </summary>
        /// <remarks>muHierarchy looks this method up by name. Keep its signature stable.</remarks>
        public static string GetTagRestriction(GameObject go, string tag)
        {
            if (go == null) return null;
            foreach (var rules in GetRulesFor(GetScenePath(go)))
            {
                var restriction = rules != null ? rules.GetTagRestriction(go, tag) : null;
                if (restriction != null) return restriction;
            }

            return null;
        }

        /// <summary>
        /// Entry point for muHierarchy (called via reflection).
        /// Returns why the GameObject may not move to the layer under the SceneRules of its scene, or null if it may.
        /// </summary>
        /// <remarks>muHierarchy looks this method up by name. Keep its signature stable.</remarks>
        public static string GetLayerRestriction(GameObject go, int layer)
        {
            if (go == null) return null;
            foreach (var rules in GetRulesFor(GetScenePath(go)))
            {
                var restriction = rules != null ? rules.GetLayerRestriction(go, layer) : null;
                if (restriction != null) return restriction;
            }

            return null;
        }

        /// <summary>
        /// Returns the paths the SceneRules is assigned to.
        /// </summary>
        public static List<string> GetAssignedPaths(SceneRules rules)
        {
            EnsureLoaded();
            return _entries.Where(entry => entry.Rules == rules).Select(entry => entry.Path).Distinct().ToList();
        }

        private static string GetScenePath(GameObject go)
        {
            if (!EditorSceneManager.IsPreviewScene(go.scene)) return go.scene.path;
            var stage = PrefabStageUtility.GetPrefabStage(go);
            return stage != null ? stage.assetPath : "";
        }

        private static void EnsureLoaded()
        {
            if (_entries != null) return;

            var entries = new List<Entry>();
            var paths = AssetDatabase.FindAssets("t:" + nameof(SceneRulesAssignments), new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal);

            _building = true;
            try
            {
                foreach (var path in paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<SceneRulesAssignments>(path);
                    if (asset == null || asset.assignments == null) continue;

                    foreach (var assignment in asset.assignments)
                    {
                        if (assignment?.rules == null) continue;
                        var folder = SceneRulesAssignments.NormalizePath(assignment.path);
                        foreach (var rules in assignment.rules)
                        {
                            if (rules != null) entries.Add(new Entry(folder, rules));
                        }
                    }
                }
            }
            finally
            {
                _building = false;
            }

            _entries = entries;
        }
    }
}
