using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Collects all FileNameRules assets and caches validation results per GUID.
    /// Results depend only on asset paths and rules, so the cache is cleared when a rule changes
    /// or assets are moved / deleted (see ValidationStatus), not on every import.
    ///
    /// A file is checked against the rules whose folder is its nearest ancestor among all rule folders;
    /// it is valid if any of those rules allows it. If no rule covers it, the FileNameRules assets in the
    /// nearest ancestor folder decide whether it is allowed (allowOtherFiles).
    /// </summary>
    public static class FileNameRulesRegistry
    {
        private readonly struct RuleRef
        {
            public readonly FileNameRules Asset;
            public readonly FileNameRules.Rule Rule;

            public RuleRef(FileNameRules asset, FileNameRules.Rule rule)
            {
                Asset = asset;
                Rule = rule;
            }
        }

        // resolved folder path -> rules targeting that folder
        private static Dictionary<string, List<RuleRef>> _rulesByFolder;

        // folder path -> FileNameRules assets placed directly in that folder
        private static Dictionary<string, List<FileNameRules>> _assetsByFolder;

        // guid -> error message (null = valid / not a target)
        private static readonly Dictionary<string, string> ResultCache = new Dictionary<string, string>();

        // Loading a rule asset triggers its OnValidate, which must not reset the maps being built.
        private static bool _building;

        public static void Invalidate()
        {
            if (_building) return;
            _rulesByFolder = null;
            _assetsByFolder = null;
            ResultCache.Clear();
            ValidationStatus.RequestRescan();
            EditorApplication.RepaintProjectWindow();
        }

        /// <summary>
        /// Returns the error message for the asset, or null if it is valid or no rule applies.
        /// </summary>
        public static string GetError(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            if (ResultCache.TryGetValue(guid, out var cached)) return cached;

            var error = Evaluate(AssetDatabase.GUIDToAssetPath(guid), null);
            ResultCache[guid] = error;
            return error;
        }

        /// <summary>
        /// Lists asset paths that violate a rule of the given asset, with their messages.
        /// </summary>
        public static List<KeyValuePair<string, string>> CollectViolations(FileNameRules asset)
        {
            var result = new List<KeyValuePair<string, string>>();
            var folders = new HashSet<string>();
            if (asset.Folder != null) folders.Add(asset.Folder);
            if (asset.rules != null)
            {
                foreach (var rule in asset.rules)
                {
                    if (rule == null) continue;
                    var folder = asset.ResolveFolder(rule);
                    if (folder != null) folders.Add(folder);
                }
            }

            folders.RemoveWhere(folder => !AssetDatabase.IsValidFolder(folder));
            if (folders.Count == 0) return result;

            var deciders = new List<FileNameRules>();
            foreach (var guid in AssetDatabase.FindAssets("", folders.ToArray()).Distinct())
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                deciders.Clear();
                var error = Evaluate(path, deciders);
                if (error != null && deciders.Contains(asset)) result.Add(new KeyValuePair<string, string>(path, error));
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        /// <param name="deciders">If not null, receives the FileNameRules assets that decided the result.</param>
        private static string Evaluate(string assetPath, List<FileNameRules> deciders)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return null;
            if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(FileNameRules)) return null;

            EnsureMaps();
            if (_assetsByFolder.Count == 0) return null;

            var isFolder = AssetDatabase.IsValidFolder(assetPath);

            // The nearest folder targeted by any rule decides.
            for (var folder = FileNameRules.GetParentFolder(assetPath); folder != null; folder = FileNameRules.GetParentFolder(folder))
            {
                if (!_rulesByFolder.TryGetValue(folder, out var rules)) continue;

                if (isFolder) rules = rules.Where(r => r.Asset.validateFolders).ToList();
                if (rules.Count == 0) return null;

                deciders?.AddRange(rules.Select(r => r.Asset).Distinct());

                var relativePath = assetPath.Substring(folder.Length + 1);
                foreach (var rule in rules)
                {
                    if (rule.Asset.IsAllowed(rule.Rule, relativePath)) return null;
                }

                var patterns = rules
                    .SelectMany(r => r.Rule.patterns ?? new string[0])
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .Select(p => $"/{p}/");
                return $"Not allowed in {folder}/\n\"{relativePath}\" must match one of:\n" + string.Join("\n", patterns);
            }

            // No rule covers it: the nearest FileNameRules assets decide.
            for (var folder = FileNameRules.GetParentFolder(assetPath); folder != null; folder = FileNameRules.GetParentFolder(folder))
            {
                if (!_assetsByFolder.TryGetValue(folder, out var assets)) continue;

                if (isFolder) assets = assets.Where(a => a.validateFolders).ToList();
                var strict = assets.Where(a => !a.allowOtherFiles).ToList();
                if (strict.Count == 0) return null;

                deciders?.AddRange(strict);
                return $"Not covered by any rule, and other files are not allowed by {string.Join(", ", strict.Select(AssetDatabase.GetAssetPath))}";
            }

            return null;
        }

        private static void EnsureMaps()
        {
            if (_rulesByFolder != null && _assetsByFolder != null) return;

            var rulesByFolder = new Dictionary<string, List<RuleRef>>();
            var assetsByFolder = new Dictionary<string, List<FileNameRules>>();
            var paths = AssetDatabase.FindAssets("t:" + nameof(FileNameRules), new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal);

            _building = true;
            try
            {
                foreach (var path in paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<FileNameRules>(path);
                    if (asset == null) continue;

                    AddTo(assetsByFolder, FileNameRules.GetParentFolder(path), asset);
                    if (asset.rules == null) continue;

                    foreach (var rule in asset.rules)
                    {
                        if (rule == null) continue;
                        var folder = asset.ResolveFolder(rule);
                        if (folder != null) AddTo(rulesByFolder, folder, new RuleRef(asset, rule));
                    }
                }
            }
            finally
            {
                _building = false;
            }

            _rulesByFolder = rulesByFolder;
            _assetsByFolder = assetsByFolder;
        }

        private static void AddTo<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            if (key == null) return;
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<T>();
            list.Add(value);
        }
    }
}
