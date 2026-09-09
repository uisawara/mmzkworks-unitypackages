using System;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    public static class AsmdefGraphMenuItems
    {
        // Hierarchy 右クリック時は context が null で渡ることがある（Unity の挙動）→ その場合は選択中の GameObject を使う
        private static GameObject GetGameObjectFromMenuContext(MenuCommand menuCommand)
        {
            if (menuCommand.context is GameObject go)
                return go;
            if (menuCommand.context is Component c)
                return c.gameObject;
            if (menuCommand.context == null)
                return Selection.activeGameObject;
            return null;
        }

        [MenuItem("Assets/Open Asmdef Graph", false, 1000)]
        private static void OpenAsmdefGraphFromMenu()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return;
            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                return;
            ComponentAsmdefGraphWindow.OpenForAsmdef(path);
        }

        [MenuItem("Assets/Open Asmdef Graph", true)]
        private static bool ValidateOpenAsmdefGraphFromMenu()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return false;
            var path = AssetDatabase.GetAssetPath(selected);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);
        }

        // GameObject context menu: mu 系ベース（muHierarchy と揃える） + 相対値で位置指定
        private const int GameObjectMenuPriorityBase = 21000;

        [MenuItem("GameObject/muAsmdefgraph/Open Component Asmdef Graph", false, GameObjectMenuPriorityBase + 10)]
        private static void OpenAsmdefGraphContext(MenuCommand menuCommand)
        {
            var go = GetGameObjectFromMenuContext(menuCommand);
            if (go == null)
                return;

            var asmdefNames = ComponentAsmdefGraphWindow.GetAsmdefNamesForGameObject(go);
            if (asmdefNames.Count <= 1)
            {
                ComponentAsmdefGraphWindow.OpenFor(go);
                return;
            }

            var menu = new GenericMenu();
            foreach (var name in asmdefNames)
            {
                var asmdefName = name;
                menu.AddItem(new GUIContent(asmdefName), false, () => ComponentAsmdefGraphWindow.OpenForAsmdefName(asmdefName));
            }

            // この時点でカーソル位置を取得（MenuItem 実行直後は Event.current が有効なことが多い）
            var screenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (Event.current != null)
                screenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

            // 1 フレーム遅延してから DropDown（ShowAsContext だけでは出ないため）
            EditorApplication.delayCall += () =>
            {
                menu.DropDown(new Rect(screenPos, Vector2.zero));
            };
        }

        [MenuItem("GameObject/muAsmdefgraph/Open Component Asmdef Graph", true)]
        private static bool OpenAsmdefGraphContextValidate(MenuCommand menuCommand)
        {
            return GetGameObjectFromMenuContext(menuCommand) != null;
        }
    }
}
