using UnityEngine;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// パイ型（扇形のリングに上底・下底を持たせた立体）を描画する。
    /// 扇はローカル +Z を中心に左右へ Angle/2 ずつ開く。
    /// </summary>
    [AddComponentMenu("muPrimitive/Pie Shape")]
    public sealed class PieShape : ShapeBase
    {
        [Header("Shape")]
        [SerializeField, Min(0f), Tooltip("内周の半径。0 で中心まで埋まった扇形")]
        private float _innerRadius;

        [SerializeField, Min(0f), Tooltip("外周の半径")]
        private float _outerRadius = 1f;

        [SerializeField, Range(0f, 360f), Tooltip("扇の開き角（度）")]
        private float _angle = 90f;

        [SerializeField, Tooltip("下底のローカル Y 座標")]
        private float _bottom;

        [SerializeField, Tooltip("上底のローカル Y 座標。下底と同じなら厚みのない面になる")]
        private float _top = 0.1f;

        [SerializeField, Range(3, 256), Tooltip("一周あたりの分割数")]
        private int _segments = 64;

        public float InnerRadius
        {
            get => _innerRadius;
            set { _innerRadius = Mathf.Max(0f, value); MarkMeshDirty(); }
        }

        public float OuterRadius
        {
            get => _outerRadius;
            set { _outerRadius = Mathf.Max(0f, value); MarkMeshDirty(); }
        }

        public float Angle
        {
            get => _angle;
            set { _angle = Mathf.Clamp(value, 0f, 360f); MarkMeshDirty(); }
        }

        public float Bottom
        {
            get => _bottom;
            set { _bottom = value; MarkMeshDirty(); }
        }

        public float Top
        {
            get => _top;
            set { _top = value; MarkMeshDirty(); }
        }

        public int Segments
        {
            get => _segments;
            set { _segments = Mathf.Clamp(value, 3, 256); MarkMeshDirty(); }
        }

        protected override void BuildMesh(Mesh mesh)
        {
            ShapeMeshBuilder.BuildPie(mesh, _innerRadius, _outerRadius, _angle, _bottom, _top, _segments);
        }
    }
}
