using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Project-wide asmdef collection, name/path/GUID/DisplayName mappings, and reference normalization.</summary>
    public static class AsmdefDatabase
    {
        private static readonly Dictionary<string, string> AsmdefNameToPath = new(256);
        private static readonly Dictionary<string, string> AsmdefGuidToName = new(256);
        private static readonly Dictionary<string, AsmdefInfo> AsmdefPathToInfo = new(256);
        private static readonly Dictionary<string, string> AsmdefPathToPackageName = new(256);
        private static readonly Dictionary<string, string> PackageNameToAsmdefPath = new(256);
        private static readonly Dictionary<string, string> GuidToDisplayName = new(256);
        private static readonly Dictionary<string, string> AsmdefNameToDisplayName = new(256);
        private static readonly Dictionary<string, string> PackageNameToDisplayName = new(256);
        private static readonly Dictionary<string, string> DisplayNameToPath = new(256);

        private static readonly Dictionary<string, AsmdefInfo> UnresolvedDisplayNameToInfo = new(64);
        private static readonly HashSet<string> UnresolvedDisplayNames = new();

        private const string UnresolvedNodePrefix = "Unresolved::";
        private const string PrefsKeyShowUnresolvedNodes = "Mmzkworks.muAsmdefgraph.ShowUnresolvedNodes";

        private static bool s_logEnumerationState = true;

        public static bool ShowUnresolvedNodes
        {
            get => EditorPrefs.GetBool(PrefsKeyShowUnresolvedNodes, false);
            set => EditorPrefs.SetBool(PrefsKeyShowUnresolvedNodes, value);
        }

        public static bool IsUnresolvedNode(string displayName)
        {
            return !string.IsNullOrEmpty(displayName) && UnresolvedDisplayNames.Contains(displayName);
        }

        public static void Refresh()
        {
            var rawDataList = CollectAllAsmdefs();
            BuildMappings(rawDataList);
            BuildGraphNodes(rawDataList);
            if (s_logEnumerationState)
                LogAsmdefEnumerationState(rawDataList);
        }

        public static bool TryGetAsmdefInfoByName(string asmdefName, out AsmdefInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(asmdefName))
                return false;

            string path = null;
            if (DisplayNameToPath.TryGetValue(asmdefName, out path) && !string.IsNullOrEmpty(path))
            {
                if (AsmdefPathToInfo.TryGetValue(path, out info) && info != null)
                    return true;
            }
            if (AsmdefNameToPath.TryGetValue(asmdefName, out path) && !string.IsNullOrEmpty(path))
            {
                if (AsmdefPathToInfo.TryGetValue(path, out info) && info != null)
                    return true;
            }
            if (PackageNameToAsmdefPath.TryGetValue(asmdefName, out path) && !string.IsNullOrEmpty(path))
            {
                if (AsmdefPathToInfo.TryGetValue(path, out info) && info != null)
                    return true;
            }
            if (UnresolvedDisplayNameToInfo.TryGetValue(asmdefName, out info) && info != null)
                return true;
            return false;
        }

        /// <summary>Resolve node display name to asmdef asset path (for Project view selection).</summary>
        public static bool TryGetPathByDisplayName(string displayName, out string asmdefPath)
        {
            asmdefPath = null;
            if (DisplayNameToPath.TryGetValue(displayName, out asmdefPath) && !string.IsNullOrEmpty(asmdefPath))
                return true;
            if (AsmdefNameToPath.TryGetValue(displayName, out asmdefPath) && !string.IsNullOrEmpty(asmdefPath))
                return true;
            if (PackageNameToAsmdefPath.TryGetValue(displayName, out asmdefPath) && !string.IsNullOrEmpty(asmdefPath))
                return true;
            return false;
        }

        /// <summary>Get display name for asmdef node (package name or asmdef name). Used by drawer.</summary>
        public static bool TryGetDisplayNameForPath(string asmdefPath, out string displayName)
        {
            displayName = null;
            if (AsmdefPathToPackageName.TryGetValue(asmdefPath, out var packageName) && !string.IsNullOrEmpty(packageName))
            {
                displayName = packageName;
                return true;
            }
            if (AsmdefPathToInfo.TryGetValue(asmdefPath, out var info) && info != null)
            {
                displayName = info.Name;
                return true;
            }
            return false;
        }

        public static bool TryGetPathByAsmdefName(string asmdefName, out string path)
        {
            return AsmdefNameToPath.TryGetValue(asmdefName, out path) && !string.IsNullOrEmpty(path);
        }

        private static List<AsmdefRawData> CollectAllAsmdefs()
        {
            var rawDataList = new List<AsmdefRawData>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fullPath = AsmdefPathHelper.ToFullPath(path);
                string json = null;
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    json = File.ReadAllText(fullPath);
                }
                else if (path.StartsWith("Packages/", StringComparison.Ordinal) && AsmdefPathHelper.TryGetResolvedFullPath(path, out var resolvedFullPath))
                {
                    json = File.ReadAllText(resolvedFullPath);
                }
                if (string.IsNullOrEmpty(json))
                    continue;

                var name = AsmdefJsonHelper.ExtractStringProperty(json, "name");
                if (string.IsNullOrEmpty(name))
                    continue;

                var rawData = new AsmdefRawData
                {
                    Guid = guid,
                    Path = path,
                    Name = name,
                    Json = json
                };

                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    rawData.PackageName = AsmdefPathHelper.ResolvePackageNameFromPath(path);

                var refs = AsmdefJsonHelper.ExtractStringArrayProperty(json, "references");
                if (refs != null)
                    rawData.RawReferences.AddRange(refs);

                rawDataList.Add(rawData);
            }

            return rawDataList;
        }

        private static void BuildMappings(List<AsmdefRawData> rawDataList)
        {
            AsmdefNameToPath.Clear();
            AsmdefGuidToName.Clear();
            AsmdefPathToPackageName.Clear();
            PackageNameToAsmdefPath.Clear();
            GuidToDisplayName.Clear();
            AsmdefNameToDisplayName.Clear();
            PackageNameToDisplayName.Clear();
            DisplayNameToPath.Clear();

            foreach (var rawData in rawDataList)
            {
                rawData.DisplayName = !string.IsNullOrEmpty(rawData.PackageName)
                    ? rawData.PackageName
                    : rawData.Name;

                var guidKey = NormalizeGuid(rawData.Guid);
                if (!string.IsNullOrEmpty(guidKey))
                {
                    AsmdefGuidToName[guidKey] = rawData.Name;
                    GuidToDisplayName[guidKey] = rawData.DisplayName;
                }

                AsmdefNameToPath[rawData.Name] = rawData.Path;
                AsmdefNameToDisplayName[rawData.Name] = rawData.DisplayName;

                if (!string.IsNullOrEmpty(rawData.PackageName))
                {
                    AsmdefPathToPackageName[rawData.Path] = rawData.PackageName;
                    PackageNameToAsmdefPath[rawData.PackageName] = rawData.Path;
                    PackageNameToDisplayName[rawData.PackageName] = rawData.DisplayName;
                }

                DisplayNameToPath[rawData.DisplayName] = rawData.Path;
            }
        }

        private static void BuildGraphNodes(List<AsmdefRawData> rawDataList)
        {
            AsmdefPathToInfo.Clear();
            UnresolvedDisplayNameToInfo.Clear();
            UnresolvedDisplayNames.Clear();

            foreach (var rawData in rawDataList)
            {
                var info = new AsmdefInfo { Name = rawData.DisplayName };

                foreach (var rawRef in rawData.RawReferences)
                {
                    var normalized = NormalizeAsmdefReference(rawRef, rawDataList);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        info.References.Add(normalized);
                    }
                    else
                    {
                        var unresolvedDisplayName = UnresolvedNodePrefix + rawRef;
                        info.References.Add(unresolvedDisplayName);
                        if (!UnresolvedDisplayNameToInfo.ContainsKey(unresolvedDisplayName))
                        {
                            UnresolvedDisplayNameToInfo[unresolvedDisplayName] = new AsmdefInfo { Name = unresolvedDisplayName, References = new List<string>() };
                            UnresolvedDisplayNames.Add(unresolvedDisplayName);
                        }
                        Debug.LogWarning($"[AsmdefGraph] Unresolved: asmdef=\"{rawData.Path}\" (\"{rawData.DisplayName ?? rawData.Name}\"), reference=\"{rawRef}\"");
                    }
                }

                var dlls = AsmdefJsonHelper.ExtractStringArrayProperty(rawData.Json, "precompiledReferences");
                if (dlls != null)
                    info.PrecompiledReferences.AddRange(dlls);

                AsmdefPathToInfo[rawData.Path] = info;
            }
        }

        private static void LogAsmdefEnumerationState(List<AsmdefRawData> rawDataList)
        {
            if (rawDataList == null || rawDataList.Count == 0)
            {
                Debug.Log("[AsmdefGraph] Enumeration: (empty)");
                return;
            }
            var sb = new StringBuilder();
            sb.Append("# AsmdefGraph Enumeration\n\n");
            sb.Append("| # | Guid | RawRefs | ResolvedRefs | Name | PkgName | DisplayName | Path |\n");
            sb.Append("|---|------|---------|--------------|------|---------|-------------|------|\n");
            for (int i = 0; i < rawDataList.Count; i++)
            {
                var d = rawDataList[i];
                var guid = d.Guid ?? "";
                var rawCount = d.RawReferences != null ? d.RawReferences.Count : 0;
                var resolvedCount = AsmdefPathToInfo.TryGetValue(d.Path, out var info) && info != null ? info.References.Count : 0;
                var name = d.Name ?? "";
                var pkg = string.IsNullOrEmpty(d.PackageName) ? "-" : d.PackageName;
                var disp = d.DisplayName ?? "";
                var path = d.Path ?? "";
                sb.Append("| ").Append(i + 1);
                sb.Append(" | ").Append(guid);
                sb.Append(" | ").Append(rawCount);
                sb.Append(" | ").Append(resolvedCount);
                sb.Append(" | ").Append(EscapeMarkdownTableCell(name));
                sb.Append(" | ").Append(EscapeMarkdownTableCell(pkg));
                sb.Append(" | ").Append(EscapeMarkdownTableCell(disp));
                sb.Append(" | ").Append(EscapeMarkdownTableCell(path));
                sb.Append(" |\n");
            }
            sb.Append("\nTotal: ").Append(rawDataList.Count).Append(" asmdefs");

            AppendUnresolvedReferencesSection(sb, rawDataList);

            var content = sb.ToString();
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var mdPath = Path.Combine(projectRoot, "Library", "AsmdefGraphEnumeration.md");
            try
            {
                var dir = Path.GetDirectoryName(mdPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(mdPath, content, Encoding.UTF8);
                var relativePath = "Library/AsmdefGraphEnumeration.md";
                Debug.Log("[AsmdefGraph] Enumeration written to " + relativePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AsmdefGraph] Failed to write enumeration .md: " + ex.Message);
            }
        }

        private static void AppendUnresolvedReferencesSection(StringBuilder sb, List<AsmdefRawData> rawDataList)
        {
            var hasAny = false;
            foreach (var d in rawDataList)
            {
                var rawCount = d.RawReferences != null ? d.RawReferences.Count : 0;
                var resolvedCount = AsmdefPathToInfo.TryGetValue(d.Path, out var info) && info != null ? info.References.Count : 0;
                if (rawCount <= resolvedCount || rawCount == 0) continue;
                if (!hasAny)
                {
                    sb.Append("\n\n## Unresolved references (RawRefs > ResolvedRefs)\n\n");
                    hasAny = true;
                }
                sb.Append("### ").Append(EscapeMarkdownTableCell(d.DisplayName ?? d.Name ?? "")).Append("\n");
                sb.Append("- **Path**: ").Append(EscapeMarkdownTableCell(d.Path ?? "")).Append("\n");
                sb.Append("- **Raw references**:\n");
                foreach (var rawRef in d.RawReferences ?? new List<string>())
                {
                    var kind = (TryExtractGuidReference(rawRef, out _) || IsGuidLike(rawRef)) ? "GUID" : "name";
                    var reason = GetUnresolvedReason(rawRef, rawDataList);
                    sb.Append("  - `").Append(EscapeMarkdownTableCell(rawRef)).Append("` (").Append(kind).Append(") → ").Append(reason).Append("\n");
                }
                sb.Append("\n");
            }
            if (hasAny)
            {
                sb.Append("## Known display names (for comparison)\n\n");
                var names = new List<string>();
                foreach (var d in rawDataList)
                {
                    if (!string.IsNullOrEmpty(d.DisplayName))
                        names.Add(d.DisplayName);
                }
                names.Sort(StringComparer.Ordinal);
                sb.Append(string.Join(", ", names));
                sb.Append("\n");
            }
        }

        private static string GetUnresolvedReason(string reference, List<AsmdefRawData> rawDataList)
        {
            if (string.IsNullOrEmpty(reference)) return "empty";
            if (TryExtractGuidReference(reference, out var guidPart)) reference = guidPart;
            if (IsGuidLike(reference))
            {
                var normalizedGuid = NormalizeGuid(reference);
                if (GuidToDisplayName.TryGetValue(normalizedGuid, out _)) return "resolved (GuidToDisplayName)";
                foreach (var rawData in rawDataList)
                {
                    if (rawData != null && string.Equals(rawData.Guid, reference, StringComparison.OrdinalIgnoreCase))
                        return "resolved (rawData.Guid)";
                }
                var path = AssetDatabase.GUIDToAssetPath(normalizedGuid);
                if (string.IsNullOrEmpty(path)) return "GUIDToAssetPath returned empty";
                if (!path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) return "GUIDToAssetPath not .asmdef: " + path;
                foreach (var rawData in rawDataList)
                {
                    if (rawData != null && rawData.Path == path) return "resolved (rawData.Path)";
                }
                var fallback = GetDisplayNameForAsmdefPath(path);
                if (!string.IsNullOrEmpty(fallback)) return "resolved (GetDisplayNameForAsmdefPath)";
                return "path not in rawDataList; fallback read failed";
            }
            if (PackageNameToDisplayName.TryGetValue(reference, out _)) return "resolved (PackageNameToDisplayName)";
            if (AsmdefNameToDisplayName.TryGetValue(reference, out _)) return "resolved (AsmdefNameToDisplayName)";
            foreach (var rawData in rawDataList)
            {
                if (rawData.Name == reference || rawData.PackageName == reference) return "resolved (rawData name/pkg)";
            }
            return "name not in PackageName/AsmdefName/rawDataList";
        }

        private static string EscapeMarkdownTableCell(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
        }

        private static string NormalizeAsmdefReference(string reference, List<AsmdefRawData> rawDataList)
        {
            if (string.IsNullOrEmpty(reference))
                return string.Empty;
            if (TryExtractGuidReference(reference, out var guidPart))
                reference = guidPart;

            if (IsGuidLike(reference))
            {
                var normalizedGuid = NormalizeGuid(reference);
                if (GuidToDisplayName.TryGetValue(normalizedGuid, out var displayName) && !string.IsNullOrEmpty(displayName))
                    return displayName;

                foreach (var rawData in rawDataList)
                {
                    if (rawData != null && string.Equals(rawData.Guid, reference, StringComparison.OrdinalIgnoreCase))
                        return rawData.DisplayName;
                }

                if (!string.IsNullOrEmpty(normalizedGuid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(normalizedGuid);
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var rawData in rawDataList)
                        {
                            if (rawData != null && rawData.Path == path)
                                return rawData.DisplayName;
                        }

                        var fallbackDisplayName = GetDisplayNameForAsmdefPath(path);
                        if (!string.IsNullOrEmpty(fallbackDisplayName))
                            return fallbackDisplayName;
                    }
                }
            }
            else
            {
                if (PackageNameToDisplayName.TryGetValue(reference, out var packageDisplayName) && !string.IsNullOrEmpty(packageDisplayName))
                    return packageDisplayName;
                if (AsmdefNameToDisplayName.TryGetValue(reference, out var asmdefDisplayName) && !string.IsNullOrEmpty(asmdefDisplayName))
                    return asmdefDisplayName;
                foreach (var rawData in rawDataList)
                {
                    if (rawData.Name == reference || rawData.PackageName == reference)
                        return rawData.DisplayName;
                }
            }

            return string.Empty;
        }

        private static string GetDisplayNameForAsmdefPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            string json = null;
            if (AsmdefPathHelper.TryGetResolvedFullPath(path, out var resolvedFullPath) && File.Exists(resolvedFullPath))
                json = File.ReadAllText(resolvedFullPath);
            else
            {
                var fullPath = AsmdefPathHelper.ToFullPath(path);
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                    json = File.ReadAllText(fullPath);
            }
            if (string.IsNullOrEmpty(json))
                return string.Empty;
            var name = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var packageDisplayName = AsmdefPathHelper.ResolvePackageNameFromPath(path);
                return !string.IsNullOrEmpty(packageDisplayName) ? packageDisplayName : name;
            }
            return name;
        }

        /// <summary>Extract 32-char GUID from "GUID:xxxxxxxx..." format (Unity asmdef references).</summary>
        private static bool TryExtractGuidReference(string reference, out string guidPart)
        {
            guidPart = null;
            if (string.IsNullOrEmpty(reference) || reference.Length < 5 + 32)
                return false;
            if (!reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                return false;
            guidPart = reference.Substring(5, 32);
            return IsGuidLike(guidPart);
        }

        private static bool IsGuidLike(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 32)
                return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private static string NormalizeGuid(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 32)
                return s ?? string.Empty;
            return s.ToLowerInvariant();
        }
    }
}
