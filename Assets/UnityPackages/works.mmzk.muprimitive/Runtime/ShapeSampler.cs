using UnityEngine;

namespace Mmzkworks.muPrimitive
{
    /// <summary>
    /// 各 Shape の内部（体積内）から一様分布でランダムな点をローカル座標で返す。
    /// 形状の定義は ShapeMeshBuilder と同じ。
    /// random が null の場合は UnityEngine.Random を使う。
    /// </summary>
    public static class ShapeSampler
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// パイ型の内部の点を返す。扇はローカル +Z を中心に左右へ angle/2 ずつ開く。
        /// </summary>
        public static Vector3 SamplePie(float innerRadius, float outerRadius, float angle, float bottom, float top, System.Random random = null)
        {
            outerRadius = Mathf.Max(0f, outerRadius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius);
            angle = Mathf.Clamp(angle, 0f, 360f);

            // 面積が半径の 2 乗に比例するので、半径の 2 乗を一様に選ぶ
            var r = Mathf.Sqrt(Mathf.Lerp(innerRadius * innerRadius, outerRadius * outerRadius, Next(random)));
            var theta = Mathf.Deg2Rad * Mathf.Lerp(-angle * 0.5f, angle * 0.5f, Next(random));
            var y = Mathf.Lerp(bottom, top, Next(random));
            return new Vector3(Mathf.Sin(theta) * r, y, Mathf.Cos(theta) * r);
        }

        /// <summary>
        /// ローカル +Y 方向に高さ height を持つコーンの内部の点を返す。
        /// apexAtOrigin が false なら底面が原点、true なら頂点が原点。
        /// </summary>
        public static Vector3 SampleCone(float radius, float height, bool apexAtOrigin, System.Random random = null)
        {
            radius = Mathf.Max(0f, radius);

            // 頂点からの距離の割合 t での断面積は t^2 に比例するので、t = cbrt(u) で選ぶ
            var t = Mathf.Pow(Next(random), 1f / 3f);
            var disk = SampleDisk(radius * t, random);
            var y = apexAtOrigin ? height * t : height * (1f - t);
            return new Vector3(disk.x, y, disk.y);
        }

        /// <summary>
        /// from から to までを結ぶ半径 radius の円柱の内部の点を返す。
        /// </summary>
        public static Vector3 SampleCylinder(Vector3 from, Vector3 to, float radius, System.Random random = null)
        {
            radius = Mathf.Max(0f, radius);
            var axis = to - from;
            var length = axis.magnitude;
            var t = Next(random);
            var disk = SampleDisk(radius, random);
            if (length < Epsilon)
            {
                return from;
            }

            var forward = axis / length;
            var reference = Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.right;
            var right = Vector3.Cross(reference, forward).normalized;
            var up = Vector3.Cross(forward, right);
            return from + axis * t + right * disk.x + up * disk.y;
        }

        /// <summary>半径 radius の円盤内の点を一様に返す。</summary>
        private static Vector2 SampleDisk(float radius, System.Random random)
        {
            var r = radius * Mathf.Sqrt(Next(random));
            var theta = Next(random) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r);
        }

        private static float Next(System.Random random)
        {
            return random != null ? (float)random.NextDouble() : Random.value;
        }
    }
}
