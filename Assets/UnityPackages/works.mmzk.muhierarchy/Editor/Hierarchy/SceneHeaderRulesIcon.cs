using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Draws an icon on the right of scene header rows when muValidation SceneRules apply to the scene.
    /// Click it to select the SceneRules asset (a menu chooses when several apply).
    /// Nothing is drawn when muValidation is not installed.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneHeaderRulesIcon
    {
        private const float IconSize = 16f;

        // Keeps clear of the scene header's own menu button at the right end.
        private const float RightMargin = 20f;

        private static Texture _icon;

        static SceneHeaderRulesIcon()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            // Scene header rows pass the scene handle instead of an object instance ID.
            if (!TryGetSceneByHandle(instanceID, out var scene)) return;

            var rulePaths = MuValidationBridge.GetSceneRulePaths(scene.path);
            if (rulePaths.Length == 0) return;

            _icon ??= EditorGUIUtility.IconContent("console.infoicon.sml").image;
            var rect = new Rect(
                selectionRect.xMax - RightMargin - IconSize,
                selectionRect.y + (selectionRect.height - IconSize) * 0.5f,
                IconSize,
                IconSize);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            GUI.Label(rect, _icon);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                OpenRules(rulePaths, rect);
                e.Use();
            }
        }

        private static void OpenRules(string[] rulePaths, Rect rect)
        {
            if (rulePaths.Length == 1)
            {
                Select(rulePaths[0]);
                return;
            }

            var menu = new GenericMenu();
            foreach (var path in rulePaths)
            {
                // Slashes would make submenus.
                menu.AddItem(new GUIContent(path.Replace('/', '\u2215')), false, () => Select(path));
            }

            menu.DropDown(rect);
        }

        private static void Select(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static bool TryGetSceneByHandle(int handle, out Scene scene)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                scene = SceneManager.GetSceneAt(i);
                if (scene.handle == handle) return true;
            }

            scene = default;
            return false;
        }
    }
}
