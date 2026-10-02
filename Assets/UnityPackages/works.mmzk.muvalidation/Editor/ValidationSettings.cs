using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Project-wide muValidation settings, stored in ProjectSettings/ so they are shared through version control.
    /// Edit them in Project Settings > muValidation.
    /// </summary>
    [FilePath("ProjectSettings/muValidationSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ValidationSettings : ScriptableSingleton<ValidationSettings>
    {
        [Tooltip("Run validation before every player build.")]
        public bool validateBeforeBuild = true;

        [Tooltip("Validate all assets under Assets (FileNameRules and validation attributes).")]
        public bool includeAssets = true;

        [Tooltip("Validate the enabled scenes in Build Settings. Scenes that are not open are opened temporarily.")]
        public bool includeBuildScenes = true;

        [Tooltip("Stop the build when there are errors.")]
        public bool failOnErrors = true;

        [Tooltip("Stop the build when there are warnings.")]
        public bool failOnWarnings;

        public void Save() => Save(true);

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/muValidation", SettingsScope.Project)
            {
                label = "muValidation",
                keywords = new[] { "validation", "build", "muValidation" },
                guiHandler = _ =>
                {
                    var settings = instance;
                    // ScriptableSingleton uses DontSave flags; make it editable for the SerializedObject.
                    settings.hideFlags &= ~HideFlags.NotEditable;
                    var serialized = new SerializedObject(settings);

                    EditorGUILayout.LabelField("Before Build", EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(validateBeforeBuild)));
                    using (new EditorGUI.DisabledScope(!settings.validateBeforeBuild))
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serialized.FindProperty(nameof(includeAssets)));
                        EditorGUILayout.PropertyField(serialized.FindProperty(nameof(includeBuildScenes)));
                        EditorGUILayout.PropertyField(serialized.FindProperty(nameof(failOnErrors)));
                        EditorGUILayout.PropertyField(serialized.FindProperty(nameof(failOnWarnings)));
                        EditorGUI.indentLevel--;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        settings.Save();
                    }

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Run Build Check Now", GUILayout.Width(180))) ValidationBuildCheck.RunFromMenu();
                },
            };
        }
    }
}
