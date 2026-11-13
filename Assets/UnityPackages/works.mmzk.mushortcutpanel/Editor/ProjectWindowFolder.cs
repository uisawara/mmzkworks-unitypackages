using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmzkworks.muShortcutPanel.Editor
{
    /// <summary>
    /// Opens a folder in the Project window the way double-clicking it does: in two-column layout the right column then
    /// lists the folder's contents (selecting it would only highlight it inside its parent).
    /// Uses the internal <c>ProjectBrowser.ShowFolderContents</c>; callers fall back to a plain selection when this returns false.
    /// </summary>
    internal static class ProjectWindowFolder
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _initialized;
        private static FieldInfo _lastInteracted;
        private static MethodInfo _getAll;
        private static MethodInfo _isTwoColumns;
        private static MethodInfo _showFolderContents;

        public static bool TryShowContents(Object folder)
        {
            if (folder == null) return false;
            try
            {
                Init();
                if (_showFolderContents == null || _isTwoColumns == null) return false;

                var browser = Browser();
                if (browser == null || !(bool)_isTwoColumns.Invoke(browser, null)) return false;

                var id = FolderId(folder, _showFolderContents.GetParameters()[0].ParameterType);
                if (id == null) return false;

                _showFolderContents.Invoke(browser, new[] { id, (object)true });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[muShortcutPanel] Could not open the folder in the Project window: {e.GetBaseException().Message}");
                return false;
            }
        }

        private static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (type == null) return;

            _lastInteracted = type.GetField("s_LastInteractedProjectBrowser", AnyStatic);
            _getAll = type.GetMethod("GetAllProjectBrowsers", AnyStatic, null, Type.EmptyTypes, null);
            _isTwoColumns = type.GetMethod("IsTwoColumns", AnyInstance, null, Type.EmptyTypes, null);
            foreach (var m in type.GetMethods(AnyInstance))
            {
                if (m.Name != "ShowFolderContents") continue;
                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[1].ParameterType == typeof(bool))
                {
                    _showFolderContents = m;
                    break;
                }
            }
        }

        /// <summary>The Project window used last, or any open one.</summary>
        private static object Browser()
        {
            var last = _lastInteracted?.GetValue(null);
            if (last is Object o && o != null) return last;

            if (_getAll?.Invoke(null, null) is IList all)
            {
                foreach (var b in all)
                {
                    if (b is Object ob && ob != null) return b;
                }
            }
            return null;
        }

        /// <summary>The folder's id in the type the running Unity version expects (EntityId on Unity 6.3+, int before).</summary>
        private static object FolderId(Object folder, Type idType)
        {
#if UNITY_6000_3_OR_NEWER
            if (idType == typeof(EntityId)) return folder.GetEntityId();
#else
            if (idType == typeof(int)) return folder.GetInstanceID();
#endif
            return null;
        }
    }
}
