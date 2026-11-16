using UnityEngine;
using System;
using System.Collections.Generic;

namespace BoltRush
{
    public sealed partial class BoltRushGame
    {
        Sprite sprCoin;
        SpriteRenderer shellBackdrop, shellCard, shellShadow;
        readonly List<ShopCard> shopCards = new List<ShopCard>();
        float shopScroll, shopStartScroll;
        Vector2 shopPressPoint;
        bool shopDragging, shopPressed;
        GUIStyle catalogLabel;

        sealed class ShopCard
        {
            public string title, detail, price;
            public Sprite illustration;
            public Color color;
            public Action action;
        }

        void BuildShop()
        {
            var coinsArt = LoadIllustration("coins") ?? sprCoin;
            AddCatalogCard("Master's Chest", "500 coins + 5 of each tool", "€4.99", LoadIllustration("basket"), Brass,
                () => shopNotice = "PREVIEW PRICE");
            AddCatalogCard("Daily Gift", "Rewarded video", "VIDEO", coinsArt, new Color32(194, 228, 212, 255),
                () => shopNotice = "VIDEO UNAVAILABLE");
            for (int i = 0; i < ToolNames.Length; i++)
            {
                if (i == (int)Tool.Basket) continue;
                int index = i;
                AddCatalogCard(ToolNames[i], "+1", ToolPrices[i].ToString(), illustratedTools[i], Paper,
                    () => BuyTool(ToolPrices[index], ref stock[index]));
            }
            AddCatalogCard("Coin Pouch", "500", "€1.99", coinsArt, new Color32(206, 222, 247, 255), () => shopNotice = "PREVIEW PRICE");
            AddCatalogCard("Coin Chest", "1500", "€4.99", coinsArt, new Color32(206, 222, 247, 255), () => shopNotice = "PREVIEW PRICE");
            AddCatalogCard("Treasury", "4000", "€9.99", coinsArt, Brass, () => shopNotice = "PREVIEW PRICE");
        }

        void AddCatalogCard(string title, string detail, string price, Sprite illustration, Color color, Action action)
        {
            shopCards.Add(new ShopCard { title = title, detail = detail, price = price, illustration = illustration, color = color, action = action });
        }

        float CatalogScale => Screen.width / 430f;
        float CatalogTop => Mathf.Max(Screen.height - Screen.safeArea.yMax, Screen.height * 0.055f);
        Rect CatalogBack => new Rect(18 * CatalogScale, CatalogTop + 10 * CatalogScale, 55 * CatalogScale, 48 * CatalogScale);
        Rect CatalogViewport => new Rect(0, CatalogTop + 78 * CatalogScale, Screen.width,
            Mathf.Max(1, Screen.height - CatalogTop - 116 * CatalogScale - Screen.safeArea.yMin));
        const float CatalogRow = 123f;

        void UpdateCatalogInput(PointerPhase phase, Vector2 pointer)
        {
            Rect viewport = CatalogViewport;
            Vector2 point = new Vector2(pointer.x, Screen.height - pointer.y);
            float maxScroll = Mathf.Max(0f, (shopCards.Count * CatalogRow + 12f) * CatalogScale - viewport.height);
            shopScroll = Mathf.Clamp(shopScroll - Input.mouseScrollDelta.y * 42f * CatalogScale, 0f, maxScroll);
            if (phase == PointerPhase.Cancelled) { shopPressed = false; return; }
            if (phase == PointerPhase.Pressed)
            {
                if (CatalogBack.Contains(point)) { ShowShell(shopBack); return; }
                shopPressed = viewport.Contains(point);
                shopDragging = false;
                shopPressPoint = point;
                shopStartScroll = shopScroll;
            }
            if (!shopPressed || phase == PointerPhase.None) return;
            Vector2 movement = point - shopPressPoint;
            if (movement.sqrMagnitude > (9 * CatalogScale) * (9 * CatalogScale)) shopDragging = true;
            if (shopDragging) shopScroll = Mathf.Clamp(shopStartScroll - movement.y, 0f, maxScroll);
            if (phase != PointerPhase.Released) return;
            shopPressed = false;
            if (shopDragging || !viewport.Contains(point)) return;
            float localY = (point.y - viewport.y + shopScroll) / CatalogScale - 7f;
            int index = Mathf.FloorToInt(localY / CatalogRow);
            if (index >= 0 && index < shopCards.Count && localY - index * CatalogRow <= 113f &&
                point.x >= 20 * CatalogScale && point.x <= Screen.width - 20 * CatalogScale)
                shopCards[index].action();
        }

