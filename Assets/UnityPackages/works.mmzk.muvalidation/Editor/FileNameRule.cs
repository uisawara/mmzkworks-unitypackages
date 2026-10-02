using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Set of file path rules. Each rule names a folder relative to this asset and the file paths allowed in it.
    /// Several FileNameRule assets may be placed in the same folder; their rules are combined.
    /// </summary>
    [CreateAssetMenu(fileName = "FileNameRule", menuName = "muValidation/File Name Rule")]
    public class FileNameRule : ScriptableObject
    {
        [Serializable]
        public class Rule
        {
            [Tooltip("Folder relative to this asset's folder, e.g. Textures or ../Shared. Empty means this asset's folder.")]
            public string path = "";

            [Tooltip("Regex for allowed files, matched against the path relative to the folder above, " +
                     "e.g. T_Hero.png or Sub/T_Hero.png. Use ^...$ for a full match. Empty allows any file.")]
            public string[] patterns = Array.Empty<string>();

            [NonSerialized] internal Regex[] Compiled;
        }

        [Tooltip("Rules for folders relative to this asset. The rule with the deepest folder covering a file decides.")]
        public Rule[] rules = Array.Empty<Rule>();

        [Tooltip("Allow files under this asset's folder that no rule covers.")]
        public bool allowOtherFiles = true;

        [Tooltip("Also validate folders, not only files.")]
        public bool validateFolders;

        /// <summary>
        /// Folder this asset is placed in, e.g. Assets/Characters.
        /// </summary>
        public string Folder => GetParentFolder(AssetDatabase.GetAssetPath(this));

        /// <summary>
        /// Resolves a rule's relative path to a project path, e.g. Assets/Characters/Textures.
        /// Returns null if it points outside the project.
        /// </summary>
        public string ResolveFolder(Rule rule)
        {
            var folder = Folder;
            if (string.IsNullOrEmpty(folder)) return null;

            var parts = new List<string>(folder.Split('/'));
            foreach (var part in (rule.path ?? "").Replace('\\', '/').Split('/'))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0 || trimmed == ".") continue;
                if (trimmed == "..")
                {
                    if (parts.Count <= 1) return null;
                    parts.RemoveAt(parts.Count - 1);
                }
                else
                {
                    parts.Add(trimmed);
                }
            }

            return string.Join("/", parts);
        }

        /// <summary>
        /// True if the path (relative to the rule's folder) matches one of its patterns, or it has none.
        /// </summary>
        public bool IsAllowed(Rule rule, string relativePath)
        {
            rule.Compiled ??= Compile(rule.patterns);
            if (rule.Compiled.Length == 0) return true;

            foreach (var regex in rule.Compiled)
            {
                if (regex.IsMatch(relativePath)) return true;
            }

            return false;
        }

        private Regex[] Compile(string[] patterns)
        {
            var result = new List<Regex>();
            if (patterns == null) return result.ToArray();

            foreach (var pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                try
                {
                    result.Add(new Regex(pattern));
                }
                catch (ArgumentException e)
                {
                    Debug.LogWarning($"[muValidation] Invalid regex \"{pattern}\" in {AssetDatabase.GetAssetPath(this)}: {e.Message}", this);
                }
            }

            return result.ToArray();
        }

        internal static string GetParentFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var index = path.LastIndexOf('/');
            return index <= 0 ? null : path.Substring(0, index);
        }

        private void OnValidate()
        {
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    if (rule != null) rule.Compiled = null;
                }
            }

            FileNameRuleRegistry.Invalidate();
        }
    }
}
