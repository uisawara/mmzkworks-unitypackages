using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public interface IHierarchyViewModeHandler
    {
        HierarchyViewMode ViewMode { get; }

        bool RecordPosition { get; }

        void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx);
    }
}
