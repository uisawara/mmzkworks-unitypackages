using UnityEngine;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// ローカル +Y 方向に伸びるコーンを描画する。
    /// </summary>
    [AddComponentMenu("muPrimitive/Cone Shape")]
    public sealed class ConeShape : ShapeBase
    {
        [Header("Shape")]
        [SerializeField, Min(0f), Tooltip("底面の半径")]
        private float _radius = 0.5f;

        [SerializeField, Tooltip("高さ（ローカル +Y 方向）")]
        private float _height = 1f;

        [SerializeField, Tooltip("オフで底面が原点、オンで頂点が原点（視界・スポットライトの範囲表示向け）")]
        private bool _apexAtOrigin;

        [SerializeField, Tooltip("底面を塞ぐ")]
        private bool _capped = true;

        [SerializeField, Range(3, 256), Tooltip("一周あたりの分割数")]
        private int _segments = 32;

        public float Radius
        {
            get => _radius;
            set { _radius = Mathf.Max(0f, value); MarkMeshDirty(); }
        }

        public float Height
        {
            get => _height;
            set { _height = value; MarkMeshDirty(); }
        }

        public bool ApexAtOrigin
        {
            get => _apexAtOrigin;
            set { _apexAtOrigin = value; MarkMeshDirty(); }
        }

        public bool Capped
        {
            get => _capped;
            set { _capped = value; MarkMeshDirty(); }
        }

        public int Segments
        {
            get => _segments;
            set { _segments = Mathf.Clamp(value, 3, 256); MarkMeshDirty(); }
        }

        protected override void BuildMesh(Mesh mesh)
        {
            ShapeMeshBuilder.BuildCone(mesh, _radius, _height, _apexAtOrigin, _segments, _capped);
        }
    }
}
