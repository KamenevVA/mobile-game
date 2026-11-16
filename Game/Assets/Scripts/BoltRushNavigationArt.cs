using System.Collections.Generic;
using UnityEngine;

namespace BoltRush
{
    public sealed partial class BoltRushGame
    {
        enum Symbol { Play, Pause, Settings, Home, Retry, Back, Sound, Effects, Video, Shop, Journal, Vibration }
        static readonly Color Brass = new Color32(245, 206, 128, 255);
        static readonly Color Enamel = new Color32(52, 120, 112, 255);
        static readonly Color Midnight = new Color32(41, 57, 91, 255);
        readonly Dictionary<Symbol, Sprite> navigationArt = new Dictionary<Symbol, Sprite>();

        Btn IconButton(Symbol icon, Vector2 position, Vector2 size, Color color, int order, Transform parent,
            System.Func<bool> visible, System.Action click)
        {
            var button = AddButton("", position, size, color, order, parent, visible, click);
            var art = NavigationSprite(icon);
            var symbol = MakeSprite("Illustration", button.t, art, Color.white, order + 1);
            bool painted = icon == Symbol.Shop || icon == Symbol.Journal;
            symbol.transform.localScale = Vector3.one * Mathf.Min(size.x, size.y) * (painted ? 1.04f : 0.78f);
            return button;
        }

        Sprite NavigationSprite(Symbol symbol)
        {
            if (navigationArt.TryGetValue(symbol, out Sprite sprite)) return sprite;
            if (symbol == Symbol.Shop) sprite = LoadIllustration("shop");
            if (symbol == Symbol.Journal) sprite = LoadIllustration("journal");
            if (sprite == null) sprite = DrawSymbol(symbol);
            navigationArt.Add(symbol, sprite);
            return sprite;
        }

        static Sprite DrawSymbol(Symbol symbol)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / 32f - 1f, v = (y + 0.5f) / 32f - 1f;
                bool fill = SymbolAt(symbol, u, v);
                bool edge = !fill && (SymbolAt(symbol, u + 0.055f, v) || SymbolAt(symbol, u - 0.055f, v) ||
                    SymbolAt(symbol, u, v + 0.055f) || SymbolAt(symbol, u, v - 0.055f));
                if (fill) pixels[y * size + x] = Color32.Lerp(new Color32(224, 159, 66, 255), new Color32(255, 246, 199, 255), (v + 1f) / 2f);
                else if (edge) pixels[y * size + x] = new Color32(40, 49, 73, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static bool SymbolAt(Symbol symbol, float x, float y)
        {
            float a = Mathf.Abs(x), b = Mathf.Abs(y), r = Mathf.Sqrt(x * x + y * y);
            bool triangle = x >= -0.52f && x <= 0.65f && b < (0.66f - x) * 0.56f;
            switch (symbol)
            {
                case Symbol.Play: return triangle;
                case Symbol.Pause: return a > 0.18f && a < 0.44f && b < 0.61f;
                case Symbol.Settings:
                    return r > 0.23f && r < 0.60f + 0.13f * Mathf.Max(0f, Mathf.Cos(8f * Mathf.Atan2(y, x)));
                case Symbol.Home:
                    return (y > 0f && y < 0.65f && a < 0.79f - y) ||
                        (a < 0.52f && y >= -0.63f && y <= 0.15f && (a > 0.15f || y > -0.29f));
                case Symbol.Retry:
                    return (r > 0.44f && r < 0.67f && !(x < -0.29f && y > 0.17f)) ||
                        (x > -0.81f && x < -0.19f && Mathf.Abs(y - 0.38f) < (x + 0.81f) * 0.61f);
                case Symbol.Back: return a < 0.70f && (b < 0.12f || (x < 0.03f && Mathf.Abs(b - (x + 0.65f)) < 0.14f));
                case Symbol.Sound:
                    float wave = Mathf.Sqrt((x - 0.03f) * (x - 0.03f) + y * y);
                    return (x > -0.72f && x < -0.43f && b < 0.23f) ||
                        (x >= -0.44f && x < -0.08f && b < 0.23f + (x + 0.44f) * 0.87f) ||
                        (x > 0.11f && ((wave > 0.25f && wave < 0.38f && b < 0.33f) ||
                        (wave > 0.51f && wave < 0.65f && b < 0.52f)));
                case Symbol.Vibration:
                    return (a < 0.31f && b < 0.63f && (a > 0.21f || b > 0.48f || y < -0.35f)) ||
                        (b < 0.48f && Mathf.Abs(a - 0.61f - 0.09f * Mathf.Sin(y * 15f)) < 0.09f);
                case Symbol.Video: return (a < 0.81f && b < 0.63f && (a > 0.68f || b > 0.50f)) || triangle;
                case Symbol.Shop:
                    return (y > 0.14f && y < 0.48f && a < 0.71f) ||
                        (y > -0.60f && y < 0.15f && a < 0.54f && (a > 0.17f || y > -0.22f));
                default: return a + b < 0.86f && (a < 0.22f || b < 0.22f || symbol == Symbol.Journal);
            }
        }
    }
}
