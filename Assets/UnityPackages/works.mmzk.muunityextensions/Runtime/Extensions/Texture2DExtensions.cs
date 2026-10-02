using System;
using UnityEngine;

namespace Mmzkworks.muUnityExtensions
{
    public static class Texture2DExtensions
    {
        public static Texture2D CreateResized(this Texture2D self, int width, int height)
        {
            var resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            if (!Graphics.ConvertTexture(self, 0, resizedTexture, 0))
            {
                UnityEngine.Object.DestroyImmediate(resizedTexture);
                throw new InvalidOperationException($"Graphics.ConvertTexture failed: '{self.name}'.");
            }

            return resizedTexture;
        }

        public static Texture2D CreateCropped(this Texture2D self, int x, int y, int width, int height)
        {
            var pixels = self.GetPixels(x, y, width, height);
            var croppedTexture = new Texture2D(width, height, self.format, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();
            return croppedTexture;
        }

        public static Texture2D CreateReadable(this Texture2D self,
            RenderTextureReadWrite readWrite = RenderTextureReadWrite.Linear)
        {
            var renderTexture = RenderTexture.GetTemporary(
                self.width,
                self.height,
                0,
                RenderTextureFormat.Default,
                readWrite);
            Graphics.Blit(self, renderTexture);

            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var readableTexture = new Texture2D(self.width, self.height);
            readableTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = previous;

            RenderTexture.ReleaseTemporary(renderTexture);
            return readableTexture;
        }
    }
}
