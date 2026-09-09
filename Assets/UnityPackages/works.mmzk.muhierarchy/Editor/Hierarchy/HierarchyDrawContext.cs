using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public struct HierarchyDrawContext
    {
        public int InstanceId { get; set; }
        public Rect SelectionRect { get; set; }
        public GameObject GameObject { get; set; }
        public float FixedRightPosition { get; set; }
        public GUIStyle SmallLabelStyle { get; set; }
        public float IconSize { get; set; }
        public float Spacing { get; set; }
        public float ComponentIconSpacing { get; set; }
        public float TextSpacing { get; set; }
        public float FixedRightMargin { get; set; }
        public bool ShowLayerName { get; set; }
        public bool ShowTagName { get; set; }
        public bool ShowPrefabIcon { get; set; }
        public bool ShowComponentIcons { get; set; }
        public bool DedupeComponentIcons { get; set; }
        public bool ShowReferenceLines { get; set; }
        public Texture2D IconError { get; set; }
        public Texture2D IconPrefab { get; set; }
        public Texture2D IconPrefabApplyWarning { get; set; }
        public Texture2D IconPrefabEmpty { get; set; }
    }
}
