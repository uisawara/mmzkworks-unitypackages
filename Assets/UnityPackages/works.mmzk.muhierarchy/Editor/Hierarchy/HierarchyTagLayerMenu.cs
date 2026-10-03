using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Clicking the Tag / Layer label in the Hierarchy opens a menu to change it in place.
    /// When the clicked object is part of the selection, the change applies to all selected objects.
    /// Tags / layers that muValidation SceneRules forbid for any of the targets are shown disabled.
    /// </summary>
    public static class HierarchyTagLayerMenu
    {
        private const string TagsAndLayersSettingsPath = "Project/Tags and Layers";

        public static void HandleTagClick(Rect rect, GameObject go)
        {
            if (!IsClicked(rect))
                return;

            var targets = GetTargets(go);
            var menu = new GenericMenu();
            foreach (var tag in InternalEditorUtility.tags)
            {
                string t = tag;
                string restriction = GetTagRestriction(targets, t);
                if (restriction != null)
                    menu.AddDisabledItem(new GUIContent(FormatDisabledLabel(t, restriction)), go.tag == t);
                else
                    menu.AddItem(new GUIContent(t), go.tag == t, () => SetTag(targets, t));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Add Tag..."), false,
                () => SettingsService.OpenProjectSettings(TagsAndLayersSettingsPath));
            menu.DropDown(rect);
        }

        public static void HandleLayerClick(Rect rect, GameObject go)
        {
            if (!IsClicked(rect))
                return;

            var targets = GetTargets(go);
            var menu = new GenericMenu();
            if (string.IsNullOrEmpty(LayerMask.LayerToName(go.layer)))
            {
                // Current layer has no name, so it would be missing from the list below
                menu.AddDisabledItem(new GUIContent($"{go.layer}: (unnamed)"), true);
                menu.AddSeparator(string.Empty);
            }
            for (int layer = 0; layer < 32; layer++)
            {
                string layerName = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(layerName))
                    continue;
                int l = layer;
                string label = $"{layer}: {layerName}";
                string restriction = GetLayerRestriction(targets, l);
                if (restriction != null)
                    menu.AddDisabledItem(new GUIContent(FormatDisabledLabel(label, restriction)), go.layer == l);
                else
                    menu.AddItem(new GUIContent(label), go.layer == l, () => SetLayer(targets, l));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Add Layer..."), false,
                () => SettingsService.OpenProjectSettings(TagsAndLayersSettingsPath));
            menu.DropDown(rect);
        }

        private static string GetTagRestriction(GameObject[] targets, string tag)
        {
            foreach (var go in targets)
            {
                string restriction = MuValidationBridge.GetTagRestriction(go, tag);
                if (restriction != null)
                    return restriction;
            }
            return null;
        }

        private static string GetLayerRestriction(GameObject[] targets, int layer)
        {
            foreach (var go in targets)
            {
                string restriction = MuValidationBridge.GetLayerRestriction(go, layer);
                if (restriction != null)
                    return restriction;
            }
            return null;
        }

        // GenericMenu shows no tooltips, so the reason goes into the label. Slashes would make submenus.
        private static string FormatDisabledLabel(string label, string restriction)
        {
            return $"{label}    ({restriction})".Replace('/', '\u2215');
        }

        private static bool IsClicked(Rect rect)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || e.alt)
                return false;
            if (!rect.Contains(e.mousePosition))
                return false;
            // Keep the Hierarchy from treating this as a row click (selection / drag)
            e.Use();
            return true;
        }

        private static GameObject[] GetTargets(GameObject go)
        {
            if (HierarchyDrawUtils.IsSelectedInstanceId(go.GetInstanceID()))
            {
                var selected = Selection.gameObjects;
                if (selected.Length > 0)
                    return selected;
            }
            return new[] { go };
        }

        private static void SetTag(GameObject[] targets, string tag)
        {
            Undo.RecordObjects(targets, "Change Tag");
            foreach (var go in targets)
            {
                if (go != null)
                    go.tag = tag;
            }
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void SetLayer(GameObject[] targets, int layer)
        {
            bool includeChildren = false;
            if (HasChildren(targets))
            {
                // Same choice as the Inspector's Layer field
                int choice = EditorUtility.DisplayDialogComplex(
                    "Change Layer",
                    $"Do you want to set layer to {LayerMask.LayerToName(layer)} for all child objects as well?",
                    "Yes, change children", "Cancel", "No, this object only");
                if (choice == 1)
                    return;
                includeChildren = choice == 0;
            }

            var objects = new List<GameObject>();
            foreach (var go in targets)
            {
                if (go == null)
                    continue;
                if (includeChildren)
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                        objects.Add(t.gameObject);
                }
                else
                {
                    objects.Add(go);
                }
            }

            Undo.RecordObjects(objects.ToArray(), "Change Layer");
            foreach (var go in objects)
                go.layer = layer;
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
