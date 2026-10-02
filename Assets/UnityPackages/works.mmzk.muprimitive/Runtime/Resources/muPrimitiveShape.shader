// muPrimitive の各 Shape が使う既定シェーダ。
// シーンのライトに依存せず形が読み取れるよう、カメラ基準の簡易シェーディングを行う。
// LightMode タグを持たないパスなので Built-in / URP の両方で描画される。
Shader "Hidden/muPrimitive/Shape"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Shading ("Shading", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewNormal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            half _Shading;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                // 裏面も描画するので、裏から見えている面は法線を反転する
                float3 n = normalize(i.viewNormal) * (facing > 0 ? 1 : -1);
                // ビュー空間で左上手前からのライト
                float3 l = normalize(float3(-0.3, 0.5, 1.0));
                half lit = saturate(dot(n, l)) * 0.5 + 0.5;
                fixed4 c = _Color;
                c.rgb *= lerp(1, lit, _Shading);
                return c;
            }
            ENDCG
        }
    }
}
