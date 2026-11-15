using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Mmzkworks.muDashboard.Editor
{
    internal static class DashboardAssetHandler
    {
        // Double-clicking a *.dashboard.json in the Project window opens it in the Dashboard window.
#if UNITY_6000_3_OR_NEWER
        [OnOpenAsset(0)]
        private static bool OnOpenAsset(EntityId entityId, int line) => TryOpen(AssetDatabase.GetAssetPath(entityId));
#else
        [OnOpenAsset(0)]
        private static bool OnOpenAsset(int instanceID, int line) => TryOpen(AssetDatabase.GetAssetPath(instanceID));
#endif

        private static bool TryOpen(string path)
        {
            if (!DashboardFiles.IsPanelFile(path)) return false;
            DashboardWindow.Open(path);
            return true;
        }

        [MenuItem("Assets/Create/muDashboard/Dashboard", false, 2040)]
        private static void CreatePanelAsset()
        {
            var folder = "Assets";
            var selected = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (!string.IsNullOrEmpty(selected))
                folder = AssetDatabase.IsValidFolder(selected) ? selected : Path.GetDirectoryName(selected)?.Replace('\\', '/');

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/NewPanel{DashboardFiles.Suffix}");
            var full = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", path);
            File.WriteAllText(full, DashboardSerializer.ToJson(new DashboardDocument()), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);

            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
