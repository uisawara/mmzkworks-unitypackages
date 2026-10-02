using System;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Assigns SceneRules to folders: scenes under a folder get its rules. Prefab Mode uses the prefab's path.
    /// When several entries cover a scene, all their rules apply. Several SceneRulesAssignments assets are
    /// combined. Assets/Settings is the recommended place for it.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneRulesAssignments", menuName = "muValidation/Scene Rules Assignments")]
    public class SceneRulesAssignments : ScriptableObject
    {
        [Serializable]
        public class Assignment
        {
            [Tooltip("Folder from the project root, e.g. Assets/Scenes/Stages, or a scene path. " +
                     "Assets (or empty) covers all scenes, including unsaved ones.")]
            public string path = "Assets";

            [Tooltip("Rules applied to scenes under the path.")]
            public SceneRules[] rules = Array.Empty<SceneRules>();
        }

        public Assignment[] assignments = Array.Empty<Assignment>();

        /// <summary>
        /// Normalizes an assignment path: forward slashes, no trailing slash, "Assets" if empty.
        /// </summary>
        public static string NormalizePath(string path)
        {
            path = (path ?? "").Trim().Replace('\\', '/').TrimEnd('/');
            return path.Length == 0 ? "Assets" : path;
        }

        /// <summary>
        /// True if the assignment path covers the scene (or prefab) path. "Assets" also covers unsaved scenes (empty path).
        /// </summary>
        public static bool Covers(string assignmentPath, string scenePath)
        {
            if (assignmentPath == "Assets") return true;
            if (string.IsNullOrEmpty(scenePath)) return false;
            return scenePath == assignmentPath || scenePath.StartsWith(assignmentPath + "/", StringComparison.Ordinal);
        }

        private void OnValidate()
        {
            SceneRulesRegistry.Invalidate();
        }
    }
}
