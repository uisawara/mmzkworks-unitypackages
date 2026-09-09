using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public struct ReferenceInfo
    {
        public bool HasParentHierarchyReference;
        public bool HasChildHierarchyReference;
        public bool HasExternalHierarchyReference;
        public bool HasAssetReference;
        public HashSet<string> ParentHierarchyReferenceNames;
        public HashSet<string> ChildHierarchyReferenceNames;
        public HashSet<string> ExternalHierarchyReferenceNames;
        public HashSet<string> AssetReferenceNames;
        public HashSet<int> ParentHierarchyReferenceIds;
        public HashSet<int> ChildHierarchyReferenceIds;
        public HashSet<int> ExternalHierarchyReferenceIds;
        public HashSet<int> AssetReferenceIds;
        public bool HasAnyReference => HasParentHierarchyReference || HasChildHierarchyReference ||
            HasExternalHierarchyReference || HasAssetReference;

        public ReferenceInfo(bool dummy)
        {
            HasParentHierarchyReference = false;
            HasChildHierarchyReference = false;
            HasExternalHierarchyReference = false;
            HasAssetReference = false;
            ParentHierarchyReferenceNames = new HashSet<string>();
            ChildHierarchyReferenceNames = new HashSet<string>();
            ExternalHierarchyReferenceNames = new HashSet<string>();
            AssetReferenceNames = new HashSet<string>();
            ParentHierarchyReferenceIds = new HashSet<int>();
            ChildHierarchyReferenceIds = new HashSet<int>();
            ExternalHierarchyReferenceIds = new HashSet<int>();
            AssetReferenceIds = new HashSet<int>();
        }
    }

    public sealed class HierarchyViewModeReferenceViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeReferenceViewHandler();

        private static readonly Dictionary<int, ReferenceInfo> ReferenceInfoCache = new Dictionary<int, ReferenceInfo>(1024);
        private static readonly Dictionary<int, Rect> GameObjectPositions = new Dictionary<int, Rect>(1024);
        private static readonly Dictionary<int, Color> ReferencedFromItems = new Dictionary<int, Color>(1024);
        private static readonly Dictionary<int, Color> ReferencedToItems = new Dictionary<int, Color>(1024);
        private static List<int> _highlightedReferenceIds;
        private static int[] _originalSelectionBeforeHighlight;
        private static int _lastFrameCount = -1;
        private static int _lastHighlightFrame = -1;

        private static Texture2D _iconReferenceParent;
        private static Texture2D _iconReferenceChild;
        private static Texture2D _iconReferenceExternal;
        private static Texture2D _iconReferenceAsset;
        private static Texture2D _iconSelectPrefabAsset;
        private static bool _iconsInitialized;
        private static readonly object _iconsInitLock = new object();

        public static void ClearReferenceInfoCache() => ReferenceInfoCache.Clear();
        public static void ClearGameObjectPositions()
        {
            GameObjectPositions.Clear();
            ReferencedFromItems.Clear();
            ReferencedToItems.Clear();
        }
        public static void ClearHighlight()
        {
            if (_highlightedReferenceIds != null && _highlightedReferenceIds.Count > 0)
            {
                if (_originalSelectionBeforeHighlight != null)
                    Selection.instanceIDs = _originalSelectionBeforeHighlight;
                _highlightedReferenceIds = null;
                _originalSelectionBeforeHighlight = null;
                _lastHighlightFrame = -1;
            }
        }

        public static bool IsHighlighted(int instanceID) =>
            _highlightedReferenceIds != null && _highlightedReferenceIds.Contains(instanceID);

        public static void ProcessInput(int instanceID, Rect selectionRect, int currentFrame)
        {
            if (currentFrame != _lastFrameCount)
            {
                GameObjectPositions.Clear();
                ReferencedFromItems.Clear();
                ReferencedToItems.Clear();
                _lastFrameCount = currentFrame;
            }
            GameObjectPositions[instanceID] = selectionRect;

            if (_highlightedReferenceIds != null && _highlightedReferenceIds.Count > 0)
            {
                if (currentFrame != _lastHighlightFrame)
                {
                    _lastHighlightFrame = currentFrame;
                    var highlightedObjects = new List<GameObject>(_highlightedReferenceIds.Count);
                    foreach (int refId in _highlightedReferenceIds)
                    {
                        var refGo = EditorUtility.InstanceIDToObject(refId) as GameObject;
                        if (refGo != null)
                            highlightedObjects.Add(refGo);
                    }
                    if (highlightedObjects.Count > 0)
                        Selection.objects = highlightedObjects.ToArray();
                }
            }

            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 &&
                _highlightedReferenceIds != null && _highlightedReferenceIds.Count > 0 &&
                !_highlightedReferenceIds.Contains(instanceID))
            {
                ClearHighlight();
            }
        }

        public HierarchyViewMode ViewMode => HierarchyViewMode.ReferenceView;
        public bool RecordPosition => true;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            EnsureIconsInitialized();
            bool isPrefabRoot = PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.IsAnyPrefabInstanceRoot(go);
            var refInfo = GetReferenceInfo(go);
            float iconSize = ctx.IconSize;
            float refIconSpacing = 2f;
            float refIconY = selectionRect.y + (selectionRect.height - iconSize) * 0.5f;
            float refIconRightX = selectionRect.xMax - iconSize;
            float refPrefabSelectX = refIconRightX;
            float refAssetX = refPrefabSelectX - (iconSize + refIconSpacing);
            float refChildX = refAssetX - (iconSize + refIconSpacing);
            float refParentX = refChildX - (iconSize + refIconSpacing);
            float refExternalX = refParentX - (iconSize + refIconSpacing);

            if (isPrefabRoot && _iconSelectPrefabAsset != null)
            {
                var prefabSelectIconRect = new Rect(refPrefabSelectX, refIconY, iconSize, iconSize);
                DrawPrefabSelectIcon(prefabSelectIconRect, go);
            }
            if (refInfo.HasAssetReference && _iconReferenceAsset != null)
            {
                var iconRect = new Rect(refAssetX, refIconY, iconSize, iconSize);
                string tooltip = refInfo.AssetReferenceNames != null && refInfo.AssetReferenceNames.Count > 0
                    ? string.Join(", ", refInfo.AssetReferenceNames) : "Has Asset reference";
                DrawAssetReferenceIcon(iconRect, _iconReferenceAsset, tooltip, refInfo.AssetReferenceIds);
            }
            if (refInfo.HasChildHierarchyReference && _iconReferenceChild != null)
            {
                var iconRect = new Rect(refChildX, refIconY, iconSize, iconSize);
                string tooltip = refInfo.ChildHierarchyReferenceNames != null && refInfo.ChildHierarchyReferenceNames.Count > 0
                    ? string.Join(", ", refInfo.ChildHierarchyReferenceNames) : "Has child hierarchy reference";
                DrawReferenceIconWithHighlight(iconRect, _iconReferenceChild, tooltip, refInfo.ChildHierarchyReferenceIds);
            }
            if (refInfo.HasParentHierarchyReference && _iconReferenceParent != null)
            {
                var iconRect = new Rect(refParentX, refIconY, iconSize, iconSize);
                string tooltip = refInfo.ParentHierarchyReferenceNames != null && refInfo.ParentHierarchyReferenceNames.Count > 0
                    ? string.Join(", ", refInfo.ParentHierarchyReferenceNames) : "Has parent hierarchy reference";
                DrawReferenceIconWithHighlight(iconRect, _iconReferenceParent, tooltip, refInfo.ParentHierarchyReferenceIds);
            }
            if (refInfo.HasExternalHierarchyReference && _iconReferenceExternal != null)
            {
                var iconRect = new Rect(refExternalX, refIconY, iconSize, iconSize);
                string tooltip = refInfo.ExternalHierarchyReferenceNames != null && refInfo.ExternalHierarchyReferenceNames.Count > 0
                    ? string.Join(", ", refInfo.ExternalHierarchyReferenceNames) : "Has external hierarchy reference";
                DrawReferenceIconWithHighlight(iconRect, _iconReferenceExternal, tooltip, refInfo.ExternalHierarchyReferenceIds);
            }

            if (ctx.ShowReferenceLines)
                DrawReferenceArrows(selectionRect, instanceID, refInfo);

            if (Event.current.type == EventType.Repaint)
            {
                if (ReferencedFromItems.TryGetValue(instanceID, out Color fromColor))
                    DrawRoundedRectBackground(selectionRect, fromColor, true);
                if (ReferencedToItems.TryGetValue(instanceID, out Color toColor))
                    DrawRoundedRectBackground(selectionRect, toColor, false);
            }

            if (_highlightedReferenceIds != null && _highlightedReferenceIds.Count > 0 && Event.current.type == EventType.Repaint &&
                _highlightedReferenceIds.Contains(instanceID))
            {
                Color highlightColor = new Color(0.24f, 0.48f, 0.90f, 0.3f);
                EditorGUI.DrawRect(selectionRect, highlightColor);
            }
        }

        private static void EnsureIconsInitialized()
        {
            if (_iconsInitialized)
                return;
            lock (_iconsInitLock)
            {
                if (_iconsInitialized)
                    return;
                try
                {
                    _iconReferenceParent = EditorGUIUtility.IconContent("d_Animation.FirstKey").image as Texture2D ?? EditorGUIUtility.IconContent("d_Linked").image as Texture2D;
                    _iconReferenceChild = EditorGUIUtility.IconContent("d_Animation.LastKey").image as Texture2D ?? EditorGUIUtility.IconContent("d_Unlinked").image as Texture2D;
                    _iconReferenceExternal = EditorGUIUtility.IconContent("d_Linked").image as Texture2D;
                    _iconReferenceAsset = EditorGUIUtility.IconContent("d_FolderOpened Icon").image as Texture2D ?? EditorGUIUtility.IconContent("d_Folder Icon").image as Texture2D;
                    _iconSelectPrefabAsset = EditorGUIUtility.IconContent("d_Search Icon").image as Texture2D ?? EditorGUIUtility.IconContent("d_ViewToolZoom").image as Texture2D;
                    _iconsInitialized = true;
                }
                catch { }
            }
        }

        private static ReferenceInfo GetReferenceInfo(GameObject go)
        {
            if (go == null)
                return default;
            int id = go.GetInstanceID();
            if (ReferenceInfoCache.TryGetValue(id, out var cached))
                return cached;
            var info = CollectReferences(go);
            ReferenceInfoCache[id] = info;
            return info;
        }

        private static ReferenceInfo CollectReferences(GameObject go)
        {
            var info = new ReferenceInfo(true);
            if (go == null)
                return info;
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null)
                    continue;
                var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (it.propertyPath == "m_Script")
                        continue;
                    if (it.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    var refObj = it.objectReferenceValue;
                    if (refObj == null)
                        continue;
                    if (IsAssetReference(refObj))
                    {
                        info.HasAssetReference = true;
                        if (!string.IsNullOrEmpty(refObj.name))
                            info.AssetReferenceNames.Add(refObj.name);
                        info.AssetReferenceIds.Add(refObj.GetInstanceID());
                        continue;
                    }
                    var refGo = GetGameObjectFromReference(refObj);
                    if (refGo == null || refGo == go)
                        continue;
                    if (!refGo.scene.IsValid() || refGo.scene != go.scene)
                        continue;
                    string refName = refGo.name;
                    int refId = refGo.GetInstanceID();
                    if (IsParentHierarchy(go, refGo))
                    {
                        info.HasParentHierarchyReference = true;
                        if (!string.IsNullOrEmpty(refName))
                            info.ParentHierarchyReferenceNames.Add(refName);
                        info.ParentHierarchyReferenceIds.Add(refId);
                    }
                    else if (IsChildHierarchy(go, refGo))
                    {
                        info.HasChildHierarchyReference = true;
                        if (!string.IsNullOrEmpty(refName))
                            info.ChildHierarchyReferenceNames.Add(refName);
                        info.ChildHierarchyReferenceIds.Add(refId);
                    }
                    else
                    {
                        info.HasExternalHierarchyReference = true;
                        if (!string.IsNullOrEmpty(refName))
                            info.ExternalHierarchyReferenceNames.Add(refName);
                        info.ExternalHierarchyReferenceIds.Add(refId);
                    }
                }
            }
            return info;
        }

        private static GameObject GetGameObjectFromReference(UnityEngine.Object obj)
        {
            if (obj == null) return null;
            if (obj is GameObject go) return go;
            if (obj is Component c) return c.gameObject;
            return null;
        }

        private static bool IsParentHierarchy(GameObject current, GameObject target)
        {
            if (current == null || target == null) return false;
            var p = current.transform.parent;
            while (p != null)
            {
                if (p == target.transform) return true;
                p = p.parent;
            }
            return false;
        }

        private static bool IsChildHierarchy(GameObject current, GameObject target)
        {
            if (current == null || target == null) return false;
            return target.transform.IsChildOf(current.transform);
        }

        private static bool IsAssetReference(UnityEngine.Object obj) => obj != null && AssetDatabase.Contains(obj);

        private static void DrawPrefabSelectIcon(Rect iconRect, GameObject go)
        {
            if (_iconSelectPrefabAsset == null || go == null || !PrefabUtility.IsPartOfPrefabInstance(go))
                return;
            EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.Link);
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && iconRect.Contains(e.mousePosition))
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabAsset == null)
                {
                    var path = HierarchyDrawUtils.GetPrefabSourcePath(go);
                    if (!string.IsNullOrEmpty(path))
                        prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                if (prefabAsset != null)
                {
                    EditorUtility.FocusProjectWindow();
                    EditorApplication.delayCall += () => { Selection.activeObject = prefabAsset; EditorGUIUtility.PingObject(prefabAsset); };
                }
                e.Use();
                return;
            }
            GUI.Label(iconRect, new GUIContent(_iconSelectPrefabAsset, "Select Prefab Asset in Project View"));
        }

        private static void DrawAssetReferenceIcon(Rect iconRect, Texture2D icon, string tooltip, ICollection<int> assetIds)
        {
            if (icon == null) return;
            EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.Link);
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && iconRect.Contains(e.mousePosition))
            {
                if (assetIds != null && assetIds.Count > 0)
                {
                    if (assetIds.Count == 1)
                    {
                        foreach (int id in assetIds)
                        {
                            var asset = EditorUtility.InstanceIDToObject(id);
                            if (asset != null)
                            {
                                EditorUtility.FocusProjectWindow();
                                EditorApplication.delayCall += () => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); };
                            }
                            break;
                        }
                    }
                    else
                        AssetSelectionPopupWindow.Open(iconRect, assetIds);
                    e.Use();
                    return;
                }
            }
            GUI.Label(iconRect, new GUIContent(icon, tooltip));
        }

        private static void DrawReferenceIconWithHighlight(Rect iconRect, Texture2D icon, string tooltip, ICollection<int> referenceIds)
        {
            if (icon == null) return;
            GUI.Label(iconRect, new GUIContent(icon, tooltip));
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && iconRect.Contains(e.mousePosition))
            {
                if (referenceIds != null && referenceIds.Count > 0)
                {
                    if (_originalSelectionBeforeHighlight == null)
                        _originalSelectionBeforeHighlight = Selection.instanceIDs;
                    var list = new List<GameObject>();
                    foreach (int refId in referenceIds)
                    {
                        var refGo = EditorUtility.InstanceIDToObject(refId) as GameObject;
                        if (refGo != null) list.Add(refGo);
                    }
                    if (list.Count > 0)
                    {
                        _highlightedReferenceIds = new List<int>(referenceIds);
                        Selection.objects = list.ToArray();
                    }
                    e.Use();
                }
                else
                {
                    ClearHighlight();
                    e.Use();
                }
            }
        }

        private static void DrawReferenceArrows(Rect fromRect, int fromInstanceID, ReferenceInfo refInfo)
        {
            if (!refInfo.HasAnyReference) return;
            Color parentColor = new Color(0.2f, 0.6f, 1f, 0.8f);
            Color childColor = new Color(0.2f, 1f, 0.4f, 0.8f);
            Color externalColor = new Color(1f, 0.6f, 0.2f, 0.8f);
            const float baseOffsetX = 220f, offsetStep = 48f, endOffsetDelta = 20f;
            float ps = baseOffsetX, pe = baseOffsetX + endOffsetDelta;
            float cs = baseOffsetX + offsetStep, ce = baseOffsetX + offsetStep + endOffsetDelta;
            float es = baseOffsetX + offsetStep * 2f, ee = baseOffsetX + offsetStep * 2f + endOffsetDelta;

            if (refInfo.ParentHierarchyReferenceIds != null)
                foreach (int refId in refInfo.ParentHierarchyReferenceIds)
                    if (GameObjectPositions.TryGetValue(refId, out Rect toRect))
                    {
                        DrawReferenceArrow(fromRect, toRect, parentColor, ps, pe, false);
                        ReferencedFromItems[fromInstanceID] = parentColor;
                        ReferencedToItems[refId] = parentColor;
                    }
            if (refInfo.ChildHierarchyReferenceIds != null)
                foreach (int refId in refInfo.ChildHierarchyReferenceIds)
                    if (GameObjectPositions.TryGetValue(refId, out Rect toRect))
                    {
                        DrawReferenceArrow(fromRect, toRect, childColor, cs, ce, true);
                        ReferencedFromItems[fromInstanceID] = childColor;
                        ReferencedToItems[refId] = childColor;
                    }
            if (refInfo.ExternalHierarchyReferenceIds != null)
                foreach (int refId in refInfo.ExternalHierarchyReferenceIds)
                    if (GameObjectPositions.TryGetValue(refId, out Rect toRect))
                    {
                        DrawReferenceArrow(fromRect, toRect, externalColor, es, ee, false);
                        ReferencedFromItems[fromInstanceID] = externalColor;
                        ReferencedToItems[refId] = externalColor;
                    }
        }

        private static void DrawReferenceArrow(Rect fromRect, Rect toRect, Color color, float startOffsetX, float endOffsetX, bool isParentToChild)
        {
            if (fromRect == default || toRect == default) return;
            var startPos = new Vector2(fromRect.xMin + startOffsetX, fromRect.y + fromRect.height * 0.5f);
            var endPos = new Vector2(toRect.xMin + endOffsetX, toRect.y + toRect.height * 0.5f);
            if (startPos.y < 0 || endPos.y < 0 || startPos.y > Screen.height || endPos.y > Screen.height) return;
            Handles.color = color;
            Handles.BeginGUI();
            Vector2 cp1, cp2;
            if (isParentToChild)
            {
                float vd = Mathf.Abs(endPos.y - startPos.y), hd = Mathf.Abs(endPos.x - startPos.x);
                float co = Mathf.Max(vd * 0.3f, hd * 0.3f, 30f);
                cp1 = new Vector2(startPos.x, startPos.y + co);
                cp2 = new Vector2(endPos.x - co, endPos.y);
            }
            else
            {
                float vd = Mathf.Abs(endPos.y - startPos.y);
                float co = Mathf.Max(vd * 0.3f, 30f);
                cp1 = new Vector2(startPos.x + co, startPos.y);
                cp2 = new Vector2(endPos.x + co, endPos.y);
            }
            const int segments = 50;
            var curvePoints = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1f - t;
                float uuu = u * u * u, ttt = t * t * t;
                var p = uuu * startPos + 3f * u * u * t * cp1 + 3f * u * t * t * cp2 + ttt * endPos;
                curvePoints[i] = new Vector3(p.x, p.y, 0f);
            }
            Handles.DrawAAPolyLine(2f, curvePoints);
            Handles.DrawSolidDisc(startPos, Vector3.forward, 3f);
            Handles.color = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, color.a);
            Handles.DrawSolidDisc(endPos, Vector3.forward, 3f);
            Handles.color = color;
            var dir = (endPos - cp2).normalized;
            if (dir.magnitude > 0.01f)
            {
                float arrowSize = 8f;
                var arrowBase = endPos - dir * arrowSize;
                var perp = new Vector2(-dir.y, dir.x) * arrowSize * 0.6f;
                Handles.DrawAAConvexPolygon(new Vector3[] { endPos, arrowBase + perp, arrowBase - perp });
            }
            Handles.EndGUI();
        }

        private static void DrawRoundedRectBackground(Rect rect, Color color, bool isFrom)
        {
            const float cornerRadius = 4f, padding = 2f, leftPadding = 16f;
            Color bgColor = new Color(color.r, color.g, color.b, 0.075f);
            var drawRect = new Rect(rect.x + leftPadding, rect.y + padding, rect.width - leftPadding - 2f, rect.height - padding * 2f);
            var fillRect = new Rect(drawRect.xMin + cornerRadius, drawRect.yMin, drawRect.width - cornerRadius * 2f, drawRect.height);
            EditorGUI.DrawRect(fillRect, bgColor);
            EditorGUI.DrawRect(new Rect(drawRect.xMin, drawRect.yMin + cornerRadius, cornerRadius, drawRect.height - cornerRadius * 2f), bgColor);
        }

        private sealed class AssetSelectionPopupWindow : EditorWindow
        {
            private List<UnityEngine.Object> _assets;
            private Vector2 _scrollPosition;

            public static void Open(Rect buttonRect, ICollection<int> assetIds)
            {
                var window = CreateInstance<AssetSelectionPopupWindow>();
                window._assets = new List<UnityEngine.Object>();
                foreach (int id in assetIds)
                {
                    var asset = EditorUtility.InstanceIDToObject(id);
                    if (asset != null) window._assets.Add(asset);
                }
                var screenRect = GUIUtility.GUIToScreenRect(buttonRect);
                window.position = new Rect(screenRect.x, screenRect.y + screenRect.height, 300, Mathf.Min(400, window._assets.Count * 22 + 10));
                window.ShowPopup();
                window.Focus();
            }

            private void OnGUI()
            {
                if (_assets == null || _assets.Count == 0) { Close(); return; }
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                foreach (var asset in _assets)
                {
                    if (asset == null) continue;
                    string name = asset.name;
                    var path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path)) name = $"{name} ({path})";
                    if (GUILayout.Button(name, EditorStyles.miniButton))
                    {
                        EditorUtility.FocusProjectWindow();
                        var a = asset;
                        EditorApplication.delayCall += () => { Selection.activeObject = a; EditorGUIUtility.PingObject(a); };
                        Close();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
