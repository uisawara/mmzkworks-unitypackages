using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// 各 Shape のメッシュをローカル座標で生成する。
    /// </summary>
    public static class ShapeMeshBuilder
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// パイ型（扇形のリングを上下に厚みを持たせた立体）を生成する。
        /// 扇はローカル +Z を中心に左右へ angle/2 ずつ開く。
        /// bottom と top が等しい場合は厚みのない扇形の面になる。
        /// </summary>
        public static void BuildPie(Mesh mesh, float innerRadius, float outerRadius, float angle, float bottom, float top, int segmentsPerCircle)
        {
            var builder = new Builder();

            outerRadius = Mathf.Max(0f, outerRadius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius);
            angle = Mathf.Clamp(angle, 0f, 360f);
            var y0 = Mathf.Min(bottom, top);
            var y1 = Mathf.Max(bottom, top);

            if (outerRadius - innerRadius > Epsilon && angle > Epsilon)
            {
                var segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(3, segmentsPerCircle) * angle / 360f));
                var dirs = new Vector3[segments + 1];
                for (var i = 0; i <= segments; i++)
                {
                    var theta = Mathf.Deg2Rad * Mathf.Lerp(-angle * 0.5f, angle * 0.5f, i / (float)segments);
                    dirs[i] = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                }

                builder.AddSectorFace(dirs, innerRadius, outerRadius, y1, Vector3.up);

                if (y1 - y0 > Epsilon)
                {
                    builder.AddSectorFace(dirs, innerRadius, outerRadius, y0, Vector3.down);
                    builder.AddArcWall(dirs, outerRadius, y0, y1, 1f);
                    if (innerRadius > Epsilon)
                    {
                        builder.AddArcWall(dirs, innerRadius, y0, y1, -1f);
                    }

                    if (angle < 360f - Epsilon)
                    {
                        var first = dirs[0];
                        var last = dirs[segments];
                        builder.AddQuad(
                            first * innerRadius + Vector3.up * y0, first * outerRadius + Vector3.up * y0,
                            first * outerRadius + Vector3.up * y1, first * innerRadius + Vector3.up * y1,
                            Vector3.Cross(first, Vector3.up));
                        builder.AddQuad(
                            last * innerRadius + Vector3.up * y0, last * outerRadius + Vector3.up * y0,
                            last * outerRadius + Vector3.up * y1, last * innerRadius + Vector3.up * y1,
                            Vector3.Cross(Vector3.up, last));
                    }
                }
            }

            builder.ApplyTo(mesh);
        }

        /// <summary>
        /// ローカル +Y 方向に高さ height を持つコーンを生成する。
        /// apexAtOrigin が false なら底面が原点、true なら頂点が原点になる。
        /// </summary>
        public static void BuildCone(Mesh mesh, float radius, float height, bool apexAtOrigin, int segments, bool capped)
        {
            var builder = new Builder();
            radius = Mathf.Max(0f, radius);
            builder.AddFrustum(
                Vector3.zero, Vector3.up * height,
                apexAtOrigin ? 0f : radius, apexAtOrigin ? radius : 0f,
                segments, capped);
            builder.ApplyTo(mesh);
        }

        /// <summary>
        /// from から to へ伸びる円柱を生成する。
        /// </summary>
        public static void BuildCylinder(Mesh mesh, Vector3 from, Vector3 to, float radius, int segments, bool capped)
        {
            var builder = new Builder();
            radius = Mathf.Max(0f, radius);
            builder.AddFrustum(from, to, radius, radius, segments, capped);
            builder.ApplyTo(mesh);
        }

        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector3> _normals = new();
            private readonly List<int> _triangles = new();

            private int AddVertex(Vector3 position, Vector3 normal)
            {
                _vertices.Add(position);
                _normals.Add(normal);
                return _vertices.Count - 1;
            }

            /// <summary>
            /// 頂点法線の向きを表面とみなすよう、巻き順を揃えて三角形を追加する。
            /// </summary>
            private void AddTriangle(int a, int b, int c)
            {
                var pa = _vertices[a];
                var face = Vector3.Cross(_vertices[b] - pa, _vertices[c] - pa);
                if (face.sqrMagnitude < Epsilon * Epsilon)
                {
                    return;
                }

                var expected = _normals[a] + _normals[b] + _normals[c];
                _triangles.Add(a);
                if (Vector3.Dot(face, expected) >= 0f)
                {
                    _triangles.Add(b);
                    _triangles.Add(c);
                }
                else
                {
                    _triangles.Add(c);
                    _triangles.Add(b);
                }
            }

            public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
            {
                var i0 = AddVertex(p0, normal);
                var i1 = AddVertex(p1, normal);
                var i2 = AddVertex(p2, normal);
                var i3 = AddVertex(p3, normal);
                AddTriangle(i0, i1, i2);
                AddTriangle(i0, i2, i3);
            }

            public void AddSectorFace(Vector3[] dirs, float innerRadius, float outerRadius, float y, Vector3 normal)
            {
                var up = Vector3.up * y;
                for (var i = 0; i < dirs.Length - 1; i++)
                {
                    AddQuad(
                        dirs[i] * innerRadius + up, dirs[i] * outerRadius + up,
                        dirs[i + 1] * outerRadius + up, dirs[i + 1] * innerRadius + up,
                        normal);
                }
            }

            /// <param name="sign">1 で外向き、-1 で内向きの壁</param>
            public void AddArcWall(Vector3[] dirs, float radius, float y0, float y1, float sign)
            {
                var start = _vertices.Count;
                foreach (var dir in dirs)
                {
                    AddVertex(dir * radius + Vector3.up * y0, dir * sign);
                    AddVertex(dir * radius + Vector3.up * y1, dir * sign);
                }

                for (var i = 0; i < dirs.Length - 1; i++)
                {
                    var b0 = start + i * 2;
                    var t0 = b0 + 1;
                    var b1 = b0 + 2;
                    var t1 = b0 + 3;
                    AddTriangle(b0, b1, t1);
                    AddTriangle(b0, t1, t0);
                }
            }

            /// <summary>
            /// from の半径 r0、to の半径 r1 の円錐台を追加する。円柱・コーンの共通処理。
            /// </summary>
            public void AddFrustum(Vector3 from, Vector3 to, float r0, float r1, int segments, bool capped)
            {
                var axisVector = to - from;
                var length = axisVector.magnitude;
                if (length < Epsilon || (r0 < Epsilon && r1 < Epsilon))
                {
                    return;
                }

                segments = Mathf.Max(3, segments);
                var axis = axisVector / length;
                var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
                var v = Vector3.Cross(axis, u);

                var radials = new Vector3[segments + 1];
                for (var i = 0; i <= segments; i++)
                {
                    var theta = 2f * Mathf.PI * i / segments;
                    radials[i] = u * Mathf.Cos(theta) + v * Mathf.Sin(theta);
                }

                // 側面
                var start = _vertices.Count;
                foreach (var radial in radials)
                {
                    var normal = (radial * length + axis * (r0 - r1)).normalized;
                    AddVertex(from + radial * r0, normal);
                    AddVertex(to + radial * r1, normal);
                }

                for (var i = 0; i < segments; i++)
                {
                    var b0 = start + i * 2;
                    var t0 = b0 + 1;
                    var b1 = b0 + 2;
                    var t1 = b0 + 3;
                    AddTriangle(b0, b1, t1);
                    AddTriangle(b0, t1, t0);
                }

                if (!capped)
                {
                    return;
                }

                if (r0 > Epsilon)
                {
                    AddDisc(from, radials, r0, -axis);
                }

                if (r1 > Epsilon)
                {
                    AddDisc(to, radials, r1, axis);
                }
            }

            private void AddDisc(Vector3 center, Vector3[] radials, float radius, Vector3 normal)
            {
                var c = AddVertex(center, normal);
                var start = _vertices.Count;
                foreach (var radial in radials)
                {
                    AddVertex(center + radial * radius, normal);
                }

                for (var i = 0; i < radials.Length - 1; i++)
                {
                    AddTriangle(c, start + i, start + i + 1);
                }
            }

            public void ApplyTo(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = _vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
            }
        }
    }
}
