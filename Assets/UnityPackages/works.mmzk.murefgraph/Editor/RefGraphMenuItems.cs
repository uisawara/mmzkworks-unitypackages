using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    public static class RefGraphMenuItems
    {
        // GameObject context menu: mu 系ベース（muHierarchy / muAsmdefgraph と揃える） + 相対値で位置指定
        private const int GameObjectMenuPriorityBase = 21000;

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

        [MenuItem("GameObject/muRefgraph/Open Ref Graph", false, GameObjectMenuPriorityBase + 20)]
        private static void OpenFromHierarchy(MenuCommand menuCommand)
        {
            var go = GetGameObjectFromMenuContext(menuCommand);
            if (go != null)
                RefGraphWindow.OpenFor(go);
        }

        [MenuItem("GameObject/muRefgraph/Open Ref Graph", true)]
        private static bool ValidateOpenFromHierarchy(MenuCommand menuCommand)
        {
            return GetGameObjectFromMenuContext(menuCommand) != null;
        }

        [MenuItem("Assets/Open Ref Graph", false, 1001)]
        private static void OpenFromProject()
        {
            if (Selection.activeObject is GameObject go)
                RefGraphWindow.OpenFor(go);
        }

        [MenuItem("Assets/Open Ref Graph", true)]
        private static bool ValidateOpenFromProject()
        {
            return Selection.activeObject is GameObject go && EditorUtility.IsPersistent(go);
        }

        [MenuItem("Window/muRefgraph/Ref Graph")]
        private static void OpenWindow()
        {
            RefGraphWindow.OpenFor(Selection.activeGameObject);
        }
    }
}
