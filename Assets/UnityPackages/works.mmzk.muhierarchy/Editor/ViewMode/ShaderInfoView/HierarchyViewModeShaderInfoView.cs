using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    public sealed class HierarchyViewModeShaderInfoViewHandler : IHierarchyViewModeHandler
    {
        public static readonly IHierarchyViewModeHandler Instance = new HierarchyViewModeShaderInfoViewHandler();

        public HierarchyViewMode ViewMode => HierarchyViewMode.ShaderInfoView;
        public bool RecordPosition => false;

        public void Draw(int instanceID, Rect selectionRect, GameObject go, HierarchyDrawContext ctx)
        {
            var text = GetShaderInfoText(go);
            if (!string.IsNullOrEmpty(text))
            {
                HierarchyDrawUtils.DrawRightAlignedSmallText(selectionRect, ctx.FixedRightPosition, text, ctx.SmallLabelStyle);
            }
        }

        private static string GetShaderInfoText(GameObject go)
        {
            if (go == null)
                return string.Empty;

            var shaderNames = new HashSet<string>();

            var meshRenderers = go.GetComponents<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                if (mr == null)
                    continue;
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && !string.IsNullOrEmpty(mat.shader.name))
                        shaderNames.Add(mat.shader.name);
                }
            }

            var skinnedMeshRenderers = go.GetComponents<SkinnedMeshRenderer>();
            foreach (var smr in skinnedMeshRenderers)
            {
                if (smr == null)
                    continue;
                foreach (var mat in smr.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && !string.IsNullOrEmpty(mat.shader.name))
                        shaderNames.Add(mat.shader.name);
                }
            }

            var lineRenderers = go.GetComponents<LineRenderer>();
            foreach (var lr in lineRenderers)
            {
                if (lr == null)
                    continue;
                foreach (var mat in lr.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && !string.IsNullOrEmpty(mat.shader.name))
                        shaderNames.Add(mat.shader.name);
                }
            }

            var trailRenderers = go.GetComponents<TrailRenderer>();
            foreach (var tr in trailRenderers)
            {
                if (tr == null)
                    continue;
                foreach (var mat in tr.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && !string.IsNullOrEmpty(mat.shader.name))
                        shaderNames.Add(mat.shader.name);
                }
            }

            var particleSystemRenderers = go.GetComponents<ParticleSystemRenderer>();
            foreach (var psr in particleSystemRenderers)
            {
                if (psr == null)
                    continue;
                foreach (var mat in psr.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && !string.IsNullOrEmpty(mat.shader.name))
                        shaderNames.Add(mat.shader.name);
                }
            }

            if (shaderNames.Count == 0)
                return string.Empty;

            return string.Join(", ", shaderNames);
        }
    }
}
