using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muProject.Editor
{
    /// <summary>
    /// Shows the icon of the first FolderSettings rule whose regular expression matches the folder path.
    /// A FolderSettings asset applies only to the folder it is in and the folders below it.
    /// When several assets apply, the one in the deepest folder is used first.
    /// Asked before the built-in providers, so a rule can override e.g. the package icon.
    /// </summary>
    public class FolderSettingsFolderIconProvider : IFolderIconProvider
    {
        private (string scope, Regex pattern, Texture icon)[] _rules;

        public FolderSettingsFolderIconProvider()
        {
            FolderIconDrawer.Invalidated += () => _rules = null;
        }

        public int Order => -100;

        public Texture GetIcon(string folderPath)
        {
            foreach (var (scope, pattern, icon) in Rules)
            {
                if (!IsInScope(folderPath, scope)) continue;
                if (pattern.IsMatch(folderPath)) return icon;
            }

            return null;
        }

        private static bool IsInScope(string folderPath, string scope)
        {
            return folderPath.Length == scope.Length
                ? string.Equals(folderPath, scope, StringComparison.Ordinal)
                : folderPath.Length > scope.Length && folderPath[scope.Length] == '/' &&
                  folderPath.StartsWith(scope, StringComparison.Ordinal);
        }

        private (string scope, Regex pattern, Texture icon)[] Rules
        {
            get
            {
                if (_rules != null) return _rules;

                var rules = new List<(string, Regex, Texture)>();
                var paths = AssetDatabase.FindAssets($"t:{nameof(FolderSettings)}")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderByDescending(p => p.Count(c => c == '/'))
                    .ThenBy(p => p, StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    var settings = AssetDatabase.LoadAssetAtPath<FolderSettings>(path);
                    if (settings == null || settings.folderIcons == null) continue;

                    var scope = path.Substring(0, path.LastIndexOf('/'));

                    foreach (var rule in settings.folderIcons)
                    {
                        if (rule == null || rule.icon == null || string.IsNullOrEmpty(rule.pathPattern)) continue;
                        try
                        {
                            rules.Add((scope, new Regex(rule.pathPattern, RegexOptions.CultureInvariant), rule.icon));
                        }
                        catch (ArgumentException e)
                        {
                            Debug.LogWarning($"[muProject] Invalid path pattern \"{rule.pathPattern}\" in {path}: {e.Message}", settings);
                        }
                    }
                }

                _rules = rules.ToArray();
                return _rules;
            }
        }
    }
}
