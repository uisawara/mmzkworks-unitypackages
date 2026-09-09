using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public static class HierarchyDrawUtils
    {
        public static void DrawRightAlignedSmallText(Rect selectionRect, float rightEdgeX, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var content = new GUIContent(text);
            float w = style.CalcSize(content).x;
            var r = new Rect(
                rightEdgeX - w,
                selectionRect.y,
                w,
                selectionRect.height
            );
            GUI.Label(r, content, style);
        }

        public static bool IsSelectedInstanceId(int instanceID)
        {
            var ids = Selection.instanceIDs;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == instanceID)
                    return true;
            }
            return false;
        }

        // Exclude from icon display "noise" components (always present, low info)
        public static readonly HashSet<Type> ExcludedComponentTypes = new HashSet<Type>
        {
            typeof(Transform),
            typeof(RectTransform),
            typeof(CanvasRenderer),
        };

        public static string BuildComponentNameText(GameObject go, int maxNames)
        {
            if (go == null)
                return string.Empty;

            var components = go.GetComponents<Component>();
            var names = new List<string>(Mathf.Max(4, maxNames));
            var seen = new HashSet<string>();
            bool truncated = false;

            foreach (var c in components)
            {
                if (c == null)
                    continue;

                var t = c.GetType();
                if (ExcludedComponentTypes.Contains(t))
                    continue;

                if (seen.Add(t.Name))
                    names.Add(t.Name);

                if (names.Count >= maxNames)
                {
                    truncated = true;
                    break;
                }
            }

            if (names.Count == 0)
                return string.Empty;

            var text = string.Join(", ", names);
            return truncated ? text + ", ..." : text;
        }

        public static string GetPrefabSourcePath(GameObject go)
        {
            if (go == null)
                return string.Empty;
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return string.Empty;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(path))
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabAsset == null)
                    return string.Empty;
                path = AssetDatabase.GetAssetPath(prefabAsset);
            }
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                return path;
            return string.Empty;
        }
    }
}