        void DrawShop()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (catalogLabel == null)
                catalogLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            float k = CatalogScale;
            Rect viewport = CatalogViewport;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, new Color32(248, 242, 221, 255), 0, 0);
            GUI.BeginGroup(viewport);
            for (int i = 0; i < shopCards.Count; i++)
            {
                var card = shopCards[i];
                float y = (7 + i * CatalogRow) * k - shopScroll;
                if (y + 113 * k <= 0 || y >= viewport.height) continue;
                CatalogPanel(new Rect(20 * k, y + 4 * k, 390 * k, 109 * k), Midnight);
                CatalogPanel(new Rect(20 * k, y, 390 * k, 109 * k), card.color);
                CatalogArt(card.illustration, new Rect(24 * k, y + 5 * k, 99 * k, 99 * k));
                CatalogText(card.title, new Rect(120 * k, y + 20 * k, 166 * k, 34 * k), 18, Ink);
                CatalogText(card.detail, new Rect(120 * k, y + 55 * k, 166 * k, 28 * k), 14, Ink);
                CatalogPanel(new Rect(291 * k, y + 32 * k, 102 * k, 48 * k), Enamel);
                CatalogText(card.price, new Rect(293 * k, y + 32 * k, 98 * k, 48 * k), 17, Color.white);
            }
            GUI.EndGroup();
            CatalogPanel(new Rect(0, 0, Screen.width, viewport.y - 4 * k), Midnight);
            CatalogPanel(CatalogBack, Enamel);
            CatalogArt(NavigationSprite(Symbol.Back), CatalogBack);
            CatalogArt(NavigationSprite(Symbol.Shop), new Rect(109 * k, CatalogTop + 3 * k, 59 * k, 59 * k));
            CatalogText("SHOP", new Rect(162 * k, CatalogTop + 6 * k, 95 * k, 56 * k), 28, Brass);
            CatalogArt(sprCoin, new Rect(296 * k, CatalogTop + 19 * k, 25 * k, 25 * k));
            CatalogText(walletLabel, new Rect(319 * k, CatalogTop + 13 * k, 100 * k, 38 * k), 18, Color.white);
            CatalogText(shopNotice.Length == 0 ? "€  PREVIEW PRICES" : shopNotice,
                new Rect(15 * k, viewport.yMax, 400 * k, 33 * k), 13, Ink);
        }

        void CatalogPanel(Rect rect, Color tint) => GUI.DrawTexture(rect, Texture2D.whiteTexture,
            ScaleMode.StretchToFill, true, 0, tint, 0, Mathf.Min(13 * CatalogScale, rect.height * 0.24f));

        static void CatalogArt(Sprite art, Rect area)
        {
            if (art != null) GUI.DrawTexture(area, art.texture, ScaleMode.ScaleToFit, true);
        }

        void CatalogText(string value, Rect rect, int size, Color tint)
        {
            catalogLabel.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * CatalogScale));
            catalogLabel.normal.textColor = tint;
            GUI.Label(rect, value, catalogLabel);
        }

        void BuyTool(int price, ref int stock)
        {
            if (coins < price)
            {
                shopNotice = "NOT ENOUGH COINS";
                return;
            }
            coins -= price;
            stock++;
            shopNotice = "PURCHASED";
            SaveWallet();
        }

        void SaveWallet()
        {
            PlayerPrefs.SetInt("br_coins", coins);
            for (int i = 0; i < stock.Length; i++) PlayerPrefs.SetInt("br_tool_" + i, stock[i]);
            PlayerPrefs.Save();
            RefreshWallet();
        }

        static Sprite MakeCoinSprite()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x - 23.5f, py = y - 23.5f;
                float radius = Mathf.Sqrt(px * px + py * py);
                Color32 color = radius > 19f ? new Color32(158, 88, 29, 255) :
                    radius > 15f ? new Color32(255, 224, 123, 255) :
                    new Color32(242, 174, 59, 255);
                if (Mathf.Abs(px) < 3.5f && Mathf.Abs(py) < 9f)
                    color = new Color32(255, 232, 139, 255);
                color.a = (byte)(255f * Mathf.Clamp01(22.5f - radius));
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
