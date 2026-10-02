using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muProject.Editor
{
    /// <summary>
    /// Replaces folder icons in the Project window with the icon chosen by IFolderIconProvider implementations.
    /// Results are cached per GUID and cleared on any asset change.
    /// </summary>
    [InitializeOnLoad]
    public static class FolderIconDrawer
    {
        private const string MenuPath = "Tools/muProject/Custom Folder Icons";
        private const string EnabledKey = "muProject.CustomFolderIcons";

        // guid -> icon (null = keep the default folder icon)
        private static readonly Dictionary<string, Texture> IconCache = new Dictionary<string, Texture>();

        private static IFolderIconProvider[] _providers;
        private static Color? _defaultBackground;

        static FolderIconDrawer()
        {
            // Register after every other [InitializeOnLoad] handler, then move to the front of the list,
            // so overlays from other extensions (e.g. muValidation's ✗ mark) are drawn on top of the icon.
            EditorApplication.delayCall += () =>
            {
                EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
                EditorApplication.projectWindowItemOnGUI = OnProjectWindowItemGUI + EditorApplication.projectWindowItemOnGUI;
                Menu.SetChecked(MenuPath, Enabled);
                EditorApplication.RepaintProjectWindow();
            };
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set
            {
                EditorPrefs.SetBool(EnabledKey, value);
                Menu.SetChecked(MenuPath, value);
                EditorApplication.RepaintProjectWindow();
            }
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            Enabled = !Enabled;
        }

        /// <summary>
        /// Raised when cached icons are cleared. Providers that cache their own data should clear it here.
        /// </summary>
        public static event Action Invalidated;

        public static void Invalidate()
        {
            IconCache.Clear();
            Invalidated?.Invoke();
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint || !Enabled) return;

            var icon = GetIcon(guid);
            if (icon == null) return;

            var isListRow = rect.height <= 20f;
            var iconRect = GetIconRect(rect);

            // Cover the default folder icon. Grid view does not highlight the icon of selected items.
            EditorGUI.DrawRect(iconRect, isListRow ? GetRowBackground(guid) : DefaultBackground);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        }

        private static Texture GetIcon(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            if (IconCache.TryGetValue(guid, out var cached)) return cached;

            Texture icon = null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                foreach (var provider in Providers)
                {
                    try
                    {
                        icon = provider.GetIcon(path);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    if (icon != null) break;
                }
            }

            IconCache[guid] = icon;
            return icon;
        }

        private static IFolderIconProvider[] Providers
        {
            get
            {
                if (_providers != null) return _providers;

                var providers = new List<IFolderIconProvider>();
                foreach (var type in TypeCache.GetTypesDerivedFrom<IFolderIconProvider>())
                {
                    if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                    try
                    {
                        providers.Add((IFolderIconProvider)Activator.CreateInstance(type));
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                _providers = providers.OrderBy(p => p.Order).ToArray();
                return _providers;
            }
        }

        private static Rect GetIconRect(Rect rect)
        {
            // List view (one line)
            if (rect.height <= 20f) return new Rect(rect.x, rect.y, rect.height, rect.height);

            // Grid view: square icon above the label
            var size = Mathf.Min(rect.width, rect.height - 14f);
            return new Rect(rect.x + (rect.width - size) * 0.5f, rect.y, size, size);
        }

        private static Color GetRowBackground(string guid)
        {
            if (Array.IndexOf(Selection.assetGUIDs, guid) < 0) return DefaultBackground;

            var focused = EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType().Name == "ProjectBrowser";
            if (EditorGUIUtility.isProSkin)
            {
                return focused ? new Color32(44, 93, 135, 255) : new Color32(77, 77, 77, 255);
            }

            return focused ? new Color32(58, 114, 176, 255) : new Color32(174, 174, 174, 255);
        }

        private static Color DefaultBackground
        {
            get
            {
                if (_defaultBackground.HasValue) return _defaultBackground.Value;

                // EditorGUIUtility.GetDefaultBackgroundColor is internal; fall back to the known skin colors.
                var method = typeof(EditorGUIUtility).GetMethod("GetDefaultBackgroundColor", BindingFlags.Static | BindingFlags.NonPublic);
                _defaultBackground = method != null && method.GetParameters().Length == 0
                    ? (Color)method.Invoke(null, null)
                    : EditorGUIUtility.isProSkin
                        ? new Color32(56, 56, 56, 255)
                        : new Color32(200, 200, 200, 255);
                return _defaultBackground.Value;
            }
        }

        private class Postprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                Invalidate();
            }
        }
    }
}
