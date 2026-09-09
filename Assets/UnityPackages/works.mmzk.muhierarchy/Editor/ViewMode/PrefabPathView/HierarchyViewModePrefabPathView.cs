using System;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModePrefabPathViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModePrefabPathViewHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.PrefabPathView;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            bool isPrefabRoot = PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.IsAnyPrefabInstanceRoot(go);
            if (!isPrefabRoot)
                return;

            var path = HierarchyDrawUtils.GetPrefabSourcePath(go);
            if (string.IsNullOrEmpty(path))
                return;

            var displayPath = path;
            if (displayPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                displayPath = displayPath.Substring(0, displayPath.Length - ".prefab".Length);

            var content = new GUIContent(displayPath);
            float w = ctx.SmallLabelStyle.CalcSize(content).x;
            var pathRect = new Rect(
                ctx.FixedRightPosition - w,
                selectionRect.y,
                w,
                selectionRect.height
            );

            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && pathRect.Contains(e.mousePosition))
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset != null)
                {
                    Selection.activeObject = prefabAsset;
                    EditorGUIUtility.PingObject(prefabAsset);
                    e.Use();
                }
            }

            GUI.Label(pathRect, content, ctx.SmallLabelStyle);
        }
    }
}
