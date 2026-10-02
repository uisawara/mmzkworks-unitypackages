using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public static class RectExtensions
    {
        public static Rect ToUVRect(this Rect self, Texture texture)
        {
            return new Rect(self.x / texture.width, self.y / texture.height,
                self.width / texture.width, self.height / texture.height);
        }
    }
}
