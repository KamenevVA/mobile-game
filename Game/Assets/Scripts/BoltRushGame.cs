using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BoltRush
{
    public sealed partial class BoltRushGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { new GameObject("BoltRush").AddComponent<BoltRushGame>(); }

        // ---------- layout (world units, camera is fitted to a 9 x 20 design area)
        const float BoardCy = -0.28f, BoxY = 5.82f, BufY = 4.19f, BtnY = -7.0f;
        static readonly float[] BoxX = { -3.15f, -1.05f, 1.05f, 3.15f };
        const int MaxCap = 8;

        static readonly Color[] Palette =
        {
            new Color(0.96f, 0.27f, 0.31f), new Color(1.00f, 0.62f, 0.13f), new Color(1.00f, 0.86f, 0.18f),
            new Color(0.27f, 0.80f, 0.40f), new Color(0.25f, 0.56f, 0.97f), new Color(0.68f, 0.40f, 0.93f)
        };

        sealed class PlateView { public Transform t; public SpriteRenderer[] all; public int remaining, index; public bool revealed; }
        sealed class ScrewView { public int id, color, bufferSlot = -1; public Transform t; public SpriteRenderer sr; public PlateView plate; public bool busy, removed; }
        sealed class BoxView
        {
            public int index, color, arrived; public Transform t; public Vector3 basePos; public bool completing, ready, departed;
            public Vector3 SlotWorld(int slot) => basePos + SlotOffset(slot);
            public static Vector3 SlotOffset(int slot) => slot == 0 ? new Vector3(0, 0.30f, 0)
                : new Vector3(slot == 1 ? -0.37f : 0.37f, -0.27f, 0);
        }
        sealed class Btn { public Transform t; public SpriteRenderer sr; public Vector2 size; public string label; public Func<bool> visible; public Action click; }
        enum State { Playing, Won, Failed }

        Camera cam; float halfW, halfH;
        Transform world, tray, overlay;
        Sprite sprRound, sprCircle, sprScrew, sprGrad, sprShard;
        SpriteRenderer progressFill, progressTrack;
        AudioSource audioSrc;
        AudioClip sndTap, sndLand, sndBox, sndFall, sndFail, sndWin, sndBlocked;
        GUIStyle labelStyle;

        LevelPack pack; int levelNumber; LevelData L; LevelGeo geo; Sim sim;
        State state; int pending, cap;
        readonly List<PlateView> plates = new List<PlateView>();
        readonly List<ScrewView> screws = new List<ScrewView>();
        readonly Dictionary<int, BoxView> boxViews = new Dictionary<int, BoxView>();
        readonly BoxView[] slotViews = new BoxView[Const.ActiveBoxes + Sim.ExtraLaneCount];
        readonly List<int> activeTmp = new List<int>();
        ScrewView[] bufferOcc = new ScrewView[0];
        SpriteRenderer trayBody;
        readonly List<Btn> buttons = new List<Btn>();
        string levelLabel = "", overlayTitle = "", overlayHint = "";
        ulong departingPlates;
        int layoutWidth, layoutHeight;
        Rect layoutSafeArea;
        bool layoutPlaying;
        ShellScreen layoutScreen;
        readonly GUIContent measuredLabel = new GUIContent();
        Transform background;
        int pointerFinger = -1;
        Vector2 pointerPosition;

        // =============================================================== setup
        void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Camera"); cam = cg.AddComponent<Camera>(); cg.tag = "MainCamera"; }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(13, 34, 39, 255);
            cam.transform.position = new Vector3(0, 0, -10);
            FitCamera();

            sprRound = MakeRoundSprite(64, 18f, 6f);
            sprCircle = MakeCircleSprite(64);
            sprScrew = MakeScrewSprite();
            sprShard = MakeShardSprite();
            sprCoin = MakeCoinSprite();
            BuildGlassArt();
            sprGrad = MakeGradientSprite();
            audioSrc = gameObject.AddComponent<AudioSource>();
            MakeSounds();

            var bg = new GameObject("Bg").AddComponent<SpriteRenderer>();
            bg.sprite = sprGrad; bg.sortingOrder = -100;
            background = bg.transform;

            BuildUi();
            var asset = Resources.Load<TextAsset>("levels");
            pack = asset != null ? JsonUtility.FromJson<LevelPack>(asset.text) : new LevelPack();
            BuildShell();
            LoadLevel(SavedLevel, ShellScreen.Home);
            layoutWidth = 0;
            FitCamera();
        }

        void FitCamera()
        {
            int width = Screen.width, height = Screen.height;
            Rect safe = Screen.safeArea;
            bool playing = CanUseTools;
            if (layoutWidth == width && layoutHeight == height && layoutSafeArea == safe &&
                layoutPlaying == playing && layoutScreen == shellScreen) return;
            layoutWidth = width; layoutHeight = height; layoutSafeArea = safe;
            layoutPlaying = playing; layoutScreen = shellScreen;
            float aspect = (float)width / Mathf.Max(1, height);
            cam.orthographicSize = Mathf.Max(4.5f / aspect, 10f);
            halfH = cam.orthographicSize; halfW = halfH * aspect;
            if (background != null)
                background.localScale = new Vector3(halfW * 2f + 2f, (halfH * 2f + 2f) / 32f, 1f);
            if (progressTrack == null) return;

            // Some camera overlays are taller than the cutout reported by Android.
            float topInset = Mathf.Max(height - safe.yMax, height * 0.055f);
            float hudTop = cam.ScreenToWorldPoint(new Vector3(0, height - topInset, 0)).y;
            progressTrack.transform.localPosition = new Vector3(0, hudTop - 1.65f, 0);
            Vector3 fill = progressFill.transform.localPosition;
            fill.y = progressTrack.transform.localPosition.y;
            progressFill.transform.localPosition = fill;
            if (pauseButton != null)
                pauseButton.t.position = new Vector3(-3.55f, hudTop - 0.53f, 0);
            if (shopButton != null)
                shopButton.t.position = new Vector3(CanUseTools ? -2.26f : -3.55f, hudTop - 0.53f, 0);
            if (walletCoin != null)
            {
                bool hud = shellScreen == ShellScreen.Home || (InGame && state == State.Playing);
                walletCoin.enabled = hud || shellScreen == ShellScreen.Shop;
                walletCoin.transform.position = hud ? new Vector3(2.88f, hudTop - 0.53f, 0) : new Vector3(-0.54f, 4.94f, 0);
            }
        }

        // =============================================================== procedural art
        static Sprite MakeRoundSprite(int size, float radius, float rim, bool metal = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f - half, cy = y + 0.5f - half;
                    float qx = Mathf.Abs(cx) - (half - radius), qy = Mathf.Abs(cy) - (half - radius);
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-d / rim));
                    float light = cy / half;
                    float shine = metal ? 0.17f * Mathf.Exp(-Mathf.Pow((cx + cy * 0.82f + size * 0.23f) / 6f, 2f)) : 0f;
                    float shade = Mathf.Lerp(0.52f + 0.30f * Mathf.Max(0f, light), 0.89f + 0.10f * light + shine, bevel);
                    byte v = (byte)(Mathf.Clamp01(shade) * 255f);
                    px[y * size + x] = new Color32(v, v, v, (byte)(a * (metal ? Mathf.Lerp(250f, 157f, bevel) : 255f)));
                }
            tex.SetPixels32(px); tex.Apply();
            float b = radius + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - r) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeScrewSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f, R = 60f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f - half, cy = y + 0.5f - half;
                    float r = Mathf.Sqrt(cx * cx + cy * cy);
                    float a = Mathf.Clamp01(R - r + 0.5f);
                    float light = (cy - cx * 0.5f) / R;
                    float shade = r > 53f ? 0.43f + 0.30f * Mathf.Max(0f, light) : 0.85f + 0.16f * light;
                    if (r > 43f && r <= 51f) shade += 0.17f + 0.13f * Mathf.Max(0f, light);
                    if (r < 42f)
                        shade += 0.14f * Mathf.Exp(-Mathf.Pow((cx + cy * 0.65f + 21f) / 7.5f, 2f)) * Mathf.Max(0f, cy / 38f);
                    float slot = Mathf.Clamp01(Mathf.Min(8f - Mathf.Min(Mathf.Abs(cx), Mathf.Abs(cy)),
                        27f - Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cy))));
                    shade = Mathf.Lerp(shade, 0.23f + 0.09f * light, slot);
                    byte g = (byte)(Mathf.Clamp01(shade) * 255f);
                    byte red = r > 54f ? (byte)Mathf.Min(255, g + 18) : g;
                    byte blue = r > 54f ? (byte)Mathf.Max(0, g - 12) : g;
                    px[y * size + x] = new Color32(red, g, blue, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 0.66f);
        }

        static Sprite MakeGradientSprite()
        {
            var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color top = new Color32(253, 247, 226, 255), bot = new Color32(201, 223, 220, 255);
            for (int y = 0; y < 64; y++)
            {
                Color c = Color.Lerp(bot, top, y / 63f);
                tex.SetPixel(0, y, c); tex.SetPixel(1, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 2f);
        }

        // =============================================================== sound
        AudioClip Tone(float f0, float f1, float dur, float vol, bool noise = false)
        {
            int n = (int)(44100 * dur);
            var d = new float[n];
            float ph = 0f; var rnd = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / 44100f;
                float env = Mathf.Pow(1f - t, 2f) * Mathf.Min(1f, i / 150f);
                float v = noise ? (float)(rnd.NextDouble() * 2 - 1) * 0.6f + Mathf.Sin(ph) * 0.4f : Mathf.Sin(ph);
                d[i] = v * env * vol;
            }
            var c = AudioClip.Create("t", n, 1, 44100, false); c.SetData(d, 0); return c;
        }

        AudioClip Arp(float[] freqs, float each, float vol)
        {
            int per = (int)(44100 * each), n = per * freqs.Length;
            var d = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                float ph = 0f;
                for (int i = 0; i < per; i++)
                {
                    ph += 2f * Mathf.PI * freqs[k] / 44100f;
                    float t = i / (float)per;
                    d[k * per + i] = Mathf.Sin(ph) * Mathf.Pow(1f - t, 1.5f) * Mathf.Min(1f, i / 120f) * vol;
                }
            }
            var c = AudioClip.Create("a", n, 1, 44100, false); c.SetData(d, 0); return c;
        }

        void MakeSounds()
        {
            sndTap = Tone(1100f, 700f, 0.07f, 0.5f);
            sndLand = Tone(420f, 300f, 0.08f, 0.5f);
            sndBox = Arp(new[] { 660f, 880f, 1320f }, 0.07f, 0.5f);
            sndFall = Tone(260f, 80f, 0.35f, 0.5f, true);
            sndFail = Tone(300f, 110f, 0.45f, 0.55f);
            sndWin = Arp(new[] { 523f, 659f, 784f, 1047f }, 0.11f, 0.55f);
            sndBlocked = Tone(180f, 150f, 0.09f, 0.45f);
        }

        void Play(AudioClip c, float v = 1f) { if (soundEnabled && c != null) audioSrc.PlayOneShot(c, v); }

        // =============================================================== ui
        SpriteRenderer MakeSprite(string name, Transform parent, Sprite spr, Color col, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.color = col; sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer MakePanel(string name, Transform parent, Vector2 size, Color col, int order)
        {
            var sr = MakeSprite(name, parent, sprRound, col, order);
            sr.drawMode = SpriteDrawMode.Sliced; sr.size = size;
            return sr;
        }

        Btn AddButton(string label, Vector2 pos, Vector2 size, Color col, int order, Transform parent, Func<bool> visible, Action click)
        {
            var sr = MakePanel("Btn " + label, parent, size, col, order);
            sr.transform.position = new Vector3(pos.x, pos.y, 0f);
            var b = new Btn { t = sr.transform, sr = sr, size = size, label = label, visible = visible, click = click };
            sr.gameObject.SetActive(false);
            buttons.Add(b);
            return b;
        }

        void BuildUi()
        {
            var ui = new GameObject("Ui").transform;
            progressTrack = MakePanel("Progress track", ui, new Vector2(5.8f, 0.18f), new Color32(27, 62, 63, 255), 599);
            
            progressFill = MakePanel("Progress fill", ui, new Vector2(0, 0.18f), new Color32(149, 234, 187, 255), 600);
            BuildTools(ui);
            BuildLockedBaskets(ui);

            overlay = new GameObject("Overlay").transform;
            var dim = MakeSprite("Dim", overlay, sprCircle, new Color(0, 0, 0, 0.62f), 3000);
            dim.drawMode = SpriteDrawMode.Sliced; dim.size = new Vector2(60f, 60f);
            dim.sprite = sprRound;
            MakePanel("Brass frame", overlay, new Vector2(7.75f, 5.8f), Brass, 3001).transform.position = new Vector3(0, 2.05f, 0);
            MakePanel("Enamel", overlay, new Vector2(7.52f, 5.55f), Midnight, 3002).transform.position = new Vector3(0, 2.05f, 0);
            IconButton(Symbol.Play, new Vector2(0f, 0.9f), new Vector2(2.05f, 1.45f), Enamel, 3003, overlay,
                () => InGame && state == State.Won, NextAfterWin);
            IconButton(Symbol.Video, new Vector2(1.41f, 0.72f), new Vector2(2.05f, 1.25f), Enamel, 3003, overlay,
                () => InGame && state == State.Failed && overlay.gameObject.activeSelf && FindRewardLane(true) >= 0, RequestRecovery);
            IconButton(Symbol.Retry, new Vector2(-1.41f, 0.72f), new Vector2(1.84f, 1.25f), Brick, 3003, overlay,
                () => InGame && state == State.Failed && overlay.gameObject.activeSelf, () => LoadLevel(levelNumber));
            overlay.gameObject.SetActive(false);
        }

        void RefreshButtons()
        {
            foreach (var button in buttons)
                button.t.gameObject.SetActive(button.visible());
            if (progressTrack != null)
            {
                progressTrack.enabled = InGame;
                progressFill.enabled = InGame && progressFill.size.x > 0f;
            }
        }

        void OnGUI()
        {
            if (cam == null || Event.current.type != EventType.Repaint) return;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            }
            if (shellScreen == ShellScreen.Shop) { DrawShop(); return; }
            float progressY = progressTrack.transform.position.y;
            if (InGame && state == State.Playing)
            {
                DrawLabel(levelLabel, new Vector3(0, progressY + 1.12f, 0), 0.030f, Ink, 4.6f);
            }
            DrawShell();
            DrawTools();
             foreach (var b in buttons)
                if (b.visible()) DrawLabel(b.label, b.t.position, 0.026f, Color.white, b.size.x - 0.25f);
            if (InGame && overlay != null && overlay.gameObject.activeSelf)
            {
                DrawLabel(overlayTitle, new Vector3(0, 3.6f, 0), 0.037f, Color.white, 6.8f);
                DrawLabel(overlayHint, new Vector3(0, 2.5f, 0), 0.022f, new Color(1, 1, 1, 0.8f), 6.8f);
                DrawChestReward();
            }
        }

        void DrawLabel(string text, Vector3 worldPos, float sizeFrac, Color col, float worldWidth)
        {
            if (string.IsNullOrEmpty(text)) return;
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            labelStyle.fontSize = Mathf.Max(10, (int)(Screen.height * sizeFrac));
            float width = worldWidth * Screen.height / (2f * halfH);
            measuredLabel.text = text;
            float textWidth = labelStyle.CalcSize(measuredLabel).x;
            if (textWidth > width)
                labelStyle.fontSize = Mathf.Max(1, Mathf.FloorToInt(labelStyle.fontSize * width / textWidth));
            float height = labelStyle.fontSize * 1.6f;
            var rect = new Rect(sp.x - width * 0.5f, Screen.height - sp.y - height * 0.5f, width, height);
            labelStyle.normal.textColor = new Color(0, 0, 0, col.grayscale > 0.6f ? 0.55f * col.a : 0f);
            if (col.grayscale > 0.6f)
                GUI.Label(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), measuredLabel, labelStyle);
            labelStyle.normal.textColor = col;
            GUI.Label(rect, measuredLabel, labelStyle);
        }

        // =============================================================== level loading
        LevelData FindLevel(int n)
        {
            if (pack == null || pack.levels.Count == 0) return Generator.Make(n, 40);
            LevelData best = null;
            foreach (var lv in pack.levels) if (lv.index <= n && (best == null || lv.index > best.index)) best = lv;
            return best != null ? best : pack.levels[0];
        }

        void LoadLevel(int n, ShellScreen screen = ShellScreen.Game)
        {
            ShowShell(ShellScreen.Game);
            StopAllCoroutines();
            ResetToolPulses();
            pending = 0;
            rewardInFlight = false;
            rewardRequestId++;
            chestCoins = chestToolAmount = 0;
            chestTool = -1;
            toolAnimations = 0;
            departingPlates = 0;
            if (world != null) { world.gameObject.SetActive(false); Destroy(world.gameObject); }
            world = new GameObject("World").transform;
            plates.Clear(); screws.Clear(); boxViews.Clear();
            for (int i = 0; i < slotViews.Length; i++) slotViews[i] = null;
            tray = null;
            bufferOcc = Array.Empty<ScrewView>();
            overlay.gameObject.SetActive(false);
            progressFill.size = new Vector2(0, 0.18f);
            progressFill.enabled = false;

            levelNumber = n;
            breakMode = false;
            shopNotice = "";
            L = FindLevel(n);
            geo = new LevelGeo(L);
            cap = Const.DefaultBuffer;
            sim = new Sim(geo, cap);
            state = State.Playing;
            RefreshButtons();

            // workbench behind the board
            var bench = MakePanel("Bench", world, new Vector2(8.6f, 7.35f), Brass, -50);
            bench.transform.position = new Vector3(0, BoardCy - 0.25f, 0);
            MakePanel("Enamel board", bench.transform, new Vector2(8.36f, 7.11f), Midnight, -49);
            MakePanel("Board inset", bench.transform, new Vector2(8.06f, 6.82f), new Color32(68, 103, 124, 255), -48);

            for (int i = 0; i < L.plates.Count; i++)
            {
                var pd = L.plates[i];
                var pv = new PlateView { index = i };
                var go = new GameObject("Plate" + i);
                go.transform.SetParent(world, false);
                go.transform.position = new Vector3(pd.x, pd.y + BoardCy, 0f);
                go.transform.rotation = Quaternion.Euler(0, 0, pd.angle);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GlassShape(pd, i); sr.drawMode = SpriteDrawMode.Sliced; sr.size = new Vector2(pd.w, pd.h);
                sr.color = GlassColors[pd.tint % GlassColors.Length]; sr.sortingOrder = i * 10;
                var sh = MakeSprite("Shadow", go.transform, sr.sprite, new Color(0.05f, 0.08f, 0.15f, 0.35f), i * 10 - 1);
                sh.drawMode = SpriteDrawMode.Sliced; sh.size = sr.size;
                sh.transform.position = go.transform.position + new Vector3(0.10f, -0.16f, 0f);
                pv.t = go.transform; pv.remaining = 0;
                plates.Add(pv);
            }

            for (int i = 0; i < L.screws.Count; i++)
            {
                var sd = L.screws[i];
                var pv = plates[sd.plate];
                pv.remaining++;
                Vector3 wp = new Vector3(sd.x, sd.y + BoardCy, 0f);
                var hole = MakeSprite("Hole", pv.t, sprCircle, new Color(0.08f, 0.06f, 0.12f, 0.65f), sd.plate * 10 + 1);
                hole.transform.position = wp; hole.transform.localScale = Vector3.one * 0.72f;
                var sg = new GameObject("Screw" + i);
                sg.transform.SetParent(world, false);
                sg.transform.position = wp;
                sg.transform.rotation = Quaternion.Euler(0, 0, (i * 37) % 90);
                var ssr = sg.AddComponent<SpriteRenderer>();
                ssr.sprite = sprScrew; ssr.color = Palette[sd.color]; ssr.sortingOrder = sd.plate * 10 + 3;
                screws.Add(new ScrewView { id = i, color = sd.color, t = sg.transform, sr = ssr, plate = pv });
            }
            foreach (var pv in plates)
            {
                pv.all = pv.t.GetComponentsInChildren<SpriteRenderer>();
                SetPlateVisible(pv, false);
            }
            RevealPlayablePlates(false);

            BuildTray();
            SyncBoxes();
            RefreshHud();
            ShowShell(screen);
        }

        // =============================================================== boxes, buffer
        void SyncBoxes()
        {
            sim.ActiveBoxes(activeTmp);
            foreach (int idx in activeTmp) EnsureBox(idx);

        }

        BoxView EnsureBox(int idx)
        {
            BoxView bv;
            if (boxViews.TryGetValue(idx, out bv)) return bv;
            int extraLane = sim.ExtraLaneOf(idx);
            int slot = extraLane >= 0 ? Const.ActiveBoxes + extraLane : Const.ActiveBoxes;
            if (extraLane < 0)
            {
                for (int i = 0; i < Const.ActiveBoxes; i++)
                    if (slotViews[i] == null || sim.fill[slotViews[i].index] >= Const.BoxSize) { slot = i; break; }
                if (slot == Const.ActiveBoxes) slot = 0;
            }
            BoxView previous = slotViews[slot];

            bv = new BoxView { index = idx, color = L.boxes[idx], basePos = new Vector3(BoxX[slot], BoxY, 0f) };
            var go = new GameObject("Box" + idx);
            go.transform.SetParent(world, false);
            go.transform.position = bv.basePos;
            bv.t = go.transform;
            MakePanel("Body", go.transform, new Vector2(1.93f, 1.58f), Palette[bv.color], 400);
            var inner = MakePanel("Inner", go.transform, new Vector2(1.59f, 1.25f), Color.Lerp(Palette[bv.color], Color.white, 0.75f), 401);
            inner.transform.localPosition = new Vector3(0, 0.05f, 0);
            for (int k = 0; k < 3; k++)
            {
                var h = MakeSprite("Slot", go.transform, sprCircle, Color.Lerp(Palette[bv.color], new Color32(49, 66, 95, 255), 0.52f), 402);
                h.transform.localPosition = BoxView.SlotOffset(k); h.transform.localScale = Vector3.one * 0.53f;
            }
            slotViews[slot] = bv;
            boxViews[idx] = bv;
            if (previous == null)
                bv.ready = true;
            else
                StartCoroutine(SlideIn(bv, previous));
            return bv;
        }

        IEnumerator SlideIn(BoxView box, BoxView previous)
        {
            Transform t = box.t;
            t.gameObject.SetActive(false);
            while (previous != null && !previous.departed) yield return null;
            t.localScale = Vector3.one * 0.78f;
            t.gameObject.SetActive(true);
            yield return ScaleBox(t, 0.78f, 1f, 0.15f);
            box.ready = true;
        }

        IEnumerator ScaleBox(Transform t, float from, float to, float duration)
        {
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                t.localScale = Vector3.one * Mathf.Lerp(from, to, EaseOutCubic(elapsed / duration));
                yield return null;
            }
            t.localScale = Vector3.one * to;
        }

        float BufSpacing() { return Mathf.Min(0.98f, 7.5f / cap); }
        Vector3 BufferPos(int i) { return new Vector3((i - (cap - 1) * 0.5f) * BufSpacing(), BufY, 0f); }

        void BuildTray()
        {
            if (tray != null) Destroy(tray.gameObject);
            tray = new GameObject("Tray").transform;
            tray.SetParent(world, false);
            trayBody = MakePanel("TrayBody", tray, new Vector2(BufSpacing() * cap + 0.40f, 0.88f), new Color32(23, 54, 58, 255), 400);
            trayBody.transform.position = new Vector3(0, BufY, 0);
            for (int i = 0; i < cap; i++)
            {
                var h = MakeSprite("BufSlot", tray, sprCircle, new Color(1, 1, 1, 0.12f), 402);
                h.transform.position = BufferPos(i); h.transform.localScale = Vector3.one * 0.69f;
            }
            var old = bufferOcc;
            bufferOcc = new ScrewView[cap];
            for (int i = 0; i < old.Length && i < cap; i++)
            {
                bufferOcc[i] = old[i];
                if (old[i] != null && !old[i].busy) old[i].t.position = BufferPos(i);
            }
        }

        void AddSlot()
        {
            if (cap >= MaxCap || state == State.Won) return;
            cap++; sim.cap = cap;
            BuildTray();
            Play(sndBox, 0.6f);
            RefreshButtons();
            StartCoroutine(Pulse(trayBody.transform, 1.08f, 0.25f));
        }

        // =============================================================== input and game flow
        void Update()
        {
            if (cam == null) return;
            FitCamera();
            var phase = ReadPointer();
            if (rewardInFlight)
            {
                if (Time.realtimeSinceStartup < rewardDeadline) return;
                rewardInFlight = false;
                if (state == State.Failed) overlayHint = "VIDEO UNAVAILABLE";
                else shopNotice = "VIDEO UNAVAILABLE";
            }
            if (shellScreen == ShellScreen.Shop) { UpdateCatalogInput(phase, pointerPosition); return; }
            if (phase == PointerPhase.Pressed)
            {
                Vector3 w = cam.ScreenToWorldPoint(pointerPosition);
                foreach (var b in buttons)
                {
                    if (!b.visible()) continue;
                    Vector2 d = (Vector2)b.t.position - (Vector2)w;
                    if (Mathf.Abs(d.x) <= b.size.x * 0.5f && Mathf.Abs(d.y) <= b.size.y * 0.5f)
                    {
                        Play(sndTap, 0.6f);

                        b.click();
                        return;
                    }
                }
                HandleTap(w);
            }
        }

        enum PointerPhase { None, Pressed, Held, Released, Cancelled }

        PointerPhase ReadPointer()
        {
            int touches = Input.touchCount;
            for (int i = 0; i < touches; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (pointerFinger >= 0 ? touch.fingerId != pointerFinger : touch.phase != TouchPhase.Began) continue;
                pointerPosition = touch.position;
                if (touch.phase == TouchPhase.Began) { pointerFinger = touch.fingerId; return PointerPhase.Pressed; }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    pointerFinger = -1;
                    return touch.phase == TouchPhase.Ended ? PointerPhase.Released : PointerPhase.Cancelled;
                }
                return PointerPhase.Held;
            }
            if (pointerFinger >= 0) { pointerFinger = -1; return PointerPhase.Cancelled; }
            if (touches > 0) return PointerPhase.None;
            pointerPosition = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) return PointerPhase.Pressed;
            if (Input.GetMouseButtonUp(0)) return PointerPhase.Released;
            return Input.GetMouseButton(0) ? PointerPhase.Held : PointerPhase.None;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            pointerFinger = -1;
            shopPressed = false;
        }

        void HandleTap(Vector2 w)
        {
            if (!InGame || state != State.Playing || toolAnimations > 0) return;
            if (breakMode)
            {
                TryBreakAt(w);
                return;
            }
            ScrewView selected = null;
            float nearest = 0.43f;
            foreach (var sv in screws)
            {
                if (sv.removed || sv.busy || !sv.plate.revealed) continue;
                var origin = L.screws[sv.id];
                float d = Vector2.Distance(w, new Vector2(origin.x, origin.y + BoardCy));
                if (d >= nearest) continue;
                nearest = d;
                selected = sv;
            }
            if (selected == null) return;
            if (CanUnscrew(selected.id)) RemoveScrew(selected);
            else Play(sndBlocked, 0.6f);
        }

        bool CanUnscrew(int screw)
        {
            if (!sim.IsFree(screw) || (geo.CoverMask[screw] & departingPlates) != 0) return false;
            if (departingPlates == 0) return true;
            var head = L.screws[screw];
            for (int i = head.plate + 1; i < plates.Count; i++)
            {
                if ((departingPlates & geo.PlateMask[i]) == 0) continue;
                var plate = plates[i].t;
                if (plate == null) continue;
                Vector3 p = plate.position;
                if (Geo.GlassCoversHead(L.plates[i], ShapeStyle(i), head.x, head.y, p.x, p.y - BoardCy, plate.eulerAngles.z))
                    return false;
            }
            return true;
        }

        void RemoveScrew(ScrewView sv)
        {
            var flushes = new List<Sim.Flush>();
            int box, slot;
            if (!sim.TryRemove(sv.id, flushes, out box, out slot))
            {
                StartCoroutine(Wiggle(sv.t));
                StartCoroutine(Pulse(trayBody.transform, 1.06f, 0.2f));
                Play(sndBlocked, 0.7f);
                return;
            }
            sv.removed = true; sv.busy = true;
            sv.plate.remaining--;
            Play(sndTap, 0.8f);

            if (box >= 0)
            {
                var bv = EnsureBox(box);
                StartCoroutine(Fly(sv, bv.SlotWorld(slot), true, bv, () => OnBoxArrive(bv, sv)));
            }
            else
            {
                int bs = 0;
                while (bs < bufferOcc.Length && bufferOcc[bs] != null) bs++;
                bufferOcc[bs] = sv; sv.bufferSlot = bs;
                StartCoroutine(Fly(sv, BufferPos(bs), true, null, () => { sv.t.position = BufferPos(bs); Play(sndLand, 0.7f); }));
            }

            AnimateFlushes(flushes);

            SyncBoxes();
            if (sv.plate.remaining == 0) StartCoroutine(PlateFall(sv.plate));
            RevealPlayablePlates(true);
            RefreshHud();
            if (sim.Stuck())
            {
                state = State.Failed;
                RefreshButtons();
                StartCoroutine(ShowOverflow());
            }
        }

        void OnBoxArrive(BoxView bv, ScrewView sv)
        {
            if (bv.t == null) return;
            bv.arrived++;
            sv.t.SetParent(bv.t, true);
            sv.sr.sortingOrder = 410;
            Play(sndLand, 0.7f);
            StartCoroutine(Pulse(sv.t, 0.82f, 0.14f));
            if (bv.arrived >= Const.BoxSize && sim.fill[bv.index] >= Const.BoxSize && !bv.completing)
            {
                bv.completing = true;
                StartCoroutine(BoxComplete(bv));
            }
        }

        void RefreshHud()
        {
            levelLabel = levelNumber.ToString();
            int left = geo.N - Geo.PopCount(sim.removed);
            float width = 5.8f * (geo.N - left) / Mathf.Max(1, geo.N);
            progressFill.enabled = InGame && width > 0f;
            progressFill.size = new Vector2(width, 0.18f);
            progressFill.transform.localPosition = new Vector3((width - 5.8f) * 0.5f, progressTrack.transform.localPosition.y, 0);
        }

        void EvaluateEnd()
        {
            if (pending > 0 || state != State.Playing) return;
            if (sim.Won)
            {
                state = State.Won;
                int reward = AwardLevel();
                overlayTitle = reward > WinCoins ? ChapterNames[ChapterFor(levelNumber)] : "COMPLETE";
                overlayHint = reward > 0 ? "+" + reward : "";
                overlay.gameObject.SetActive(true);
                RefreshButtons();
                Play(sndWin, 0.9f);
                if (vibrationEnabled && Application.isMobilePlatform) Handheld.Vibrate();
                StartCoroutine(Confetti(new Vector3(0, 6f, 0), null, 36, true));
            }
        }

        IEnumerator ShowOverflow()
        {
            while (pending > 0) yield return null;
            overlayTitle = "BUFFER FULL";
            overlayHint = "";
            overlay.gameObject.SetActive(true);
            Play(sndFail, 0.9f);
            RefreshButtons();
        }

        // =============================================================== animations
        static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
        static float EaseInOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }

        IEnumerator FlushToBox(ScrewView screw, BoxView box, int slot)
        {
            pending++;
            while (screw.busy) yield return null;
            screw.busy = true;
            yield return Fly(screw, box.SlotWorld(slot), false, box, () => OnBoxArrive(box, screw));
            pending--;
            EvaluateEnd();
        }

        IEnumerator Fly(ScrewView sv, Vector3 dest, bool unscrew, BoxView box, Action onLand)
        {
            pending++;
            Transform t = sv.t;
            sv.sr.sortingOrder = 1500;

            float spin = t.eulerAngles.z;
            if (unscrew)
            {
                float d = 0.18f;
                for (float e = 0; e < d; e += Time.deltaTime)
                {
                    float k = EaseOutCubic(e / d);
                    t.localScale = Vector3.one * (1f + 0.32f * k);
                    t.rotation = Quaternion.Euler(0, 0, spin + 360f * k);
                    yield return null;
                }
                spin += 360f;
            }
            while (box != null && !box.ready) yield return null;
            Vector3 start = t.position;
            Vector3 mid = (start + dest) * 0.5f + Vector3.up * (1.6f + Vector3.Distance(start, dest) * 0.12f);
            float dur = 0.30f;
            float s0 = t.localScale.x;
            for (float e = 0; e < dur; e += Time.deltaTime)
            {
                float k = EaseInOutCubic(e / dur);
                Vector3 a = Vector3.Lerp(start, mid, k), b = Vector3.Lerp(mid, dest, k);
                t.position = Vector3.Lerp(a, b, k);
                t.localScale = Vector3.one * Mathf.Lerp(s0, 1f, k);
                t.rotation = Quaternion.Euler(0, 0, spin + 90f * k);
                yield return null;
            }
            t.position = dest; t.localScale = Vector3.one;
            t.rotation = Quaternion.Euler(0, 0, spin + 90f);
            sv.busy = false;
            if (onLand != null) onLand();
            pending--;
            EvaluateEnd();
        }

        IEnumerator Pulse(Transform t, float peak, float dur)
        {
            if (t == null) yield break;
            Vector3 baseScale = t.localScale;
            for (float e = 0; e < dur; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = Mathf.Sin(e / dur * Mathf.PI);
                t.localScale = baseScale * (1f + (peak - 1f) * k);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        IEnumerator Wiggle(Transform t)
        {
            if (t == null) yield break;
            Vector3 p = t.position;
            for (float e = 0; e < 0.28f; e += Time.deltaTime)
            {
                if (t == null) yield break;
                t.position = p + new Vector3(Mathf.Sin(e * 60f) * 0.07f * (1f - e / 0.28f), 0, 0);
                yield return null;
            }
            if (t != null) t.position = p;
        }

        IEnumerator PlateFall(PlateView pv)
        {
            pending++;
            departingPlates |= geo.PlateMask[pv.index];
            Play(sndFall, 0.6f);
            Transform t = pv.t;
            float vx = UnityEngine.Random.Range(-1.5f, 1.5f), vy = 1.2f, spin = UnityEngine.Random.Range(-140f, 140f);
            float life = 0.70f;
            for (float e = 0; e < life; e += Time.deltaTime)
            {
                if (t == null) break;
                vy -= 26f * Time.deltaTime;
                t.position += new Vector3(vx, vy, 0) * Time.deltaTime;
                t.Rotate(0, 0, spin * Time.deltaTime);
                yield return null;
            }
            if (t != null)
            {
                Color tint = t.GetComponent<SpriteRenderer>().color;
                if (effectsEnabled) StartCoroutine(Shatter(t.position, tint));
                Destroy(t.gameObject);
                Play(sndFall, 0.42f);
            }
            departingPlates &= ~geo.PlateMask[pv.index];
            RevealPlayablePlates(true);
            pending--;
            EvaluateEnd();
        }

        static Sprite MakeShardSprite()
        {
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (x < 3 + y / 4 || x > 22 - y / 2 || y < 2) continue;
                byte value = (byte)(190 + y * 2);
                pixels[y * size + x] = new Color32(value, value, value, 230);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        IEnumerator Shatter(Vector3 point, Color tint)
        {
            const int count = 9;
            var shards = new SpriteRenderer[count];
            var velocities = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                shards[i] = MakeSprite("Glass shard", world, sprShard, Color.Lerp(tint, Color.white, 0.32f), 520);
                shards[i].transform.position = point;
                shards[i].transform.localScale = Vector3.one * UnityEngine.Random.Range(0.18f, 0.44f);
                velocities[i] = new Vector3(UnityEngine.Random.Range(-3.4f, 3.4f), UnityEngine.Random.Range(1f, 4.7f), 0);
            }
            const float duration = 0.42f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    velocities[i].y -= 17f * Time.deltaTime;
                    shards[i].transform.position += velocities[i] * Time.deltaTime;
                    shards[i].transform.Rotate(0, 0, 240f * Time.deltaTime);
                    Color color = shards[i].color;
                    color.a = 1f - elapsed / duration;
                    shards[i].color = color;
                }
                yield return null;
            }
            foreach (var shard in shards) Destroy(shard.gameObject);
        }

        IEnumerator BoxComplete(BoxView bv)
        {
            pending++;
            yield return new WaitForSeconds(0.12f);
            Transform t = bv.t;
            Play(sndBox, 0.9f);
            if (t != null)
            {
                var lid = MakePanel("Enamel lid", t, new Vector2(1.80f, 1.43f), Palette[bv.color], 420);
                var seal = MakeSprite("Brass seal", lid.transform, NavigationSprite(Symbol.Journal), Color.white, 421);
                seal.transform.localScale = Vector3.one * 0.82f;
                for (float elapsed = 0; elapsed < 0.31f; elapsed += Time.deltaTime)
                {
                    float p = Mathf.Clamp01(elapsed / 0.31f);
                    lid.transform.localScale = new Vector3(1, EaseOutCubic(Mathf.Min(1f, p * 2.6f)), 1);
                    t.localScale = Vector3.one * (1f - EaseInOutCubic(Mathf.Max(0f, (p - 0.40f) / 0.60f)));
                    yield return null;
                }
            }
            for (int i = 0; i < slotViews.Length; i++) if (slotViews[i] == bv) slotViews[i] = null;
            if (t != null)
            {
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
            bv.departed = true;
            RefreshButtons();
            boxViews.Remove(bv.index);
            pending--;
            EvaluateEnd();
        }

        IEnumerator Confetti(Vector3 pos, Color? tint, int count, bool rain)
        {
            if (!effectsEnabled) yield break;
            var parts = new List<Transform>(); var vel = new List<Vector3>(); var spin = new List<float>(); var srs = new List<SpriteRenderer>();
            for (int i = 0; i < count; i++)
            {
                var sr = MakeSprite("Confetti", world, sprCircle, tint.HasValue ? tint.Value : Palette[i % Palette.Length], 2500);
                sr.transform.localScale = new Vector3(0.2f, 0.14f, 1f);
                Vector3 p = rain ? new Vector3(UnityEngine.Random.Range(-4f, 4f), halfH, 0) : pos;
                sr.transform.position = p;
                parts.Add(sr.transform); srs.Add(sr);
                vel.Add(rain ? new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-3f, -6f), 0)
                             : new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(2f, 7f), 0));
                spin.Add(UnityEngine.Random.Range(-500f, 500f));
            }
            float life = rain ? 2.4f : 0.9f;
            for (float e = 0; e < life; e += Time.deltaTime)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i] == null) continue;
                    if (!rain) vel[i] += new Vector3(0, -16f * Time.deltaTime, 0);
                    parts[i].position += vel[i] * Time.deltaTime;
                    parts[i].Rotate(0, 0, spin[i] * Time.deltaTime);
                    var c = srs[i].color; c.a = rain ? 1f : 1f - e / life; srs[i].color = c;
                }
                yield return null;
            }
            foreach (var p in parts) if (p != null) Destroy(p.gameObject);
        }
    }
}
