using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muPrimitive.Tests
{
    public class ShapeMeshBuilderTests
    {
        private const float Tolerance = 1e-4f;

        private Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void BuildPie_FullRing_BoundsMatchRadiusAndHeight()
        {
            ShapeMeshBuilder.BuildPie(_mesh, 0.5f, 1f, 360f, 0f, 2f, 64);

            AssertVector(new Vector3(-1f, 0f, -1f), _mesh.bounds.min);
            AssertVector(new Vector3(1f, 2f, 1f), _mesh.bounds.max);
        }

        [Test]
        public void BuildPie_PartialAngle_OpensAroundForward()
        {
            ShapeMeshBuilder.BuildPie(_mesh, 0.5f, 1f, 90f, -1f, 1f, 64);

            var half = Mathf.Sin(45f * Mathf.Deg2Rad);
            AssertVector(new Vector3(-half, -1f, 0.5f * half), _mesh.bounds.min);
            AssertVector(new Vector3(half, 1f, 1f), _mesh.bounds.max);
        }

        [Test]
        public void BuildPie_TopBelowBottom_IsTreatedAsSwapped()
        {
            ShapeMeshBuilder.BuildPie(_mesh, 0f, 1f, 360f, 2f, 1f, 64);

            Assert.AreEqual(1f, _mesh.bounds.min.y, Tolerance);
            Assert.AreEqual(2f, _mesh.bounds.max.y, Tolerance);
        }

        [Test]
        public void BuildPie_NoHeight_OnlyTopFace()
        {
            ShapeMeshBuilder.BuildPie(_mesh, 0.5f, 1f, 360f, 0f, 0f, 64);

            Assert.AreEqual(64 * 2 * 3, _mesh.triangles.Length);
            foreach (var normal in _mesh.normals)
            {
                AssertVector(Vector3.up, normal);
            }
        }

        [Test]
        public void BuildPie_InnerEqualsOuter_IsEmpty()
        {
            ShapeMeshBuilder.BuildPie(_mesh, 1f, 1f, 360f, 0f, 1f, 64);

            Assert.AreEqual(0, _mesh.vertexCount);
        }

        [Test]
        public void BuildPie_TrianglesFaceTheirVertexNormals()
        {
            ShapeMeshBuilder.BuildPie(_mesh, 0.3f, 1f, 120f, 0f, 0.5f, 32);

            AssertTrianglesMatchNormals(_mesh);
        }

        [Test]
        public void BuildCone_BaseAtOrigin()
        {
            ShapeMeshBuilder.BuildCone(_mesh, 0.5f, 2f, false, 32, true);

            Assert.AreEqual(0f, _mesh.bounds.min.y, Tolerance);
            Assert.AreEqual(2f, _mesh.bounds.max.y, Tolerance);
            Assert.AreEqual(0.5f, _mesh.bounds.extents.x, Tolerance);
            AssertOutward(_mesh, new Vector3(0f, 0.5f, 0f));
        }

        [Test]
        public void BuildCone_ApexAtOrigin()
        {
            ShapeMeshBuilder.BuildCone(_mesh, 0.5f, 2f, true, 32, true);

            Assert.AreEqual(0f, _mesh.bounds.min.y, Tolerance);
            Assert.AreEqual(2f, _mesh.bounds.max.y, Tolerance);
            AssertOutward(_mesh, new Vector3(0f, 1.5f, 0f));
        }

        [Test]
        public void BuildCylinder_SpansFromTo()
        {
            var from = new Vector3(1f, 2f, 3f);
            var to = new Vector3(1f, 2f, 7f);
            ShapeMeshBuilder.BuildCylinder(_mesh, from, to, 0.25f, 16, true);

            AssertVector(new Vector3(0.75f, 1.75f, 3f), _mesh.bounds.min);
            AssertVector(new Vector3(1.25f, 2.25f, 7f), _mesh.bounds.max);
            AssertOutward(_mesh, (from + to) * 0.5f);
        }

        [Test]
        public void BuildCylinder_DiagonalAxis_FacesOutward()
        {
            var from = new Vector3(-1f, 0.5f, 2f);
            var to = new Vector3(2f, 3f, -1f);
            ShapeMeshBuilder.BuildCylinder(_mesh, from, to, 0.2f, 16, true);

            AssertOutward(_mesh, (from + to) * 0.5f);
            AssertTrianglesMatchNormals(_mesh);
        }

        [Test]
        public void BuildCylinder_ZeroLength_IsEmpty()
        {
            ShapeMeshBuilder.BuildCylinder(_mesh, Vector3.one, Vector3.one, 0.2f, 16, true);

            Assert.AreEqual(0, _mesh.vertexCount);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Tolerance), $"expected {expected}, actual {actual}");
        }

        /// <summary>凸形状の全三角形が中心から外を向いていること。</summary>
        private static void AssertOutward(Mesh mesh, Vector3 center)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            Assert.That(triangles.Length, Is.GreaterThan(0));
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var face = Vector3.Cross(b - a, c - a);
                var centroid = (a + b + c) / 3f;
                Assert.That(Vector3.Dot(face, centroid - center), Is.GreaterThan(0f), $"triangle {i / 3} faces inward");
            }
        }

        /// <summary>全三角形の表側が頂点法線と同じ側を向いていること。</summary>
        private static void AssertTrianglesMatchNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            Assert.That(triangles.Length, Is.GreaterThan(0));
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                var normal = normals[a] + normals[b] + normals[c];
                Assert.That(Vector3.Dot(face, normal), Is.GreaterThan(0f), $"triangle {i / 3} is flipped");
            }
        }
    }
}
