using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Mmzkworks.muShortcutPanel.Editor
{
    internal static class ShortcutPanelAssetHandler
    {
        // Double-clicking a *.shortcutpanel.json in the Project window opens it in the ShortcutPanel window.
#if UNITY_6000_3_OR_NEWER
        [OnOpenAsset(0)]
        private static bool OnOpenAsset(EntityId entityId, int line) => TryOpen(AssetDatabase.GetAssetPath(entityId));
#else
        [OnOpenAsset(0)]
        private static bool OnOpenAsset(int instanceID, int line) => TryOpen(AssetDatabase.GetAssetPath(instanceID));
#endif

        private static bool TryOpen(string path)
        {
            if (!ShortcutPanelFiles.IsPanelFile(path)) return false;
            ShortcutPanelWindow.Open(path);
            return true;
        }

        [MenuItem("Assets/Create/muShortcutPanel/Shortcut Panel", false, 2040)]
        private static void CreatePanelAsset()
        {
            var folder = "Assets";
            var selected = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (!string.IsNullOrEmpty(selected))
                folder = AssetDatabase.IsValidFolder(selected) ? selected : Path.GetDirectoryName(selected)?.Replace('\\', '/');

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/NewPanel{ShortcutPanelFiles.Suffix}");
            var full = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", path);
            File.WriteAllText(full, ShortcutPanelSerializer.ToJson(new ShortcutPanelDocument()), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);

            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
