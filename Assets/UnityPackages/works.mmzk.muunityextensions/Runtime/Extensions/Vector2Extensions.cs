using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public static class Vector2Extensions
    {
        public static Vector3 ToVector3XY(this Vector2 self, float z = 0f)
        {
            return new Vector3(self.x, self.y, z);
        }

        public static Vector3 ToVector3XZ(this Vector2 self, float y = 0f)
        {
            return new Vector3(self.x, y, self.y);
        }

        public static Vector3 ToVector3YZ(this Vector2 self, float x = 0f)
        {
            return new Vector3(x, self.x, self.y);
        }

        public static float[] ToArray(this Vector2 self)
        {
            return new[] { self.x, self.y };
        }
    }
}
