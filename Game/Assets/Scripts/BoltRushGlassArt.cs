using UnityEngine;

namespace BoltRush
{
    public sealed partial class BoltRushGame
    {
        static readonly Color[] GlassColors =
        {
            new Color32(127, 222, 230, 255), new Color32(187, 166, 255, 255),
            new Color32(130, 238, 196, 255), new Color32(255, 202, 127, 255),
            new Color32(255, 157, 185, 255), new Color32(163, 203, 252, 255)
        };
        const int ShapeCount = 8;
        readonly Sprite[,] glassShapes = new Sprite[2, ShapeCount];

        void BuildGlassArt()
        {
            for (int shape = 0; shape < ShapeCount; shape++)
            {
                glassShapes[0, shape] = MakeGlassShape(144, 54, shape);
                glassShapes[1, shape] = MakeGlassShape(112, 88, shape);
            }
        }

        Sprite GlassShape(PlateData plate, int id) =>
            glassShapes[plate.h > 1.1f ? 1 : 0, ShapeStyle(id)];

        int ShapeStyle(int id) => Geo.GlassStyle(L.index, id);

        static Sprite MakeGlassShape(int width, int height, int style)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = 2f * (x + 0.5f) / width - 1f, v = 2f * (y + 0.5f) / height - 1f;
                float edge = Geo.GlassDistance(u, v, style, height > 60) * height * 0.5f;

                float alpha = Mathf.Clamp01(edge + 0.65f);
                if (alpha <= 0f) continue;
                float rim = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.7f) / 2.4f));
                float shine = Mathf.Exp(-Mathf.Pow((u + v * 0.52f + 0.18f) / 0.075f, 2f)) * 0.42f;
                float inlay = Mathf.Clamp01(1f - Mathf.Abs(edge - 4.7f) / 0.94f) * 0.23f;
                float engraving = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Abs(u + v * 0.31f) + Mathf.Abs(v) * 0.39f - 0.42f) / 0.032f)
                    * Mathf.Clamp01((edge - 6f) / 3f) * 0.21f;
                float white = Mathf.Clamp01(rim * 0.72f + shine + inlay + engraving);
                byte value = (byte)Mathf.Lerp(159f, 255f, white);
                pixels[y * width + x] = new Color32(value, value, value,
                    (byte)(alpha * Mathf.Lerp(143f, 252f, Mathf.Clamp01(rim + shine + inlay + engraving))));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                width, 0, SpriteMeshType.FullRect, Vector4.one);
        }
    }
}
