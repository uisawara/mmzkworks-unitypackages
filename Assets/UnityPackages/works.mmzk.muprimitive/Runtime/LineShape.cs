using UnityEngine;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// GameObject の位置から目標座標までを円柱で結んで描画する。
    /// 目標や自身が動くと毎フレーム追従する。
    /// </summary>
    [AddComponentMenu("muPrimitive/Line Shape")]
    public sealed class LineShape : ShapeBase
    {
        private const float MoveThreshold = 1e-5f;

        [Header("Shape")]
        [SerializeField, Tooltip("目標の Transform。未指定なら Target Position を使う")]
        private Transform _target;

        [SerializeField, Tooltip("目標のワールド座標。Target が未指定のときに使う")]
        private Vector3 _targetPosition = Vector3.forward;

        [SerializeField, Min(0f), Tooltip("円柱の半径（ローカルスケールの影響を受ける）")]
        private float _radius = 0.05f;

        [SerializeField, Tooltip("両端を塞ぐ")]
        private bool _capped = true;

        [SerializeField, Range(3, 256), Tooltip("一周あたりの分割数")]
        private int _segments = 16;

        private Vector3 _localEnd;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>目標のワールド座標。Target が指定されていればその位置を返す。</summary>
        public Vector3 TargetPosition
        {
            get => _target != null ? _target.position : _targetPosition;
            set => _targetPosition = value;
        }

        public float Radius
        {
            get => _radius;
            set { _radius = Mathf.Max(0f, value); MarkMeshDirty(); }
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

        protected override bool NeedsRebuild()
        {
            var localEnd = transform.InverseTransformPoint(TargetPosition);
            if ((localEnd - _localEnd).sqrMagnitude < MoveThreshold * MoveThreshold)
            {
                return false;
            }

            _localEnd = localEnd;
            return true;
        }

        protected override void BuildMesh(Mesh mesh)
        {
            ShapeMeshBuilder.BuildCylinder(mesh, Vector3.zero, _localEnd, _radius, _segments, _capped);
        }
    }
}
