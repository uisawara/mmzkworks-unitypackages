using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Project Settings > muHierarchy page.
    /// </summary>
    internal static class MuHierarchySettingsProvider
    {
        private const string SettingsPath = "Project/muHierarchy";

        private static readonly GUIContent RightMarginLabel = new GUIContent(
            "Right Margin (px)",
            "Space kept free at the right end of each Hierarchy row. Everything muHierarchy draws on the right moves left by this amount.");

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "muHierarchy",
                guiHandler = _ => DrawGUI(),
                keywords = new HashSet<string>(new[] { "muHierarchy", "Hierarchy", "Right", "Margin" })
            };
        }

        [MenuItem("Tools/muHierarchy/Settings...", false, 2100)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        private static void DrawGUI()
        {
            var settings = MuHierarchyProjectSettings.instance;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                float margin = EditorGUILayout.FloatField(RightMarginLabel, settings.RightMargin);
                if (EditorGUI.EndChangeCheck())
                    settings.RightMargin = margin;

                if (GUILayout.Button("Reset", GUILayout.Width(60f)))
                {
                    settings.RightMargin = 0f;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.HelpBox(
                "Saved in ProjectSettings/muHierarchySettings.asset. Commit it to share the setting with your team.",
                MessageType.Info);
        }
    }
}
