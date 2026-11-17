using UnityEngine;
using UnityEditor;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Mmzkworks.muHierarchy.Editor
{
    public interface IHierarchyViewModeHandler
    {
        HierarchyViewMode ViewMode { get; }

        bool RecordPosition { get; }

        void Draw(ObjectId instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx);
    }
}
