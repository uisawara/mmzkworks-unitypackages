using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muValidation.Editor
{
    [CustomEditor(typeof(SceneRules))]
    public class SceneRulesEditor : UnityEditor.Editor
    {
        private List<KeyValuePair<GameObject, string>> _violations;
        private Vector2 _scroll;

        private void OnEnable()
        {
            _violations = null;
        }

        public override void OnInspectorGUI()
        {
            var rules = (SceneRules)target;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) _violations = null;

            if (GUILayout.Button("Reset Default Names", GUILayout.Width(160)))
            {
                Undo.RecordObject(rules, "Reset Default Names");
                rules.ResetDefaultNames();
                EditorUtility.SetDirty(rules);
                SceneRulesRegistry.Invalidate();
                _violations = null;
            }

            DrawUnresolvedTypes(rules);
            DrawAssignedPaths(rules);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Violations In Open Scenes", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(70))) _violations = null;
            }

            _violations ??= CollectViolations(rules);

            if (_violations.Count == 0)
            {
                EditorGUILayout.HelpBox("All objects in open scenes follow these rules.", MessageType.Info);
                return;
            }

            var type = rules.severity == ValidationSeverity.Warning ? MessageType.Warning : MessageType.Error;
            EditorGUILayout.HelpBox($"{_violations.Count} object(s) violate these rules.", type);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(300));
            foreach (var violation in _violations)
            {
                if (violation.Key == null) continue;
                var message = violation.Value.Replace("\n", ", ");
                var content = new GUIContent($"{violation.Key.scene.name}: {AssetValidation.GetPath(violation.Key.transform, null)}", message);
                if (GUILayout.Button(content, EditorStyles.linkLabel)) EditorGUIUtility.PingObject(violation.Key);
                EditorGUILayout.LabelField(message, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawAssignedPaths(SceneRules rules)
        {
            var paths = SceneRulesRegistry.GetAssignedPaths(rules);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Assigned To", EditorStyles.boldLabel);
            if (paths.Count == 0)
            {
                EditorGUILayout.HelpBox("Not assigned to any path in a SceneRulesAssignments asset, so these rules are not applied.", MessageType.Warning);
                return;
            }

            foreach (var path in paths) EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
        }

        private static void DrawUnresolvedTypes(SceneRules rules)
        {
            if (rules.componentLayers == null) return;

            for (var i = 0; i < rules.componentLayers.Length; i++)
            {
                var rule = rules.componentLayers[i];
                if (rule == null || SceneRules.ResolveComponentType(rule.componentType) != null) continue;
                EditorGUILayout.HelpBox($"Component Layers {i}: component type \"{rule.componentType}\" not found.", MessageType.Warning);
            }
        }

        private static List<KeyValuePair<GameObject, string>> CollectViolations(SceneRules rules)
        {
            var roots = new List<GameObject>();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null && SceneRulesRegistry.GetRulesFor(stage.assetPath).Contains(rules))
            {
                roots.Add(stage.prefabContentsRoot);
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && SceneRulesRegistry.GetRulesFor(scene.path).Contains(rules)) roots.AddRange(scene.GetRootGameObjects());
            }

            SceneNameIndex.Clear();
            var result = new List<KeyValuePair<GameObject, string>>();
            foreach (var root in roots)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var validation = new ValidationResult();
                    rules.Validate(transform.gameObject, validation);
                    var summary = ValidationSummary.From(validation);
                    if (!summary.IsValid) result.Add(new KeyValuePair<GameObject, string>(transform.gameObject, summary.Message));
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Draws a string field (or each element of a string array) as a tag dropdown.
    /// </summary>
    internal class TagFieldAttribute : PropertyAttribute
    {
    }

    [CustomPropertyDrawer(typeof(TagFieldAttribute))]
    internal class TagFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.TagField(position, label, property.stringValue);
            if (EditorGUI.EndChangeCheck()) property.stringValue = value;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(SceneRules.TagLayerRule))]
    internal class TagLayerRuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var tag = property.FindPropertyRelative(nameof(SceneRules.TagLayerRule.tag));

            EditorGUI.BeginProperty(line, label, tag);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.TagField(line, "Tag", tag.stringValue);
            if (EditorGUI.EndChangeCheck()) tag.stringValue = value;
            EditorGUI.EndProperty();

            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, property.FindPropertyRelative(nameof(SceneRules.TagLayerRule.allowedLayers)));
        }
    }

    [CustomPropertyDrawer(typeof(SceneRules.ComponentLayerRule))]
    internal class ComponentLayerRuleDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 60f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var typeProperty = property.FindPropertyRelative(nameof(SceneRules.ComponentLayerRule.componentType));

            var fieldRect = new Rect(line.x, line.y, line.width - ButtonWidth - 2, line.height);
            var buttonRect = new Rect(fieldRect.xMax + 2, line.y, ButtonWidth, line.height);
            EditorGUI.PropertyField(fieldRect, typeProperty, new GUIContent("Component"));
            if (GUI.Button(buttonRect, "Select", EditorStyles.miniButton))
            {
                var serializedObject = property.serializedObject;
                var path = typeProperty.propertyPath;
                new ComponentTypeDropdown(new AdvancedDropdownState(), fullName =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty(path).stringValue = fullName;
                    serializedObject.ApplyModifiedProperties();
                }).Show(buttonRect);
            }

            line.y += step;
            EditorGUI.PropertyField(line, property.FindPropertyRelative(nameof(SceneRules.ComponentLayerRule.includeSubclasses)));
            line.y += step;
            EditorGUI.PropertyField(line, property.FindPropertyRelative(nameof(SceneRules.ComponentLayerRule.allowedLayers)));
        }
    }

    /// <summary>
    /// Searchable list of component types, grouped by namespace.
    /// </summary>
    internal class ComponentTypeDropdown : AdvancedDropdown
    {
        private class TypeItem : AdvancedDropdownItem
        {
            public readonly string FullName;

            public TypeItem(Type type) : base(type.Name)
            {
                FullName = type.FullName;
            }
        }

        private readonly Action<string> _onSelected;

        public ComponentTypeDropdown(AdvancedDropdownState state, Action<string> onSelected) : base(state)
        {
            _onSelected = onSelected;
            minimumSize = new Vector2(300, 400);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Component");
            var groups = SceneRules.AllComponentTypes()
                .Where(type => type.FullName != null)
                .GroupBy(type => string.IsNullOrEmpty(type.Namespace) ? "(Global)" : type.Namespace)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var folder = new AdvancedDropdownItem(group.Key);
                foreach (var type in group.OrderBy(type => type.Name, StringComparer.Ordinal)) folder.AddChild(new TypeItem(type));
                root.AddChild(folder);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeItem typeItem) _onSelected(typeItem.FullName);
        }
    }
}
