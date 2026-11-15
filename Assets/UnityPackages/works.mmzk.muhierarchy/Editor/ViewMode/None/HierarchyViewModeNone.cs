using UnityEngine;
using UnityEditor;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeNoneHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeNoneHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.None;
        public bool RecordPosition => false;

        public void Draw(ObjectId instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            // No extra display
        }
    }
}
