using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muPrimitive.Tests
{
    public class ShapeSamplerTests
    {
        private const int SampleCount = 1000;
        private const float Tolerance = 1e-4f;

        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void SamplePie_StaysInsideSector()
        {
            var random = new System.Random(1);
            for (var i = 0; i < SampleCount; i++)
            {
                var p = ShapeSampler.SamplePie(0.5f, 2f, 90f, -0.1f, 0.3f, random);
                var radius = new Vector2(p.x, p.z).magnitude;
                var angle = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                Assert.That(radius, Is.InRange(0.5f - Tolerance, 2f + Tolerance));
                Assert.That(angle, Is.InRange(-45f - Tolerance, 45f + Tolerance));
                Assert.That(p.y, Is.InRange(-0.1f - Tolerance, 0.3f + Tolerance));
            }
        }

        [Test]
        public void SamplePie_IsUniformByArea()
        {
            // 半径 1 の円で内側半径 sqrt(0.5) までの面積はちょうど半分
            var random = new System.Random(2);
            var inner = 0;
            for (var i = 0; i < SampleCount; i++)
            {
                var p = ShapeSampler.SamplePie(0f, 1f, 360f, 0f, 0f, random);
                if (new Vector2(p.x, p.z).sqrMagnitude < 0.5f)
                {
                    inner++;
                }
            }

            Assert.That(inner / (float)SampleCount, Is.InRange(0.45f, 0.55f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SampleCone_StaysInsideCone(bool apexAtOrigin)
        {
            var random = new System.Random(3);
            for (var i = 0; i < SampleCount; i++)
            {
                var p = ShapeSampler.SampleCone(0.5f, 2f, apexAtOrigin, random);
                Assert.That(p.y, Is.InRange(-Tolerance, 2f + Tolerance));
                var distanceFromApex = apexAtOrigin ? p.y : 2f - p.y;
                var maxRadius = 0.5f * distanceFromApex / 2f;
                Assert.That(new Vector2(p.x, p.z).magnitude, Is.LessThanOrEqualTo(maxRadius + Tolerance));
            }
        }

        [Test]
        public void SampleCylinder_StaysInsideCylinder()
        {
            var random = new System.Random(4);
            var from = new Vector3(1f, 2f, 3f);
            var to = new Vector3(-2f, 0f, 5f);
            var axis = (to - from).normalized;
            var length = (to - from).magnitude;
            for (var i = 0; i < SampleCount; i++)
            {
                var p = ShapeSampler.SampleCylinder(from, to, 0.2f, random);
                var along = Vector3.Dot(p - from, axis);
                var offAxis = Vector3.ProjectOnPlane(p - from, axis).magnitude;
                Assert.That(along, Is.InRange(-Tolerance, length + Tolerance));
                Assert.That(offAxis, Is.LessThanOrEqualTo(0.2f + Tolerance));
            }
        }

        [Test]
        public void SampleCylinder_ZeroLength_ReturnsFrom()
        {
            var from = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(from, ShapeSampler.SampleCylinder(from, from, 1f, new System.Random(5)));
        }

        [Test]
        public void SameSeed_ReturnsSamePoint()
        {
            var a = ShapeSampler.SampleCone(1f, 1f, false, new System.Random(42));
            var b = ShapeSampler.SampleCone(1f, 1f, false, new System.Random(42));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void GetRandomPoint_ReturnsWorldPositionInsideMeshBounds()
        {
            _gameObject = new GameObject("Pie");
            _gameObject.transform.SetPositionAndRotation(new Vector3(10f, 0f, -5f), Quaternion.Euler(0f, 30f, 0f));
            _gameObject.transform.localScale = new Vector3(2f, 1f, 2f);
            var pie = _gameObject.AddComponent<PieShape>();
            pie.Refresh();

            var bounds = pie.Mesh.bounds;
            bounds.Expand(Tolerance);
            var random = new System.Random(6);
            for (var i = 0; i < SampleCount; i++)
            {
                var local = _gameObject.transform.InverseTransformPoint(pie.GetRandomPoint(random));
                Assert.IsTrue(bounds.Contains(local), local.ToString("F4"));
            }
        }

        [Test]
        public void LineShape_GetRandomPoint_FollowsTargetWithoutRefresh()
        {
            _gameObject = new GameObject("Line");
            var line = _gameObject.AddComponent<LineShape>();
            line.Radius = 0f;
            line.TargetPosition = new Vector3(0f, 0f, 10f);

            var random = new System.Random(7);
            for (var i = 0; i < SampleCount; i++)
            {
                var p = line.GetRandomPoint(random);
                Assert.AreEqual(0f, p.x, Tolerance);
                Assert.AreEqual(0f, p.y, Tolerance);
                Assert.That(p.z, Is.InRange(-Tolerance, 10f + Tolerance));
            }
        }
    }
}
