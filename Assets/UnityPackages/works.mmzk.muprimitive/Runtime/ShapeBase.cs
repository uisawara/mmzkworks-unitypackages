using UnityEngine;
using UnityEngine.Rendering;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// メッシュを生成して同じ GameObject の MeshRenderer で描画する Shape の基底クラス。
    /// 生成したメッシュはシーンに保存されず、有効化時に作り直される。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class ShapeBase : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadingId = Shader.PropertyToID("_Shading");

        [Header("Appearance")]
        [SerializeField, Tooltip("アルファが 1 未満なら半透明で描画する")]
        private Color _color = new(0.2f, 0.8f, 1f, 0.5f);

        [SerializeField, Range(0f, 1f), Tooltip("カメラ基準の陰影の強さ。0 で単色")]
        private float _shading = 0.5f;

        [SerializeField, Tooltip("他のオブジェクトに隠れず常に手前に描画する")]
        private bool _alwaysOnTop;

        [SerializeField, Tooltip("指定すると既定マテリアルの代わりに使う。色は _Color / _BaseColor に渡される")]
        private Material _customMaterial;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private bool _meshDirty = true;
        private bool _appearanceDirty = true;

        public Color Color
        {
            get => _color;
            set { _color = value; _appearanceDirty = true; }
        }

        public float Shading
        {
            get => _shading;
            set { _shading = Mathf.Clamp01(value); _appearanceDirty = true; }
        }

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set { _alwaysOnTop = value; _appearanceDirty = true; }
        }

        public Material CustomMaterial
        {
            get => _customMaterial;
            set { _customMaterial = value; _appearanceDirty = true; }
        }

        /// <summary>生成済みのメッシュ。無効化中は null の場合がある。</summary>
        public Mesh Mesh => _mesh;

        protected virtual void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_mesh == null)
            {
                _mesh = new Mesh { name = GetType().Name, hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }

            _meshFilter.sharedMesh = _mesh;
            _meshRenderer.enabled = true;
            _meshDirty = true;
            _appearanceDirty = true;
            Refresh();
        }

        protected virtual void OnDisable()
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }

            _mesh = null;
        }

        protected virtual void OnValidate()
        {
            _meshDirty = true;
            _appearanceDirty = true;
        }

        protected virtual void LateUpdate()
        {
            Refresh();
        }

        /// <summary>
        /// 変更をすぐに反映する。通常は LateUpdate で自動的に反映される。
        /// </summary>
        public void Refresh()
        {
            if (_mesh == null)
            {
                return;
            }

            // NeedsRebuild は状態を更新する場合があるので必ず呼ぶ
            if (NeedsRebuild() || _meshDirty)
            {
                BuildMesh(_mesh);
                _meshDirty = false;
            }

            if (_appearanceDirty)
            {
                ApplyAppearance();
                _appearanceDirty = false;
            }
        }

        protected void MarkMeshDirty()
        {
            _meshDirty = true;
        }

        /// <summary>
        /// 毎フレーム呼ばれる。パラメータ以外の要因（他オブジェクトの移動など）で作り直す場合に true を返す。
        /// </summary>
        protected virtual bool NeedsRebuild()
        {
            return false;
        }

        protected abstract void BuildMesh(Mesh mesh);

        /// <summary>
        /// 形状の内部から一様分布でランダムな点をワールド座標で返す。
        /// random を指定するとその乱数列を使う（再現性が必要な場合）。null なら UnityEngine.Random を使う。
        /// </summary>
        public Vector3 GetRandomPoint(System.Random random = null)
        {
            return transform.TransformPoint(GetRandomLocalPoint(random));
        }

        /// <summary>
        /// 形状の内部から一様分布でランダムな点をローカル座標で返す。
        /// </summary>
        public abstract Vector3 GetRandomLocalPoint(System.Random random = null);

        private void ApplyAppearance()
        {
            _meshRenderer.sharedMaterial = _customMaterial != null
                ? _customMaterial
                : ShapeMaterials.Get(_color.a < 1f, _alwaysOnTop);
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _propertyBlock ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, _color);
            _propertyBlock.SetColor(BaseColorId, _color);
            _propertyBlock.SetFloat(ShadingId, _shading);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
