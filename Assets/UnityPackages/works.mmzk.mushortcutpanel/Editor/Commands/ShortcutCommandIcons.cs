using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muShortcutPanel.Editor
{
    /// <summary>Built-in editor icons per command category. Names differ between Unity versions, so the first one that exists is used.</summary>
    internal static class ShortcutCommandIcons
    {
        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { ShortcutCommandCategory.Prefab, new[] { "Prefab Icon", "PrefabModel Icon" } },
            { ShortcutCommandCategory.GameObject, new[] { "GameObject Icon", "GameObject On Icon" } },
            { ShortcutCommandCategory.Scene, new[] { "SceneAsset Icon", "UnityLogo" } },
            { ShortcutCommandCategory.Asset, new[] { "Folder Icon", "FolderEmpty Icon" } },
        };

        private static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();
        private static bool _cacheIsPro;

        public static Texture For(ShortcutCommand command) => command == null ? null : ForCategory(command.Category);

        public static Texture ForCategory(string category)
        {
            if (string.IsNullOrEmpty(category) || !Names.TryGetValue(category, out var names)) return null;

            var pro = EditorGUIUtility.isProSkin;
            if (_cacheIsPro != pro)
            {
                Cache.Clear();
                _cacheIsPro = pro;
            }
            if (Cache.TryGetValue(category, out var cached)) return cached;

            Texture found = null;
            foreach (var name in names)
            {
                // FindTexture returns null quietly for unknown names (IconContent would log an error).
                if (pro) found = EditorGUIUtility.FindTexture("d_" + name);
                if (found == null) found = EditorGUIUtility.FindTexture(name);
                if (found != null) break;
            }
            Cache[category] = found;
            return found;
        }
    }
}
