using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeComponentNameViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeComponentNameViewHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.ComponentNameView;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            var text = HierarchyDrawUtils.BuildComponentNameText(go, 8);
            if (!string.IsNullOrEmpty(text))
            {
                HierarchyDrawUtils.DrawRightAlignedSmallText(selectionRect, ctx.FixedRightPosition, text, ctx.SmallLabelStyle);
            }
        }
    }
}
