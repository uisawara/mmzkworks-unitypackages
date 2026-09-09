using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeMeshInfoViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeMeshInfoViewHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.MeshInfoView;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            var text = GetMeshInfoText(go);
            if (!string.IsNullOrEmpty(text))
            {
                HierarchyDrawUtils.DrawRightAlignedSmallText(selectionRect, ctx.FixedRightPosition, text, ctx.SmallLabelStyle);
            }
        }

        private static string GetMeshInfoText(GameObject go)
        {
            if (go == null)
                return string.Empty;

            int totalTri = 0;
            int totalVert = 0;

            var meshFilters = go.GetComponents<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                if (mf == null)
                    continue;
                var mesh = mf.sharedMesh;
                if (mesh == null)
                    continue;
                totalTri += mesh.triangles.Length / 3;
                totalVert += mesh.vertexCount;
            }

            var skinnedMeshRenderers = go.GetComponents<SkinnedMeshRenderer>();
            foreach (var smr in skinnedMeshRenderers)
            {
                if (smr == null)
                    continue;
                var mesh = smr.sharedMesh;
                if (mesh == null)
                    continue;
                totalTri += mesh.triangles.Length / 3;
                totalVert += mesh.vertexCount;
            }

            if (totalTri == 0 && totalVert == 0)
                return string.Empty;

            return $"{totalTri} tri, {totalVert} vert";
        }
    }
}
