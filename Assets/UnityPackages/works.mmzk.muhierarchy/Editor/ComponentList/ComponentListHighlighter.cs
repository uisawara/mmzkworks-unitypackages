using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    // Tints Hierarchy rows of GameObjects picked in the Component List window.
    // Ancestors get a fainter tint so matches stay findable while collapsed.
    [InitializeOnLoad]
    internal static class ComponentListHighlighter
    {
        private const float RowAlpha = 0.35f;
        private const float AncestorAlpha = 0.12f;

        private static readonly Dictionary<int, Color> RowColors = new Dictionary<int, Color>();
        private static readonly Dictionary<int, Color> AncestorColors = new Dictionary<int, Color>();

        static ComponentListHighlighter()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        public static void Set(IEnumerable<(GameObject go, Color color)> items)
        {
            RowColors.Clear();
            AncestorColors.Clear();
            foreach (var (go, color) in items)
            {
                if (go == null)
                    continue;
                var id = go.GetInstanceID();
                if (!RowColors.ContainsKey(id))
                    RowColors[id] = color;
                for (var p = go.transform.parent; p != null; p = p.parent)
                {
                    var pid = p.gameObject.GetInstanceID();
                    if (AncestorColors.ContainsKey(pid))
                        break;
                    AncestorColors[pid] = color;
                }
            }
            EditorApplication.RepaintHierarchyWindow();
        }

        public static void Clear()
        {
            if (RowColors.Count == 0 && AncestorColors.Count == 0)
                return;
            RowColors.Clear();
            AncestorColors.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (RowColors.Count == 0)
                return;
            var e = Event.current;
            if (e == null || e.type != EventType.Repaint)
                return;

            float alpha;
            if (RowColors.TryGetValue(instanceID, out var color))
                alpha = RowAlpha;
            else if (AncestorColors.TryGetValue(instanceID, out color))
                alpha = AncestorAlpha;
            else
                return;

            color.a = alpha;
            // Span the whole row like Unity's own selection highlight.
            var rect = new Rect(0f, selectionRect.y, selectionRect.xMax + 16f, selectionRect.height);
            EditorGUI.DrawRect(rect, color);
        }
    }
}
