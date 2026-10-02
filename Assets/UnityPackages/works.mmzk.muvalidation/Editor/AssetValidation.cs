using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Runs validation attributes on ScriptableObject assets and on components in prefabs,
    /// and caches the result per GUID.
    ///
    /// Results of prefabs and validated ScriptableObjects are stored with the asset's dependency hash.
    /// After an import (<see cref="MarkStale"/>) each is checked against the current hash on next use and
    /// re-validated only if the asset or its import dependencies (e.g. nested prefabs) changed.
    /// Inspector edits drop that asset's result; deletes and moves clear everything (<see cref="Invalidate"/>).
    ///
    /// Content-dependent results are saved to Library/ before domain reloads and on quit
    /// (<see cref="AssetValidationStore"/>), and loaded back as stale entries that recheck their hash.
    /// </summary>
    [InitializeOnLoad]
    public static class AssetValidation
    {
        private struct CacheEntry
        {
            public ValidationSummary Summary;

            // True if the result depends on the asset's content (prefabs, validated ScriptableObjects).
            // Others depend only on the asset type, which does not change without a domain reload.
            public bool ContentDependent;
            public Hash128 Hash;
            public int Generation;
        }

        // guid -> result (Valid = valid / not a target)
        private static readonly Dictionary<string, CacheEntry> ResultCache = new Dictionary<string, CacheEntry>();

        // Incremented after imports; entries from older generations recheck their dependency hash.
        private static int _generation;

        // Assets edited in the Inspector and not saved yet. Not persisted: after a restart the edits are gone.
        private static readonly HashSet<string> UnsavedGuids = new HashSet<string>();

        private static bool _loaded;

        // Paths of scripts (or DLLs) defining a MonoBehaviour with validation attributes.
        // Used to skip loading prefabs that cannot contain one. Rebuilt on domain reload.
        private static HashSet<string> _validatedScriptPaths;

        static AssetValidation()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            AssemblyReloadEvents.beforeAssemblyReload -= Save;
            AssemblyReloadEvents.beforeAssemblyReload += Save;
            EditorApplication.quitting -= Save;
            EditorApplication.quitting += Save;
        }

        /// <summary>
        /// Clears all results.
        /// </summary>
        public static void Invalidate()
        {
            _loaded = true;
            ResultCache.Clear();
            ValidationStatus.RequestRescan();
            EditorApplication.RepaintProjectWindow();
        }

        /// <summary>
        /// Called after imports: cached results are kept, but recheck their dependency hash on next use.
        /// </summary>
        public static void MarkStale(IEnumerable<string> importedGuids)
        {
            _generation++;
            foreach (var guid in importedGuids) UnsavedGuids.Remove(guid);
        }

        /// <summary>
        /// Clears all results including the saved cache file.
        /// </summary>
        [MenuItem("Tools/muValidation/Clear Validation Cache")]
        public static void ClearCache()
        {
            AssetValidationStore.Delete();
            Invalidate();
        }

        /// <summary>
        /// Returns the validation result for the asset. Valid if it is not a target.
        /// </summary>
        public static ValidationSummary GetSummary(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return ValidationSummary.Valid;
            EnsureLoaded();

            string path = null;
            if (ResultCache.TryGetValue(guid, out var cached))
            {
                if (!cached.ContentDependent || cached.Generation == _generation) return cached.Summary;

                path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.GetAssetDependencyHash(path) == cached.Hash)
                {
                    cached.Generation = _generation;
                    ResultCache[guid] = cached;
                    return cached.Summary;
                }
            }

            path ??= AssetDatabase.GUIDToAssetPath(guid);
            var summary = Evaluate(path, out var contentDependent);
            ResultCache[guid] = new CacheEntry
            {
                Summary = summary,
                ContentDependent = contentDependent,
                Hash = contentDependent ? AssetDatabase.GetAssetDependencyHash(path) : default,
                Generation = _generation,
            };
            return summary;
        }

        public static ValidationSummary Evaluate(string assetPath)
        {
            return Evaluate(assetPath, out _);
        }

        private static ValidationSummary Evaluate(string assetPath, out bool contentDependent)
        {
            contentDependent = false;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return ValidationSummary.Valid;

            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (type == null) return ValidationSummary.Valid;

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                if (!AttributeValidator.HasValidations(type)) return ValidationSummary.Valid;
                contentDependent = true;
                return ValidateScriptableObject(assetPath);
            }

            if (type == typeof(GameObject) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // A prefab may gain validated components later, so its result always depends on content.
                contentDependent = true;
                return MayContainValidations(assetPath) ? ValidatePrefab(assetPath) : ValidationSummary.Valid;
            }

            return ValidationSummary.Valid;
        }

        private static ValidationSummary ValidateScriptableObject(string assetPath)
        {
            var result = new ValidationResult();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset == null || !AttributeValidator.HasValidations(asset.GetType())) continue;
                var label = AssetDatabase.IsMainAsset(asset) ? null : asset.name;
                AttributeValidator.Validate(asset, label, result);
            }

            return ValidationSummary.From(result);
        }

        private static ValidationSummary ValidatePrefab(string assetPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (root == null) return ValidationSummary.Valid;

            var result = new ValidationResult();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Missing scripts come back as null.
                if (component == null || !AttributeValidator.HasValidations(component.GetType())) continue;
                var label = $"{GetPath(component.transform, root.transform)} ({component.GetType().Name})";
                AttributeValidator.Validate(component, label, result);
            }

            return ValidationSummary.From(result);
        }

        private static bool MayContainValidations(string prefabPath)
        {
            var scriptPaths = GetValidatedScriptPaths();
            if (scriptPaths.Count == 0) return false;

            // Recursive, so components in nested prefabs are covered too.
            foreach (var dependency in AssetDatabase.GetDependencies(prefabPath, true))
            {
                if (scriptPaths.Contains(dependency)) return true;
            }

            return false;
        }

        private static HashSet<string> GetValidatedScriptPaths()
        {
            if (_validatedScriptPaths != null) return _validatedScriptPaths;

            var types = new HashSet<Type>(AttributeValidator.ValidatedComponentTypes);

            var paths = new HashSet<string>();
            if (types.Count > 0)
            {
                foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
                {
                    var scriptClass = script.GetClass();
                    if (scriptClass == null || !types.Contains(scriptClass)) continue;
                    var path = AssetDatabase.GetAssetPath(script);
                    if (!string.IsNullOrEmpty(path)) paths.Add(path);
                }
            }

            _validatedScriptPaths = paths;
            return _validatedScriptPaths;
        }

        internal static string GetPath(Transform transform, Transform root)
        {
            var path = transform.name;
            while (transform != root && transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            var changed = false;
            for (var i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
                stream.GetChangeAssetObjectPropertiesEvent(i, out var args);
                var guid = args.guid.ToString();
                ResultCache.Remove(guid);
                UnsavedGuids.Add(guid);
                ValidationStatus.Refresh(guid);
                changed = true;
            }

            if (changed) EditorApplication.RepaintProjectWindow();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            foreach (var entry in AssetValidationStore.Load())
            {
                if (ResultCache.ContainsKey(entry.Guid)) continue;

                // An older generation, so the hash is checked before the result is used.
                ResultCache[entry.Guid] = new CacheEntry
                {
                    Summary = entry.Summary,
                    ContentDependent = true,
                    Hash = entry.Hash,
                    Generation = _generation - 1,
                };
            }
        }

        private static void Save()
        {
            // Nothing new to save if the file was never loaded and nothing was validated.
            if (!_loaded && ResultCache.Count == 0) return;
            EnsureLoaded();

            AssetValidationStore.Save(ResultCache
                .Where(pair => pair.Value.ContentDependent && !UnsavedGuids.Contains(pair.Key))
                .Select(pair => new AssetValidationStore.Entry(pair.Key, pair.Value.Hash, pair.Value.Summary)));
        }
    }
}
