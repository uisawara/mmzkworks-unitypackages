using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Draws the Static state of a GameObject as a small "S" badge and toggles it on click.
    /// Like the Inspector's Static checkbox, clicking sets every static flag (or clears them when all are set).
    /// When the clicked object is part of the selection, the change applies to all selected objects.
    /// </summary>
    public static class HierarchyStaticToggle
    {
        private static readonly GUIContent BadgeLabel = new GUIContent("S");
        private static readonly Color StaticBackground = new Color(0.25f, 0.55f, 0.85f, 1f);
        private static readonly Color MixedBackground = new Color(0.25f, 0.55f, 0.85f, 0.45f);
        private static readonly Color NonStaticBackground = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        private static readonly Color NonStaticText = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        private static StaticEditorFlags? _allFlags;

        // Union of the defined flags; the Inspector's "Everything" sets all of them
        private static StaticEditorFlags AllFlags
        {
            get
            {
                if (_allFlags == null)
                {
                    int all = 0;
                    foreach (var v in Enum.GetValues(typeof(StaticEditorFlags)))
                        all |= (int)v;
                    _allFlags = (StaticEditorFlags)all;
                }
                return _allFlags.Value;
            }
        }

        public static float GetWidth(GUIStyle style)
        {
            return Mathf.Max(style.CalcSize(BadgeLabel).x + 4f, 14f);
        }

        public static void Draw(Rect rect, GameObject go, GUIStyle style)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            var all = AllFlags;
            bool isAll = (flags & all) == all;
            bool isNone = flags == 0;

            var content = new GUIContent(BadgeLabel.text, BuildTooltip(flags, isAll, isNone));
            if (isNone)
                HierarchyDrawUtils.DrawPillLabel(rect, content, style, NonStaticBackground, NonStaticText);
            else
                HierarchyDrawUtils.DrawPillLabel(rect, content, style, isAll ? StaticBackground : MixedBackground, Color.white);

            if (HierarchyTagLayerMenu.IsClicked(rect))
                SetStatic(HierarchyTagLayerMenu.GetTargets(go), !isAll);
        }

        private static string BuildTooltip(StaticEditorFlags flags, bool isAll, bool isNone)
        {
            const string clickHint = "\n(Click to toggle)";
            if (isNone)
                return "Not Static" + clickHint;
            if (isAll)
                return "Static" + clickHint;
            return "Static (mixed): " + flags + clickHint;
        }

        private static void SetStatic(GameObject[] targets, bool enable)
        {
            bool includeChildren = false;
            if (HasChildren(targets))
            {
                // Same choice as the Inspector's Static checkbox
                int choice = EditorUtility.DisplayDialogComplex(
                    "Change Static Flags",
                    $"Do you want to {(enable ? "enable" : "disable")} the static flags for all the child objects as well?",
                    "Yes, change children", "Cancel", "No, this object only");
                if (choice == 1)
                    return;
                includeChildren = choice == 0;
            }

            // A parent and its child may both be selected; change each object once
            var objects = new List<GameObject>();
            var seen = new HashSet<GameObject>();
            foreach (var go in targets)
            {
                if (go == null)
                    continue;
                if (includeChildren)
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (seen.Add(t.gameObject))
                            objects.Add(t.gameObject);
                    }
                }
                else if (seen.Add(go))
                {
                    objects.Add(go);
                }
            }
            if (objects.Count == 0)
                return;

            var flags = enable ? AllFlags : (StaticEditorFlags)0;
            Undo.RecordObjects(objects.ToArray(), "Change Static Flags");
            foreach (var go in objects)
                GameObjectUtility.SetStaticEditorFlags(go, flags);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static bool HasChildren(GameObject[] targets)
        {
            foreach (var go in targets)
            {
                if (go != null && go.transform.childCount > 0)
                    return true;
            }
            return false;
        }
    }
}
