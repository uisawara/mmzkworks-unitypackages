using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muShortcutPanel.Editor
{
    /// <summary>Helpers shared by the commands. The string / path functions are pure and unit tested.</summary>
    internal static class ShortcutCommandUtil
    {
        // ------------------------------------------------------------------ names

        /// <summary>
        /// Expands <c>{name}</c> (the original name) and <c>{index}</c> (1-based position in the selection).
        /// An empty pattern keeps the original name.
        /// </summary>
        public static string ExpandName(string pattern, string name, int index)
        {
            if (string.IsNullOrEmpty(pattern)) return name ?? "";
            return pattern
                .Replace("{name}", name ?? "")
                .Replace("{index}", index.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Removes characters that cannot be used in a file name.</summary>
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars).Trim();
        }

        // ------------------------------------------------------------------ vectors

        public static bool TryParseVector3(string text, out Vector3 v)
        {
            v = Vector3.zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split(',');
            if (parts.Length != 3) return false;
            var values = new float[3];
            for (var i = 0; i < 3; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
            }
            v = new Vector3(values[0], values[1], values[2]);
            return true;
        }

        public static string FormatVector3(Vector3 v) =>
            string.Join(",", new[] { v.x, v.y, v.z }.Select(f => f.ToString("R", CultureInfo.InvariantCulture)));

        // ------------------------------------------------------------------ asset paths

        /// <summary>
        /// "Assets\Foo\" -> "Assets/Foo". Returns null when the path is not inside Assets/ (or Packages/).
        /// </summary>
        public static string NormalizeAssetFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var p = path.Trim().Replace('\\', '/').TrimEnd('/');
            while (p.Contains("//")) p = p.Replace("//", "/");
            if (p == "Assets" || p.StartsWith("Assets/", StringComparison.Ordinal)) return p;
            if (p.StartsWith("Packages/", StringComparison.Ordinal) && p.Count(c => c == '/') >= 1) return p;
            return null;
        }

        /// <summary>Absolute path from a folder dialog -> project-relative path, or null when it is outside the project.</summary>
        public static string ToProjectRelative(string absolute, string projectRoot)
        {
            if (string.IsNullOrEmpty(absolute) || string.IsNullOrEmpty(projectRoot)) return null;
            var abs = absolute.Replace('\\', '/').TrimEnd('/');
            var root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            if (!abs.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            return NormalizeAssetFolder(abs.Substring(root.Length));
        }

        private static System.Reflection.MethodInfo _tryGetActiveFolderPath;
        private static bool _activeFolderLookedUp;

        /// <summary>
        /// The folder currently open in the Project window (the one "Create" menus use), or "Assets" when it cannot be read.
        /// Uses the internal <c>ProjectWindowUtil.TryGetActiveFolderPath</c>.
        /// </summary>
        public static string ActiveProjectFolder()
        {
            try
            {
                if (!_activeFolderLookedUp)
                {
                    _activeFolderLookedUp = true;
                    _tryGetActiveFolderPath = typeof(ProjectWindowUtil).GetMethod("TryGetActiveFolderPath",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                }

                if (_tryGetActiveFolderPath != null)
                {
                    var args = new object[] { null };
                    if ((bool)_tryGetActiveFolderPath.Invoke(null, args))
                    {
                        var folder = NormalizeAssetFolder(args[0] as string);
                        if (folder != null && AssetDatabase.IsValidFolder(folder)) return folder;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[muShortcutPanel] Could not read the Project window folder: {e.GetBaseException().Message}");
            }
            return "Assets";
        }

        /// <summary>Creates the folder (and missing parents) under Assets/. Returns false when the path is invalid.</summary>
        public static bool EnsureFolder(string folder)
        {
            folder = NormalizeAssetFolder(folder);
            if (folder == null) return false;
            if (AssetDatabase.IsValidFolder(folder)) return true;
            if (!folder.StartsWith("Assets", StringComparison.Ordinal)) return false;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) || !EnsureFolder(parent)) return false;
            var guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            return !string.IsNullOrEmpty(guid);
        }

        // ------------------------------------------------------------------ hierarchy

        /// <summary>"/Env//Trees/Oak " -> ["Env", "Trees", "Oak"]</summary>
        public static string[] SplitHierarchyPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Array.Empty<string>();
            return path.Split('/').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        /// <summary>"Env/Trees/Oak" for a transform (from its root).</summary>
        public static string HierarchyPath(Transform t)
        {
            var names = new List<string>();
            for (var c = t; c != null; c = c.parent) names.Add(c.name);
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>
        /// Root GameObjects to search: the open prefab's contents while in Prefab Mode, otherwise the active scene
        /// (or every loaded scene when <paramref name="allScenes"/> is true).
        /// </summary>
        public static List<GameObject> SearchRoots(bool allScenes)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) return new List<GameObject> { stage.prefabContentsRoot };

            var roots = new List<GameObject>();
            if (allScenes)
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
                }
            }
            else
            {
                roots.AddRange(SceneManager.GetActiveScene().GetRootGameObjects());
            }
            return roots;
        }

        /// <summary>All GameObjects whose path from the root is exactly <paramref name="path"/> (inactive ones included).</summary>
        public static List<GameObject> FindByPath(IEnumerable<GameObject> roots, string path)
        {
            var segments = SplitHierarchyPath(path);
            var result = new List<GameObject>();
            if (segments.Length == 0) return result;

            var current = roots.Where(r => r != null && r.name == segments[0]).Select(r => r.transform).ToList();
            for (var i = 1; i < segments.Length && current.Count > 0; i++)
            {
                var next = new List<Transform>();
                foreach (var t in current)
                {
                    foreach (Transform child in t)
                    {
                        if (child.name == segments[i]) next.Add(child);
                    }
                }
                current = next;
            }
            result.AddRange(current.Select(t => t.gameObject));
            return result;
        }

        /// <summary>Every GameObject under the roots, inactive ones included, in hierarchy order.</summary>
        public static IEnumerable<GameObject> AllUnder(IEnumerable<GameObject> roots) =>
            roots.Where(r => r != null).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);

        /// <summary>Drops objects whose ancestor is also in the list (so a parent and its child are not handled twice).</summary>
        public static List<GameObject> TopLevelOnly(IEnumerable<GameObject> objects)
        {
            var list = objects.Where(o => o != null).Distinct().ToList();
            var set = new HashSet<GameObject>(list);
            return list.Where(o =>
            {
                for (var p = o.transform.parent; p != null; p = p.parent)
                {
                    if (set.Contains(p.gameObject)) return false;
                }
                return true;
            }).ToList();
        }

        // ------------------------------------------------------------------ types

        /// <summary>
        /// Finds a Component type by full name ("UnityEngine.Rigidbody") or short name ("Rigidbody").
        /// A short name that matches several types is an error.
        /// </summary>
        public static Type ResolveComponentType(string name, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "No component type set";
                return null;
            }

            name = name.Trim();
            var types = TypeCache.GetTypesDerivedFrom<Component>().Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition);
            var candidates = types.ToList();

            var exact = candidates.FirstOrDefault(t => t.FullName == name);
            if (exact != null) return exact;

            var byName = candidates.Where(t => t.Name == name).ToList();
            if (byName.Count == 1) return byName[0];
            if (byName.Count > 1)
            {
                // Prefer the engine's own type ("Rigidbody" -> UnityEngine.Rigidbody) when there is exactly one.
                var engine = byName.Where(t => t.Namespace == "UnityEngine").ToList();
                if (engine.Count == 1) return engine[0];
                error = $"Ambiguous component type \"{name}\": {string.Join(", ", byName.Select(t => t.FullName))}";
                return null;
            }

            error = $"Component type not found: {name}";
            return null;
        }
    }
}
