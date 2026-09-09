using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeNoneHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeNoneHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.None;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            // No extra display
        }
    }
}
