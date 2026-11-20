using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muDashboard.Editor
{
    /// <summary>
    /// Shortcuts to GameObjects in scenes (dropped from the Hierarchy).
    /// An item stores the object's <see cref="GlobalObjectId"/>, the scene asset (guid / path) and the hierarchy path
    /// at the time it was registered. The object is found only by its id and the scene only by its GUID; there is no
    /// fallback by path, so a link either resolves to the same object or is reported as missing.
    /// The stored scene path and hierarchy path are only used for display while the scene is not open or missing.
    /// </summary>
    [InitializeOnLoad]
    internal static class DashboardSceneObjects
    {
        // GlobalObjectIdentifierToObjectSlow is expensive, so resolved objects are cached and failed lookups are retried
        // only occasionally (or right after the scenes / hierarchy change).
        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, double> RetryAt = new Dictionary<string, double>();
        private const double RetryInterval = 2.0;

        static DashboardSceneObjects()
        {
            EditorSceneManager.sceneOpened += (_, __) => RetryAt.Clear();
            EditorSceneManager.sceneClosed += _ => RetryAt.Clear();
            EditorApplication.hierarchyChanged += () => RetryAt.Clear();
        }

        /// <summary>
        /// The GameObject to register for a dragged object (a GameObject, or the GameObject of a component),
        /// or null when it is not a GameObject in a scene.
        /// </summary>
        public static GameObject ToSceneGameObject(Object obj)
        {
            if (obj == null || EditorUtility.IsPersistent(obj)) return null;
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            return go != null && go.scene.IsValid() ? go : null;
        }

        /// <summary>False with a reason when <paramref name="go"/> cannot be stored (e.g. its scene was never saved).</summary>
        public static bool CanRegister(GameObject go, out string error)
        {
            error = null;
            if (go == null)
            {
                error = "Not a scene object";
                return false;
            }
            if (string.IsNullOrEmpty(go.scene.path))
            {
                error = $"Save the scene first to add \"{go.name}\"";
                return false;
            }
            return true;
        }

        /// <summary>Fills the scene object fields of <paramref name="item"/> from <paramref name="go"/>.</summary>
        public static void Assign(DashboardItem item, GameObject go)
        {
            item.kind = ShortcutItemKind.SceneObject;
            item.globalId = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
            item.path = go.scene.path;
            item.guid = AssetDatabase.AssetPathToGUID(go.scene.path);
            item.hierarchy = ShortcutCommandUtil.HierarchyPath(go.transform);
            Cache[item.globalId] = go;
            RetryAt.Remove(item.globalId);
        }

        /// <summary>
        /// Current path of the item's scene asset, looked up by its GUID (follows moves / renames),
        /// or null when no scene with that GUID exists.
        /// </summary>
        public static string ScenePath(DashboardItem item)
        {
            if (string.IsNullOrEmpty(item?.guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(item.guid);
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
        }

        /// <summary>The loaded scene the item belongs to, or an invalid scene when it is not open.</summary>
        public static Scene LoadedScene(DashboardItem item)
        {
            var path = ScenePath(item);
            if (path == null) return default;
            var scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded ? scene : default;
        }

        public static bool IsSceneOpen(DashboardItem item) => LoadedScene(item).IsValid();

        /// <summary>The GameObject, or null when its scene is not open or it no longer exists.</summary>
        public static GameObject Resolve(DashboardItem item)
        {
            if (item == null) return null;
            var key = string.IsNullOrEmpty(item.globalId) ? item.id : item.globalId;

            if (Cache.TryGetValue(key, out var cached))
            {
                if (cached != null && cached.scene.IsValid() && cached.scene.isLoaded) return cached;
                Cache.Remove(key);
            }

            var now = EditorApplication.timeSinceStartup;
            if (RetryAt.TryGetValue(key, out var retry) && now < retry) return null;

            GameObject go = null;
            if (IsSceneOpen(item) && GlobalObjectId.TryParse(item.globalId, out var id))
                go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;

            if (go != null)
            {
                Cache[key] = go;
                RetryAt.Remove(key);
            }
            else
            {
                RetryAt[key] = now + RetryInterval;
            }
            return go;
        }

        /// <summary>Name shown for the item: the object's name, or the last part of the stored hierarchy path.</summary>
        public static string ObjectName(DashboardItem item, GameObject go)
        {
            if (go != null) return go.name;
            var segments = ShortcutCommandUtil.SplitHierarchyPath(item.hierarchy);
            return segments.Length > 0 ? segments[segments.Length - 1] : "GameObject";
        }

        /// <summary>"Main: Env/Trees" — scene name and the parent path of the object.</summary>
        public static string Location(DashboardItem item, GameObject go)
        {
            var sceneName = Path.GetFileNameWithoutExtension(ScenePath(item) ?? item.path);
            var path = go != null ? ShortcutCommandUtil.HierarchyPath(go.transform) : item.hierarchy;
            var slash = path?.LastIndexOf('/') ?? -1;
            var parent = slash > 0 ? path.Substring(0, slash) : "";
            return string.IsNullOrEmpty(parent) ? sceneName : $"{sceneName}: {parent}";
        }

        /// <summary>"Assets/Scenes/Main.unity: Env/Trees/Oak"</summary>
        public static string FullPath(DashboardItem item, GameObject go)
        {
            var path = go != null ? ShortcutCommandUtil.HierarchyPath(go.transform) : item.hierarchy;
            return $"{ScenePath(item) ?? item.path}: {path}";
        }

        /// <summary>Selects the object in the Hierarchy. Fails when its scene is not open or it no longer exists.</summary>
        public static bool TrySelect(DashboardItem item, out string error)
        {
            error = null;
            if (ScenePath(item) == null)
            {
                error = $"Missing scene: {item.path}";
                return false;
            }
            if (!IsSceneOpen(item))
            {
                error = $"Scene is not open: {Path.GetFileNameWithoutExtension(item.path)}";
                return false;
            }

            RetryAt.Remove(string.IsNullOrEmpty(item.globalId) ? item.id : item.globalId);
            var go = Resolve(item);
            if (go == null)
            {
                error = $"Missing: {item.hierarchy}";
                return false;
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            return true;
        }

        /// <summary>
        /// Opens the item's scene after asking (Open / Open Additive / Cancel). Returns true when the scene is open afterwards.
        /// </summary>
        public static bool AskToOpenScene(DashboardItem item)
        {
            var scenePath = ScenePath(item);
            if (scenePath == null) return false;
            if (IsSceneOpen(item)) return true;

            var choice = EditorUtility.DisplayDialogComplex("muDashboard",
                $"\"{ObjectName(item, null)}\" is in the scene \"{Path.GetFileNameWithoutExtension(scenePath)}\", which is not open.",
                "Open Scene", "Cancel", "Open Additive");
            switch (choice)
            {
                case 0:
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    break;
                case 2:
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    break;
                default:
                    return false;
            }
            RetryAt.Clear();
            return IsSceneOpen(item);
        }

        /// <summary>Opens the item's scene (no dialog other than saving modified scenes). For the context menu.</summary>
        public static void OpenScene(DashboardItem item, bool additive)
        {
            var scenePath = ScenePath(item);
            if (scenePath == null) return;
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            RetryAt.Clear();
        }
    }
}
