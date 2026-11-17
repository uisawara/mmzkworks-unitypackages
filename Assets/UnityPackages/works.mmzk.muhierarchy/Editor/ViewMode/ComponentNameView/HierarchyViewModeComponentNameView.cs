using UnityEngine;
using UnityEditor;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeComponentNameViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeComponentNameViewHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.ComponentNameView;
        public bool RecordPosition => false;

        public void Draw(ObjectId instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            var text = HierarchyDrawUtils.BuildComponentNameText(go, 8);
            if (!string.IsNullOrEmpty(text))
            {
                HierarchyDrawUtils.DrawRightAlignedSmallText(selectionRect, ctx.FixedRightPosition, text, ctx.SmallLabelStyle);
            }
        }
    }
}
