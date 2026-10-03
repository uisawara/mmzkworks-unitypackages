using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muSceneManager.Editor
{
    /// <summary>
    /// Draws SceneId as a SceneAsset picker and stores the scene name.
    /// Shows a warning when the scene cannot be found or is not enabled in Build Settings.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneId))]
    public class SceneIdDrawer : PropertyDrawer
    {
        private const string NameField = "_name";

        // Scene name -> asset path (null when not found). Cleared when the project changes.
        private static readonly Dictionary<string, string> PathCache = new();

        static SceneIdDrawer()
        {
            EditorApplication.projectChanged += PathCache.Clear;
            EditorBuildSettings.sceneListChanged += PathCache.Clear;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            return GetWarning(property.FindPropertyRelative(NameField)) == null
                ? lineHeight
                : lineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative(NameField);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                var sceneName = nameProperty.hasMultipleDifferentValues ? null : nameProperty.stringValue;
                var path = FindScenePath(sceneName);
                var current = path == null ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

                EditorGUI.showMixedValue = nameProperty.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var selected = (SceneAsset)EditorGUI.ObjectField(fieldRect, label, current, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    nameProperty.stringValue = selected == null ? string.Empty : selected.name;
                }
                EditorGUI.showMixedValue = false;

                var warning = GetWarning(nameProperty);
                if (warning != null)
                {
                    var warningRect = new Rect(
                        position.x,
                        fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                        position.width,
                        EditorGUIUtility.singleLineHeight);
                    warningRect = EditorGUI.IndentedRect(warningRect);
                    EditorGUI.LabelField(warningRect, EditorGUIUtility.TrTextContentWithIcon(warning, MessageType.Warning));
                }
            }
        }

        private static string GetWarning(SerializedProperty nameProperty)
        {
            if (nameProperty.hasMultipleDifferentValues || string.IsNullOrEmpty(nameProperty.stringValue))
            {
                return null;
            }

            var sceneName = nameProperty.stringValue;
            var path = FindScenePath(sceneName);
            if (path == null)
            {
                return $"Scene '{sceneName}' not found";
            }

            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path))
            {
                return $"Scene '{sceneName}' is not enabled in Build Settings";
            }

            return null;
        }

        private static string FindScenePath(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            if (PathCache.TryGetValue(sceneName, out var cached))
            {
                return cached;
            }

            var candidates = AssetDatabase.FindAssets($"t:SceneAsset {sceneName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == sceneName)
                .ToList();

            // Prefer the scene registered in Build Settings, since that is the one loaded by name at runtime
            var buildScenePaths = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
            var path = candidates.FirstOrDefault(buildScenePaths.Contains) ?? candidates.FirstOrDefault();

            PathCache[sceneName] = path;
            return path;
        }
    }
}
