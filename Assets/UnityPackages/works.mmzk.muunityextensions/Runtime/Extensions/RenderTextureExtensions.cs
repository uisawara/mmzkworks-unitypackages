using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public static class RenderTextureExtensions
    {
        public static void CopyFrom(this RenderTexture self, Texture2D src, RectInt srcRect, Vector2Int dstPosition)
        {
            Graphics.CopyTexture(
                src, 0, 0, srcRect.x, srcRect.y, srcRect.width, srcRect.height,
                self, 0, 0, dstPosition.x, dstPosition.y);
        }

        public static Texture2D ToTexture2D(this RenderTexture self, TextureFormat format = TextureFormat.RGBA32,
            bool mipmaps = false)
        {
            var previous = RenderTexture.active;

            RenderTexture.active = self;
            var texture = new Texture2D(self.width, self.height, format, mipmaps);
            texture.ReadPixels(new Rect(0, 0, self.width, self.height), 0, 0);
            texture.Apply();

            RenderTexture.active = previous;
            return texture;
        }
    }
}
