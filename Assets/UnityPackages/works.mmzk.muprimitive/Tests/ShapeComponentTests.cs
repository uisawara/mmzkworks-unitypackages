using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muPrimitive.Tests
{
    public class ShapeComponentTests
    {
        private GameObject _gameObject;
        private GameObject _target;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }

            if (_target != null)
            {
                Object.DestroyImmediate(_target);
            }
        }

        [Test]
        public void AddComponent_AssignsMeshAndMaterial()
        {
            _gameObject = new GameObject("Pie");
            var pie = _gameObject.AddComponent<PieShape>();

            var meshFilter = _gameObject.GetComponent<MeshFilter>();
            var meshRenderer = _gameObject.GetComponent<MeshRenderer>();
            Assert.AreSame(pie.Mesh, meshFilter.sharedMesh);
            Assert.That(pie.Mesh.vertexCount, Is.GreaterThan(0));
            Assert.IsNotNull(meshRenderer.sharedMaterial);
        }

        [Test]
        public void ChangingParameter_RebuildsOnRefresh()
        {
            _gameObject = new GameObject("Pie");
            var pie = _gameObject.AddComponent<PieShape>();
            pie.Angle = 360f;
            pie.OuterRadius = 3f;

            pie.Refresh();

            Assert.AreEqual(3f, pie.Mesh.bounds.max.x, 1e-4f);
        }

        [Test]
        public void Color_SelectsTransparentMaterialByAlpha()
        {
            _gameObject = new GameObject("Cone");
            var cone = _gameObject.AddComponent<ConeShape>();
            var meshRenderer = _gameObject.GetComponent<MeshRenderer>();

            cone.Color = new Color(1f, 0f, 0f, 1f);
            cone.Refresh();
            var opaque = meshRenderer.sharedMaterial;

            cone.Color = new Color(1f, 0f, 0f, 0.5f);
            cone.Refresh();
            var transparent = meshRenderer.sharedMaterial;

            Assert.AreNotSame(opaque, transparent);
            Assert.That(transparent.renderQueue, Is.GreaterThan(opaque.renderQueue));
        }

        [Test]
        public void Disable_HidesRenderer()
        {
            _gameObject = new GameObject("Cone");
            var cone = _gameObject.AddComponent<ConeShape>();
            var meshRenderer = _gameObject.GetComponent<MeshRenderer>();

            cone.enabled = false;
            Assert.IsFalse(meshRenderer.enabled);

            cone.enabled = true;
            Assert.IsTrue(meshRenderer.enabled);
        }

        [Test]
        public void LineShape_FollowsTarget()
        {
            _gameObject = new GameObject("Line");
            _target = new GameObject("Target");
            var line = _gameObject.AddComponent<LineShape>();
            line.Radius = 0.1f;
            line.Target = _target.transform;

            _target.transform.position = new Vector3(0f, 0f, 5f);
            line.Refresh();
            Assert.AreEqual(5f, line.Mesh.bounds.max.z, 1e-4f);

            _target.transform.position = new Vector3(0f, 4f, 0f);
            line.Refresh();
            Assert.AreEqual(4f, line.Mesh.bounds.max.y, 1e-4f);
            Assert.AreEqual(0.1f, line.Mesh.bounds.max.z, 1e-4f);
        }

        [Test]
        public void LineShape_UsesTargetPositionWithoutTarget()
        {
            _gameObject = new GameObject("Line");
            _gameObject.transform.position = new Vector3(1f, 0f, 0f);
            var line = _gameObject.AddComponent<LineShape>();
            line.Radius = 0.1f;
            line.TargetPosition = new Vector3(1f, 0f, -3f);

            line.Refresh();

            Assert.AreEqual(-3f, line.Mesh.bounds.min.z, 1e-4f);
        }
    }
}
