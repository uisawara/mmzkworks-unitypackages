using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muDashboard.Editor
{
    /// <summary>
    /// Icon per command category. Built-in icon names differ between Unity versions, so a few names are tried first,
    /// then the icon Unity shows for a matching asset type, which always exists.
    /// </summary>
    internal static class ShortcutCommandIcons
    {
        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { ShortcutCommandCategory.Prefab, new[] { "Prefab Icon", "PrefabModel Icon" } },
            { ShortcutCommandCategory.GameObject, new[] { "GameObject Icon", "GameObject On Icon" } },
            { ShortcutCommandCategory.Scene, new[] { "SceneAsset Icon" } },
            { ShortcutCommandCategory.Asset, new[] { "Folder Icon", "FolderEmpty Icon" } },
        };

        private static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();
        private static bool _cacheIsPro;

        public static Texture For(ShortcutCommand command) => command == null ? null : ForCategory(command.Category);

        public static Texture ForCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return null;

            var pro = EditorGUIUtility.isProSkin;
            if (_cacheIsPro != pro)
            {
                Cache.Clear();
                _cacheIsPro = pro;
            }
            if (Cache.TryGetValue(category, out var cached) && cached != null) return cached;

            var found = ByName(category, pro) ?? ByType(category);
            Cache[category] = found;
            return found;
        }

        private static Texture ByName(string category, bool pro)
        {
            if (!Names.TryGetValue(category, out var names)) return null;
            foreach (var name in names)
            {
                // FindTexture returns null quietly for unknown names (IconContent would log an error).
                Texture t = null;
                if (pro) t = EditorGUIUtility.FindTexture("d_" + name);
                if (t == null) t = EditorGUIUtility.FindTexture(name);
                if (t != null) return t;
            }
            return null;
        }

        private static Texture ByType(string category)
        {
            switch (category)
            {
                case ShortcutCommandCategory.Asset:
                    // The Assets folder always exists, so its cached icon is the folder icon.
                    return AssetDatabase.GetCachedIcon("Assets");
                case ShortcutCommandCategory.Scene:
                    return TypeIcon(typeof(SceneAsset));
                default:
                    return TypeIcon(typeof(GameObject));
            }
        }

        private static Texture TypeIcon(Type type) => EditorGUIUtility.ObjectContent(null, type)?.image;
    }
}
