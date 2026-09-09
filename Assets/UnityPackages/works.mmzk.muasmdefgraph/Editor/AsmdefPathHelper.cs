using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Asset path and asmdef name resolution utilities.</summary>
    public static class AsmdefPathHelper
    {
        /// <summary>Resolve Packages/ logical path to actual filesystem path (e.g. Library/PackageCache). Returns true only if the file exists.</summary>
        public static bool TryGetResolvedFullPath(string assetPath, out string resolvedFullPath)
        {
            resolvedFullPath = null;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Packages/", System.StringComparison.Ordinal))
                return false;
#if UNITY_EDITOR
            var packageInfo = PackageInfo.FindForAssetPath(assetPath);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                var packagesPrefixLen = "Packages/".Length;
                var firstSlash = assetPath.IndexOf('/', packagesPrefixLen);
                if (firstSlash < 0)
                    return false;
                var relativePath = assetPath.Substring(firstSlash + 1).Replace('/', Path.DirectorySeparatorChar);
                resolvedFullPath = Path.Combine(packageInfo.resolvedPath, relativePath);
                if (File.Exists(resolvedFullPath))
                    return true;
            }

            if (TryResolveFullPathFromAllPackages(assetPath, out resolvedFullPath))
                return true;
            return false;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private static bool TryResolveFullPathFromAllPackages(string assetPath, out string resolvedFullPath)
        {
            resolvedFullPath = null;
            PackageInfo[] packages = null;
#if UNITY_2021_1_OR_NEWER
            packages = PackageInfo.GetAllRegisteredPackages();
#else
            var method = typeof(PackageInfo).GetMethod("GetAll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            packages = method?.Invoke(null, null) as PackageInfo[];
#endif
            if (packages == null || packages.Length == 0)
                return false;

            PackageInfo best = null;
            int bestLen = 0;
            foreach (var pkg in packages)
            {
                if (string.IsNullOrEmpty(pkg.assetPath) || string.IsNullOrEmpty(pkg.resolvedPath))
                    continue;
                if (assetPath == pkg.assetPath || assetPath.StartsWith(pkg.assetPath + "/", StringComparison.Ordinal))
                {
                    if (pkg.assetPath.Length > bestLen)
                    {
                        bestLen = pkg.assetPath.Length;
                        best = pkg;
                    }
                }
            }
            if (best == null)
                return false;

            var relativePath = assetPath.Length > best.assetPath.Length
                ? assetPath.Substring(best.assetPath.Length).TrimStart('/')
                : "";
            resolvedFullPath = Path.Combine(best.resolvedPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(resolvedFullPath);
        }
#endif

        public static string GetProjectRootFullPath()
        {
            return Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
        }

        public static string ToFullPath(string assetPathOrPackagePath)
        {
            if (string.IsNullOrEmpty(assetPathOrPackagePath))
                return string.Empty;
            var root = GetProjectRootFullPath();
            if (string.IsNullOrEmpty(root))
                return string.Empty;
            return Path.GetFullPath(Path.Combine(root, assetPathOrPackagePath)).Replace('\\', '/');
        }

        public static string ToAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;
            var root = GetProjectRootFullPath();
            if (string.IsNullOrEmpty(root))
                return string.Empty;
            fullPath = fullPath.Replace('\\', '/');
            root = root.Replace('\\', '/').TrimEnd('/');
            if (!fullPath.StartsWith(root + "/", System.StringComparison.Ordinal))
                return string.Empty;
            return fullPath.Substring(root.Length + 1);
        }

        public static string ResolvePackageNameFromPath(string asmdefPath)
        {
            if (string.IsNullOrEmpty(asmdefPath) || !asmdefPath.StartsWith("Packages/", System.StringComparison.Ordinal))
                return string.Empty;

            var parts = asmdefPath.Split('/');
            if (parts.Length < 2)
                return string.Empty;

            var packageFolder = parts[1];
            var packageJsonPath = $"Packages/{packageFolder}/package.json";
            var fullPackageJsonPath = ToFullPath(packageJsonPath);
            if (!string.IsNullOrEmpty(fullPackageJsonPath) && File.Exists(fullPackageJsonPath))
            {
                try
                {
                    var packageJson = File.ReadAllText(fullPackageJsonPath);
                    var displayName = AsmdefJsonHelper.ExtractStringProperty(packageJson, "displayName");
                    if (!string.IsNullOrEmpty(displayName))
                        return displayName;
                    var packageName = AsmdefJsonHelper.ExtractStringProperty(packageJson, "name");
                    if (!string.IsNullOrEmpty(packageName))
                        return packageName;
                }
                catch { }
            }
#if UNITY_EDITOR
            var packageRootAssetPath = $"Packages/{packageFolder}";
            var packageInfo = PackageInfo.FindForAssetPath(packageRootAssetPath);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                var resolvedPackageJsonPath = Path.Combine(packageInfo.resolvedPath, "package.json");
                if (File.Exists(resolvedPackageJsonPath))
                {
                    try
                    {
                        var packageJson = File.ReadAllText(resolvedPackageJsonPath);
                        var displayName = AsmdefJsonHelper.ExtractStringProperty(packageJson, "displayName");
                        if (!string.IsNullOrEmpty(displayName))
                            return displayName;
                        var packageName = AsmdefJsonHelper.ExtractStringProperty(packageJson, "name");
                        if (!string.IsNullOrEmpty(packageName))
                            return packageName;
                    }
                    catch { }
                }
            }
#endif

            return packageFolder;
        }

        public static string ResolveAsmdefNameFromPath(string asmdefPath)
        {
            if (string.IsNullOrEmpty(asmdefPath) || !asmdefPath.EndsWith(".asmdef", System.StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var fullPath = ToFullPath(asmdefPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return string.Empty;

            try
            {
                var json = File.ReadAllText(fullPath);
                return AsmdefJsonHelper.ExtractStringProperty(json, "name");
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ResolveNearestAsmdefNameForScript(string scriptAssetPath)
        {
            var dir = Path.GetDirectoryName(scriptAssetPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir))
            {
                var fullDir = ToFullPath(dir);
                if (!string.IsNullOrEmpty(fullDir) && Directory.Exists(fullDir))
                {
                    var files = Directory.GetFiles(fullDir, "*.asmdef", SearchOption.TopDirectoryOnly);
                    if (files != null && files.Length > 0)
                    {
                        System.Array.Sort(files, System.StringComparer.Ordinal);
                        var asmdefPath = ToAssetPath(files[0]);
                        if (!string.IsNullOrEmpty(asmdefPath))
                        {
                            var fullAsmdef = ToFullPath(asmdefPath);
                            if (File.Exists(fullAsmdef))
                            {
                                var json = File.ReadAllText(fullAsmdef);
                                return AsmdefJsonHelper.ExtractStringProperty(json, "name");
                            }
                        }
                    }
                }

                if (string.Equals(dir, "Assets", System.StringComparison.Ordinal) || string.Equals(dir, "Packages", System.StringComparison.Ordinal))
                    break;
                dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            }
            return string.Empty;
        }
    }
}
