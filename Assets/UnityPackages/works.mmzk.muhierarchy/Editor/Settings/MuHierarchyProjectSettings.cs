using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Project-wide muHierarchy settings, stored in ProjectSettings so they can be committed and shared.
    /// </summary>
    [FilePath("ProjectSettings/muHierarchySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class MuHierarchyProjectSettings : ScriptableSingleton<MuHierarchyProjectSettings>
    {
        // Space (px) kept free at the right end of each Hierarchy row; muHierarchy draws left of it.
        [SerializeField] private float rightMargin = 0f;

        public float RightMargin
        {
            get => Mathf.Max(0f, rightMargin);
            set
            {
                float v = Mathf.Max(0f, value);
                if (Mathf.Approximately(v, rightMargin))
                    return;
                rightMargin = v;
                Save(true);
                EditorApplication.RepaintHierarchyWindow();
            }
        }
    }
}
