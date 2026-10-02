using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    [CustomEditor(typeof(SceneRulesAssignments))]
    public class SceneRulesAssignmentsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var assignments = ((SceneRulesAssignments)target).assignments;
            if (assignments == null || assignments.Length == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Covered Scenes", EditorStyles.boldLabel);
            for (var i = 0; i < assignments.Length; i++)
            {
                if (assignments[i] == null) continue;
                var path = SceneRulesAssignments.NormalizePath(assignments[i].path);

                if (AssetDatabase.IsValidFolder(path))
                {
                    var count = AssetDatabase.FindAssets("t:Scene", new[] { path }).Length;
                    EditorGUILayout.LabelField($"Assignment {i}", $"{path} ({count} scene(s))", EditorStyles.miniLabel);
                }
                else if (File.Exists(path))
                {
                    EditorGUILayout.LabelField($"Assignment {i}", path, EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Assignment {i}: {path} does not exist.", MessageType.Warning);
                }
            }
        }
    }
}
