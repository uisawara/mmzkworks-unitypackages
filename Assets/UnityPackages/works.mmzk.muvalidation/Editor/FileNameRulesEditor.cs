using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    [CustomEditor(typeof(FileNameRules))]
    public class FileNameRulesEditor : UnityEditor.Editor
    {
        private List<KeyValuePair<string, string>> _violations;
        private Vector2 _scroll;

        private void OnEnable()
        {
            _violations = null;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) _violations = null;

            DrawResolvedFolders((FileNameRules)target);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Violations", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    FileNameRulesRegistry.Invalidate();
                    _violations = null;
                }
            }

            _violations ??= FileNameRulesRegistry.CollectViolations((FileNameRules)target);

            if (_violations.Count == 0)
            {
                EditorGUILayout.HelpBox("All files follow this rule.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"{_violations.Count} file(s) violate this rule.", MessageType.Error);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(300));
            foreach (var violation in _violations)
            {
                var content = new GUIContent(violation.Key, violation.Value.Replace("\n", ", "));
                if (GUILayout.Button(content, EditorStyles.linkLabel))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(violation.Key);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }

                EditorGUILayout.LabelField(violation.Value.Replace("\n", ", "), EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawResolvedFolders(FileNameRules asset)
        {
            if (asset.rules == null || asset.rules.Length == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rule Folders", EditorStyles.boldLabel);
            for (var i = 0; i < asset.rules.Length; i++)
            {
                if (asset.rules[i] == null) continue;
                var folder = asset.ResolveFolder(asset.rules[i]);
                if (folder == null)
                {
                    EditorGUILayout.HelpBox($"Rule {i}: path points outside the project.", MessageType.Warning);
                }
                else if (!AssetDatabase.IsValidFolder(folder))
                {
                    EditorGUILayout.HelpBox($"Rule {i}: {folder} does not exist.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField($"Rule {i}", folder, EditorStyles.miniLabel);
                }
            }
        }
    }
}
