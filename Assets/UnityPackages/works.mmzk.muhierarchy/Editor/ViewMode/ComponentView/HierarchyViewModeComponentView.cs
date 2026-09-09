using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeComponentViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeComponentViewHandler();

        private const int MaxComponentIcons = 8;
        private static readonly Dictionary<int, Texture2D> ComponentIconCache = new Dictionary<int, Texture2D>(1024);

        public static void ClearComponentIconCache()
        {
            ComponentIconCache.Clear();
        }

        public HierarchyViewMode ViewMode => HierarchyViewMode.ComponentView;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            bool showLayer = ctx.ShowLayerName;
            bool showTag = ctx.ShowTagName;
            bool showPrefabIcon = ctx.ShowPrefabIcon;
            bool showComponentIcons = ctx.ShowComponentIcons;

            if (!showLayer && !showTag && !showPrefabIcon && !showComponentIcons)
                return;

            float fixedRightPosition = ctx.FixedRightPosition;
            var smallLabelStyle = ctx.SmallLabelStyle;
            float textSpacing = ctx.TextSpacing;
            float iconSize = ctx.IconSize;
            float spacing = ctx.Spacing;
            float componentIconSpacing = ctx.ComponentIconSpacing;

            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go);
            bool isPrefabRoot = isPrefabInstance && PrefabUtility.IsAnyPrefabInstanceRoot(go);
            bool isPrefabChild = isPrefabInstance && !isPrefabRoot;
            bool hasOverrides = isPrefabRoot && PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);
            bool hasChildOverrides = HasChildPrefabOverrides(go);
            bool showWarning = hasOverrides || hasChildOverrides;
            bool hasMissingScripts = HasMissingScripts(go, true);

            if (showLayer || showTag)
            {
                float layerWidth = 0f;
                GUIContent layerLabel = null;
                if (showLayer)
                {
                    string layerName = LayerMask.LayerToName(go.layer);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = "Default";
                    layerLabel = new GUIContent($" {layerName} ");
                    layerWidth = smallLabelStyle.CalcSize(layerLabel).x;
                }

                float tagWidth = 0f;
                GUIContent tagLabel = null;
                if (showTag)
                {
                    string tagName = go.tag;
                    if (string.IsNullOrEmpty(tagName) || tagName == "Untagged")
                        tagName = "Untagged";
                    tagLabel = new GUIContent($" {tagName} ");
                    tagWidth = smallLabelStyle.CalcSize(tagLabel).x;
                }

                if (showTag)
                {
                    var tagRect = new Rect(
                        fixedRightPosition - tagWidth,
                        selectionRect.y,
                        tagWidth,
                        selectionRect.height
                    );
                    GUI.Label(tagRect, tagLabel, smallLabelStyle);
                }
                if (showLayer)
                {
                    float layerX = showTag
                        ? fixedRightPosition - tagWidth - textSpacing - layerWidth
                        : fixedRightPosition - layerWidth;
                    var layerRect = new Rect(
                        layerX,
                        selectionRect.y,
                        layerWidth,
                        selectionRect.height
                    );
                    GUI.Label(layerRect, layerLabel, smallLabelStyle);
                }
            }

            float iconRightX = selectionRect.xMax - iconSize;

            bool shouldShowPrefabAreaIcon = showPrefabIcon &&
                (hasMissingScripts || ((isPrefabInstance || hasChildOverrides) && showWarning));
            if (shouldShowPrefabAreaIcon)
            {
                var iconRect = new Rect(
                    selectionRect.xMax - iconSize,
                    selectionRect.y + (selectionRect.height - iconSize) * 0.5f,
                    iconSize,
                    iconSize
                );
                if (hasMissingScripts)
                {
                    DrawStatusIconAtRect(iconRect, ctx.IconError, "Missing script");
                    return;
                }
                string prefabTooltip;
                if (isPrefabChild)
                    prefabTooltip = hasChildOverrides ? "Prefab child (has overrides in children)" : "Prefab child";
                else
                    prefabTooltip = showWarning ? "Prefab (has overrides)" : "Prefab (no overrides)";
                DrawPrefabIconAtRect(iconRect, ctx.IconPrefab, ctx.IconPrefabApplyWarning, ctx.IconPrefabEmpty, true, false, prefabTooltip);
            }

            if (showComponentIcons)
            {
                float x = iconRightX - (iconSize + spacing);
                foreach (var iconInfo in CollectComponentIcons(go, ctx.DedupeComponentIcons))
                {
                    if (iconInfo.Icon == null)
                        continue;
                    var iconRect = new Rect(
                        x,
                        selectionRect.y + (selectionRect.height - iconSize) * 0.5f,
                        iconSize,
                        iconSize
                    );
                    DrawIcon(iconRect, iconInfo.Icon, iconInfo.Enabled, iconInfo.Tooltip);
                    x -= (iconSize + componentIconSpacing);
                    if (x < selectionRect.xMin + 16f)
                        break;
                }
            }
        }

        private readonly struct ComponentIconInfo
        {
            public readonly Texture2D Icon;
            public readonly bool Enabled;
            public readonly string Tooltip;

            public ComponentIconInfo(Texture2D icon, bool enabled, string tooltip)
            {
                Icon = icon;
                Enabled = enabled;
                Tooltip = tooltip;
            }
        }

        private static IEnumerable<ComponentIconInfo> CollectComponentIcons(GameObject go, bool dedupe)
        {
            if (go == null)
                yield break;
            var components = go.GetComponents<Component>();
            if (!dedupe)
            {
                int count = 0;
                var scriptLike = new List<ComponentIconInfo>(4);
                foreach (var c in components)
                {
                    if (c == null)
                        continue;
                    var t = c.GetType();
                    if (HierarchyDrawUtils.ExcludedComponentTypes.Contains(t))
                        continue;
                    if (TryGetComponentIconAndEnabled(c, out var icon, out var enabled))
                    {
                        var info = new ComponentIconInfo(icon, enabled, t.Name);
                        if (c is MonoBehaviour)
                            scriptLike.Add(info);
                        else
                            yield return info;
                        count++;
                        if (count >= MaxComponentIcons)
                            yield break;
                    }
                }
                for (int i = 0; i < scriptLike.Count; i++)
                    yield return scriptLike[i];
                yield break;
            }
            var order = new List<Texture2D>(MaxComponentIcons);
            var scriptOrder = new List<Texture2D>(4);
            var namesByIcon = new Dictionary<Texture2D, List<string>>(MaxComponentIcons);
            var nameSetByIcon = new Dictionary<Texture2D, HashSet<string>>(MaxComponentIcons);
            var enabledByIcon = new Dictionary<Texture2D, bool>(MaxComponentIcons);
            foreach (var c in components)
            {
                if (c == null)
                    continue;
                var t = c.GetType();
                if (HierarchyDrawUtils.ExcludedComponentTypes.Contains(t))
                    continue;
                if (!TryGetComponentIconAndEnabled(c, out var icon, out var enabled))
                    continue;
                if (icon == null)
                    continue;
                if (!namesByIcon.TryGetValue(icon, out var nameList))
                {
                    if (c is MonoBehaviour)
                        scriptOrder.Add(icon);
                    else
                        order.Add(icon);
                    nameList = new List<string>(4);
                    namesByIcon[icon] = nameList;
                    nameSetByIcon[icon] = new HashSet<string>();
                    enabledByIcon[icon] = enabled;
                    if (order.Count + scriptOrder.Count >= MaxComponentIcons)
                        continue;
                }
                else
                    enabledByIcon[icon] = enabledByIcon[icon] && enabled;
                if (nameSetByIcon[icon].Add(t.Name))
                    nameList.Add(t.Name);
            }
            int emitted = 0;
            foreach (var icon in order)
            {
                if (emitted >= MaxComponentIcons)
                    yield break;
                if (!namesByIcon.TryGetValue(icon, out var nameList) || nameList.Count == 0)
                    continue;
                yield return new ComponentIconInfo(icon, enabledByIcon.TryGetValue(icon, out var en) ? en : true, string.Join(", ", nameList));
                emitted++;
            }
            foreach (var icon in scriptOrder)
            {
                if (emitted >= MaxComponentIcons)
                    yield break;
                if (!namesByIcon.TryGetValue(icon, out var nameList) || nameList.Count == 0)
                    continue;
                yield return new ComponentIconInfo(icon, enabledByIcon.TryGetValue(icon, out var en) ? en : true, string.Join(", ", nameList));
                emitted++;
            }
        }

        private static bool TryGetComponentIconAndEnabled(Component c, out Texture2D icon, out bool enabled)
        {
            icon = null;
            enabled = true;
            if (c == null)
                return false;
            int id = c.GetInstanceID();
            enabled = IsComponentEnabled(c);
            if (ComponentIconCache.TryGetValue(id, out var cachedIcon))
            {
                icon = cachedIcon;
                return icon != null;
            }
            icon = GetInspectorLikeIcon(c);
            ComponentIconCache[id] = icon;
            return icon != null;
        }

        private static bool IsComponentEnabled(Component c)
        {
            if (c is Behaviour b)
                return b.enabled;
            if (c is Renderer r)
                return r.enabled;
            if (c is Collider col)
                return col.enabled;
            return true;
        }

        private static Texture2D GetInspectorLikeIcon(Component c)
        {
            var icon = EditorGUIUtility.GetIconForObject(c) as Texture2D;
            if (icon == null)
                icon = EditorGUIUtility.ObjectContent(c, c.GetType()).image as Texture2D;
            if (icon == null)
                icon = EditorGUIUtility.ObjectContent(null, c.GetType()).image as Texture2D;
            return icon;
        }

        private static void DrawIcon(Rect iconRect, Texture2D icon, bool enabled, string tooltip)
        {
            if (icon == null)
                return;
            var originalColor = GUI.color;
            if (!enabled)
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.35f);
            GUI.Label(iconRect, new GUIContent(icon, tooltip));
            GUI.color = originalColor;
        }

        private static void DrawStatusIconAtRect(Rect iconRect, Texture2D icon, string tooltip)
        {
            if (icon == null)
                return;
            GUI.Label(iconRect, new GUIContent(icon, tooltip));
        }

        private static void DrawPrefabIconAtRect(Rect iconRect, Texture2D iconPrefab, Texture2D iconPrefabApplyWarning, Texture2D iconPrefabEmpty, bool hasOverrides, bool isEmpty, string tooltip)
        {
            if (isEmpty)
            {
                if (iconPrefabEmpty != null)
                {
                    var originalColor = GUI.color;
                    GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.4f);
                    GUI.Label(iconRect, new GUIContent(iconPrefabEmpty, tooltip));
                    GUI.color = originalColor;
                }
                else
                {
                    var c = GUI.color;
                    GUI.color = new Color(Color.gray.r, Color.gray.g, Color.gray.b, 0.4f);
                    EditorGUI.LabelField(iconRect, "○");
                    GUI.color = c;
                }
            }
            else if (hasOverrides)
            {
                if (iconPrefabApplyWarning != null)
                    GUI.Label(iconRect, new GUIContent(iconPrefabApplyWarning, tooltip));
                else
                {
                    var c = GUI.color;
                    GUI.color = Color.yellow;
                    EditorGUI.LabelField(iconRect, "★");
                    GUI.color = c;
                }
            }
            else
            {
                if (iconPrefab != null)
                    GUI.Label(iconRect, new GUIContent(iconPrefab, tooltip));
                else
                {
                    var c = GUI.color;
                    GUI.color = Color.yellow;
                    EditorGUI.LabelField(iconRect, "★");
                    GUI.color = c;
                }
            }
        }

        private static bool HasMissingScripts(GameObject go, bool includeChildren)
        {
            if (go == null)
                return false;
            var mbs = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] == null)
                    return true;
            }
            if (!includeChildren)
                return false;
            foreach (Transform child in go.transform)
            {
                if (child == null)
                    continue;
                if (HasMissingScripts(child.gameObject, true))
                    return true;
            }
            return false;
        }

        private static bool HasChildPrefabOverrides(GameObject go)
        {
            if (go == null)
                return false;
            foreach (Transform child in go.transform)
            {
                var childGo = child.gameObject;
                if (!childGo.scene.IsValid() || !childGo.scene.isLoaded)
                    continue;
                if (PrefabUtility.IsPartOfPrefabInstance(childGo) && PrefabUtility.HasPrefabInstanceAnyOverrides(childGo, false))
                    return true;
                if (HasChildPrefabOverrides(childGo))
                    return true;
            }
            return false;
        }
    }
}
