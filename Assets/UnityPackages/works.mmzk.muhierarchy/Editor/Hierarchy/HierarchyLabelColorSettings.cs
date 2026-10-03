using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Customizes the colors of the Tag / Layer label bands in the Hierarchy.
    /// Tags / Layers not listed here keep their auto colors. The first asset found under Assets is used.
    /// </summary>
    [CreateAssetMenu(fileName = "LabelColorSettings", menuName = "muHierarchy/Label Color Settings")]
    public class HierarchyLabelColorSettings : ScriptableObject
    {
        [Serializable]
        public class TagColor
        {
            [TagSelector]
            public string tag = "Untagged";

            public Color color = Color.gray;

            [Tooltip("Use Text Color instead of the automatic black / white.")]
            public bool overrideTextColor;

            public Color textColor = Color.white;
        }

        [Serializable]
        public class LayerColor
        {
            [LayerSelector]
            public int layer;

            public Color color = Color.gray;

            [Tooltip("Use Text Color instead of the automatic black / white.")]
            public bool overrideTextColor;

            public Color textColor = Color.white;
        }

        [Header("Auto colors")]
        [Tooltip("Saturation of the auto colors.")]
        [Range(0f, 1f)]
        public float saturation = 0.5f;

        [Tooltip("Brightness of the auto colors.")]
        [Range(0f, 1f)]
        public float value = 0.6f;

        [Tooltip("Opacity of the auto colors.")]
        [Range(0f, 1f)]
        public float alpha = 1f;

        [Header("Overrides")]
        public List<TagColor> tags = new List<TagColor>();

        public List<LayerColor> layers = new List<LayerColor>();

        private void OnValidate()
        {
            HierarchyLabelColors.Invalidate();
            EditorApplication.RepaintHierarchyWindow();
        }
    }

    /// <summary>Shows a string field as the Tag dropdown.</summary>
    public sealed class TagSelectorAttribute : PropertyAttribute
    {
    }

    /// <summary>Shows an int field as the Layer dropdown.</summary>
    public sealed class LayerSelectorAttribute : PropertyAttribute
    {
    }

    [CustomPropertyDrawer(typeof(TagSelectorAttribute))]
    internal sealed class TagSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            using (var scope = new EditorGUI.PropertyScope(position, label, property))
            {
                EditorGUI.BeginChangeCheck();
                string tag = EditorGUI.TagField(position, scope.content, property.stringValue);
                if (EditorGUI.EndChangeCheck())
                    property.stringValue = tag;
            }
        }
    }

    [CustomPropertyDrawer(typeof(LayerSelectorAttribute))]
    internal sealed class LayerSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            using (var scope = new EditorGUI.PropertyScope(position, label, property))
            {
                EditorGUI.BeginChangeCheck();
                int layer = EditorGUI.LayerField(position, scope.content, property.intValue);
                if (EditorGUI.EndChangeCheck())
                    property.intValue = layer;
            }
        }
    }
}
