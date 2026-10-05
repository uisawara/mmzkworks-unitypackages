using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// One problem found by a validation run: an asset, or a GameObject in a scene.
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity;

        /// <summary>Asset path, or "Scene: Root/Child" for scene objects.</summary>
        public string Location;

        public string Message;

        /// <summary>Set for asset issues. The asset is loaded only when needed.</summary>
        public string AssetPath;

        /// <summary>Set for scene issues. Null once the scene is closed.</summary>
        public GameObject SceneObject;

        /// <summary>Set for scene issues: the scene asset, used when the scene has been closed.</summary>
        public string ScenePath;

        /// <summary>
        /// The object to ping / select: the GameObject, the asset, or the scene asset if the scene was closed.
        /// </summary>
        public Object Resolve()
        {
            if (SceneObject != null) return SceneObject;
            var path = AssetPath ?? ScenePath;
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
        }
    }

    /// <summary>
    /// Collects validation issues for the Validation window and the pre-build check.
    /// </summary>
    public static class ValidationRunner
    {
        /// <summary>
        /// Validates every asset under Assets (FileNameRules and validation attributes).
        /// Returns false if the user canceled the progress bar.
        /// </summary>
        public static bool CollectAssets(List<ValidationIssue> into, bool showProgress)
        {
            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    if (showProgress && i % 50 == 0
                        && EditorUtility.DisplayCancelableProgressBar("Validation", "Validating assets...", (float)i / guids.Length))
                    {
                        return false;
                    }

                    var summary = ValidationStatus.GetSummary(guids[i]);
                    if (summary.IsValid) continue;

                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    into.Add(new ValidationIssue { Severity = summary.Severity, Location = path, Message = summary.Message, AssetPath = path });
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            return true;
        }

        /// <summary>
        /// Validates GameObjects in all loaded scenes and the open Prefab Mode stage (validation attributes and SceneRules).
        /// </summary>
        public static void CollectOpenScenes(List<ValidationIssue> into)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                CollectHierarchy(stage.prefabContentsRoot, "Prefab Mode: ", stage.assetPath, into);
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) CollectScene(scene, into);
            }
        }

        /// <summary>
        /// Validates the enabled scenes in Build Settings. Scenes that are not loaded are opened additively
        /// and closed again afterwards, so the user's scene setup is kept.
        /// </summary>
        public static void CollectBuildScenes(List<ValidationIssue> into, bool showProgress)
        {
            var scenes = EditorBuildSettings.scenes;
            try
            {
                for (var i = 0; i < scenes.Length; i++)
                {
                    var path = scenes[i].path;
                    if (!scenes[i].enabled || string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                    if (showProgress) EditorUtility.DisplayProgressBar("Validation", $"Validating {path}", (float)i / scenes.Length);

                    var scene = SceneManager.GetSceneByPath(path);
                    if (scene.isLoaded)
                    {
                        CollectScene(scene, into);
                        continue;
                    }

                    // In the hierarchy but unloaded: load it, then unload it again (keep it in the hierarchy).
                    var wasInHierarchy = scene.IsValid();
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        CollectScene(scene, into);
                    }
                    finally
                    {
                        EditorSceneManager.CloseScene(scene, !wasInHierarchy);
                    }
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }
        }

        public static void CollectScene(Scene scene, List<ValidationIssue> into)
        {
            var sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            foreach (var root in scene.GetRootGameObjects())
            {
                CollectHierarchy(root, sceneName + ": ", scene.path, into);
            }
        }

        // Validates the validated components, SceneRules and missing scripts under root, one issue per GameObject.
        private static void CollectHierarchy(GameObject root, string locationPrefix, string scenePath, List<ValidationIssue> into)
        {
            SceneNameIndex.Clear();
            var results = new Dictionary<GameObject, ValidationResult>();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Missing scripts come back as null.
                if (component == null) continue;
                var type = component.GetType();
                if (!AttributeValidator.HasValidations(type)) continue;

                if (!results.TryGetValue(component.gameObject, out var result))
                {
                    results[component.gameObject] = result = new ValidationResult();
                }

                AttributeValidator.Validate(component, type.Name, result);
            }

            if (SceneValidation.VisitsAllObjects)
            {
                var detectMissingScripts = ValidationSettings.instance.detectMissingScripts;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!results.TryGetValue(transform.gameObject, out var result))
                    {
                        results[transform.gameObject] = result = new ValidationResult();
                    }

                    if (detectMissingScripts) SceneValidation.ValidateMissingScripts(transform.gameObject, result);
                    SceneRulesRegistry.Validate(transform.gameObject, result);
                }
            }

            foreach (var pair in results)
            {
                var summary = ValidationSummary.From(pair.Value);
                if (summary.IsValid) continue;

                into.Add(new ValidationIssue
                {
                    Severity = summary.Severity,
                    Location = locationPrefix + AssetValidation.GetPath(pair.Key.transform, null),
                    Message = summary.Message,
                    SceneObject = pair.Key,
                    ScenePath = scenePath,
                });
            }
        }
    }
}
