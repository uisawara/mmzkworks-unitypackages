using UnityEditor;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Selects asmdef asset in Project window by node display name.</summary>
    public static class ProjectViewSelectionHelper
    {
        private const string DllPrefix = "DLL::";

        public static void SelectAsmdefInProject(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName) || nodeName.StartsWith(DllPrefix, System.StringComparison.Ordinal))
                return;

            if (!AsmdefDatabase.TryGetPathByDisplayName(nodeName, out var asmdefPath) || string.IsNullOrEmpty(asmdefPath))
                return;

            var guid = AssetDatabase.AssetPathToGUID(asmdefPath);
            if (string.IsNullOrEmpty(guid))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asmdefPath);
            if (asset == null)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath))
                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            }

            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                EditorUtility.FocusProjectWindow();
                var capturedAsset = asset;
                EditorApplication.delayCall += () =>
                {
                    if (capturedAsset != null)
                    {
                        Selection.activeObject = capturedAsset;
                        EditorGUIUtility.PingObject(capturedAsset);
                    }
                };
            }
        }
    }
}
