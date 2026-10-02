using UnityEngine;
using UnityEngine.Rendering;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// Shape の既定マテリアル。描画モードごとに 1 つを共有し、色は MaterialPropertyBlock で渡す。
    /// </summary>
    internal static class ShapeMaterials
    {
        public const string ShaderName = "Hidden/muPrimitive/Shape";

        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        private static readonly Material[] Cache = new Material[4];

        public static Material Get(bool transparent, bool alwaysOnTop)
        {
            var index = (transparent ? 1 : 0) | (alwaysOnTop ? 2 : 0);
            var material = Cache[index];
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[muPrimitive] Shader '{ShaderName}' not found.");
                return null;
            }

            material = new Material(shader)
            {
                name = $"muPrimitive ({(transparent ? "Transparent" : "Opaque")}{(alwaysOnTop ? ", AlwaysOnTop" : "")})",
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (transparent)
            {
                material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat(ZWriteId, 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat(SrcBlendId, (float)BlendMode.One);
                material.SetFloat(DstBlendId, (float)BlendMode.Zero);
                material.SetFloat(ZWriteId, 1f);
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            if (alwaysOnTop)
            {
                material.SetFloat(ZTestId, (float)CompareFunction.Always);
                material.renderQueue = (int)RenderQueue.Overlay;
            }
            else
            {
                material.SetFloat(ZTestId, (float)CompareFunction.LessEqual);
            }

            Cache[index] = material;
            return material;
        }
    }
}
