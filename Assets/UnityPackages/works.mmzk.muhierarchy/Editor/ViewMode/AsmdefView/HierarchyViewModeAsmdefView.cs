using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public static class AsmdefResolver
    {
        private static readonly Dictionary<string, string> ScriptPathToAsmdefPathCache = new Dictionary<string, string>(2048);
        private static readonly Dictionary<string, string> AsmdefPathToNameCache = new Dictionary<string, string>(256);

        public static void ClearCaches()
        {
            ScriptPathToAsmdefPathCache.Clear();
            AsmdefPathToNameCache.Clear();
        }

        public static string BuildAsmdefNameText(GameObject go, int maxNames)
        {
            if (go == null)
                return string.Empty;

            var monoBehaviours = go.GetComponents<MonoBehaviour>();
            if (monoBehaviours == null || monoBehaviours.Length == 0)
                return string.Empty;

            var names = new List<string>(Mathf.Max(4, maxNames));
            var seen = new HashSet<string>();
            bool hadAnyScript = false;
            bool truncated = false;

            foreach (var mb in monoBehaviours)
            {
                if (mb == null)
                    continue;
                var ms = MonoScript.FromMonoBehaviour(mb);
                if (ms == null)
                    continue;
                hadAnyScript = true;
                var scriptPath = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(scriptPath))
                    continue;
                var asmdefName = ResolveAsmdefNameForScriptPath(scriptPath);
                if (string.IsNullOrEmpty(asmdefName))
                    continue;
                if (seen.Add(asmdefName))
                    names.Add(asmdefName);
                if (names.Count >= maxNames)
                {
                    truncated = true;
                    break;
                }
            }

            if (names.Count == 0)
                return hadAnyScript ? "(no asmdef)" : string.Empty;
            var text = string.Join(", ", names);
            return truncated ? text + ", ..." : text;
        }

        private static string ResolveAsmdefNameForScriptPath(string scriptAssetPath)
        {
            if (string.IsNullOrEmpty(scriptAssetPath))
                return string.Empty;
            if (ScriptPathToAsmdefPathCache.TryGetValue(scriptAssetPath, out var asmdefPathCached))
            {
                if (string.IsNullOrEmpty(asmdefPathCached))
                    return string.Empty;
                return ResolveAsmdefNameFromAsmdefPath(asmdefPathCached);
            }
            var dir = Path.GetDirectoryName(scriptAssetPath)?.Replace('\\', '/');
            string asmdefPath = string.Empty;
            while (!string.IsNullOrEmpty(dir))
            {
                var fullDir = ToFullPath(dir);
                if (!string.IsNullOrEmpty(fullDir) && Directory.Exists(fullDir))
                {
                    try
                    {
                        var files = Directory.GetFiles(fullDir, "*.asmdef", SearchOption.TopDirectoryOnly);
                        if (files != null && files.Length > 0)
                        {
                            Array.Sort(files, StringComparer.Ordinal);
                            asmdefPath = ToAssetPath(files[0]);
                        }
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(asmdefPath))
                    break;
                if (string.Equals(dir, "Assets", StringComparison.Ordinal) || string.Equals(dir, "Packages", StringComparison.Ordinal))
                    break;
                dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            }
            ScriptPathToAsmdefPathCache[scriptAssetPath] = asmdefPath ?? string.Empty;
            if (string.IsNullOrEmpty(asmdefPath))
                return string.Empty;
            return ResolveAsmdefNameFromAsmdefPath(asmdefPath);
        }

        private static string GetProjectRootFullPath()
        {
            return Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
        }

        private static string ToFullPath(string assetPathOrPackagePath)
        {
            if (string.IsNullOrEmpty(assetPathOrPackagePath))
                return string.Empty;
            var root = GetProjectRootFullPath();
            if (string.IsNullOrEmpty(root))
                return string.Empty;
            return Path.GetFullPath(Path.Combine(root, assetPathOrPackagePath)).Replace('\\', '/');
        }

        private static string ToAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;
            var root = GetProjectRootFullPath();
            if (string.IsNullOrEmpty(root))
                return string.Empty;
            fullPath = fullPath.Replace('\\', '/');
            root = root.Replace('\\', '/').TrimEnd('/');
            if (!fullPath.StartsWith(root + "/", StringComparison.Ordinal))
                return string.Empty;
            return fullPath.Substring(root.Length + 1);
        }

        private static string ResolveAsmdefNameFromAsmdefPath(string asmdefAssetPath)
        {
            if (string.IsNullOrEmpty(asmdefAssetPath))
                return string.Empty;
            if (AsmdefPathToNameCache.TryGetValue(asmdefAssetPath, out var nameCached))
                return nameCached ?? string.Empty;
            try
            {
                var fullPath = Path.GetFullPath(asmdefAssetPath);
                if (!File.Exists(fullPath))
                {
                    AsmdefPathToNameCache[asmdefAssetPath] = string.Empty;
                    return string.Empty;
                }
                var json = File.ReadAllText(fullPath);
                var name = ExtractAsmdefNameFromJson(json);
                AsmdefPathToNameCache[asmdefAssetPath] = name ?? string.Empty;
                return name ?? string.Empty;
            }
            catch
            {
                AsmdefPathToNameCache[asmdefAssetPath] = string.Empty;
                return string.Empty;
            }
        }

        private static string ExtractAsmdefNameFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;
            const string key = "\"name\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
                return string.Empty;
            idx = json.IndexOf(':', idx);
            if (idx < 0)
                return string.Empty;
            idx = json.IndexOf('"', idx);
            if (idx < 0)
                return string.Empty;
            int start = idx + 1;
            int end = json.IndexOf('"', start);
            if (end < 0 || end <= start)
                return string.Empty;
            return json.Substring(start, end - start);
        }
    }

    public sealed class HierarchyViewModeAsmdefViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeAsmdefViewHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.AsmdefView;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            var text = AsmdefResolver.BuildAsmdefNameText(go, 6);
            if (!string.IsNullOrEmpty(text))
            {
                HierarchyDrawUtils.DrawRightAlignedSmallText(selectionRect, ctx.FixedRightPosition, text, ctx.SmallLabelStyle);
            }
        }
    }
}
