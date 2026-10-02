using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public static class Vector3Extensions
    {
        public static Vector3 WithX(this Vector3 self, float v)
        {
            return new Vector3(v, self.y, self.z);
        }
        public static Vector3 WithY(this Vector3 self, float v)
        {
            return new Vector3(self.x, v, self.z);
        }
        public static Vector3 WithZ(this Vector3 self, float v)
        {
            return new Vector3(self.x, self.y, v);
        }
        public static Vector2 XY(this Vector3 self)
        {
            return new Vector2(self.x, self.y);
        }
        public static Vector2 XZ(this Vector3 self)
        {
            return new Vector2(self.x, self.z);
        }
        public static Vector2 YZ(this Vector3 self)
        {
            return new Vector2(self.y, self.z);
        }
        public static float[] ToArray(this Vector3 self)
        {
            return new[] { self.x, self.y, self.z };
        }
    }
}
