using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BoltRush
{
    public sealed partial class BoltRushGame
    {
        enum Tool { Slot, Store, Basket, Break }
        readonly int[] stock = new int[4];
        readonly Btn[] toolButtons = new Btn[4];
        readonly SpriteRenderer[] toolBadges = new SpriteRenderer[4];
        readonly Coroutine[] toolPulses = new Coroutine[4];
        readonly string[] toolCounts = new string[4];
        readonly Sprite[] illustratedTools = new Sprite[4];
        static readonly string[] ToolArtNames = { "slot", "store", "basket", "mallet" };
        static readonly int[] ToolPrices = { 30, 40, 45, 55 };
        static readonly string[] ToolNames = { "EXTRA SLOT", "STORE BUFFER", "EXTRA BOX", "BREAK GLASS" };
        bool breakMode;
        int toolAnimations;

        bool CanUseTools => InGame && state == State.Playing;

        void BuildTools(Transform root)
        {
            for (int i = 0; i < toolButtons.Length; i++)
            {
                Tool tool = (Tool)i;
                var art = LoadIllustration(ToolArtNames[i]);
                illustratedTools[i] = art != null ? art : MakeToolIcon(tool);
                if (tool == Tool.Basket) continue;
                float x = (i == (int)Tool.Break ? 1 : i - 1) * 2.46f;
                var button = AddButton("", new Vector2(x, BtnY), new Vector2(2.05f, 1.59f),
                    new Color32(244, 209, 135, 255), 600, root, () => CanUseTools, () => UseTool(tool));
                toolButtons[i] = button;
                var icon = MakeSprite("Tool icon", button.t, illustratedTools[i], art != null ? Color.white : Ink, 601);
                icon.transform.localScale = Vector3.one * 1.49f;
                toolBadges[i] = MakeSprite("Quantity badge", button.t, sprCircle, new Color32(255, 244, 198, 255), 602);
                toolBadges[i].transform.localPosition = new Vector3(0.72f, 0.55f, 0);
                toolBadges[i].transform.localScale = Vector3.one * 0.59f;
            }
        }

        void DrawTools()
        {
            foreach (var tag in basketVideoTags)
                if (tag != null && tag.gameObject.activeInHierarchy)
                    DrawLabel("AD", tag.transform.position, 0.009f, Color.white, 0.50f);
            if (!CanUseTools) return;
            for (int i = 0; i < stock.Length; i++)
                if (toolBadges[i] != null)
                    DrawLabel(toolCounts[i], toolBadges[i].transform.position, 0.012f, Ink, 0.50f);
        }

        void UseTool(Tool tool)
        {
            if (!CanUseTools) return;
            if (tool == Tool.Break && breakMode) { breakMode = false; return; }
            breakMode = false;
            shopNotice = "";
            if (stock[(int)tool] == 0) { shopNotice = "OUT OF TOOLS"; return; }
            if (pending > 0) { shopNotice = "WAIT FOR THE SCREWS TO LAND"; return; }

            switch (tool)
            {
                case Tool.Slot:
                    if (cap >= MaxCap) return;
                    AddSlot();
                    break;
                case Tool.Store:
                    if (sim.bufTotal == 0) return;
                    sim.StoreBuffer();
                    for (int i = 0; i < bufferOcc.Length; i++)
                    {
                        if (bufferOcc[i] == null) continue;
                        StartCoroutine(StoreScrew(bufferOcc[i].t, i * 0.035f));
                        bufferOcc[i] = null;
                    }
                    break;
                case Tool.Break:
                    breakMode = true;
                    AnimateTool(tool);
                    return;
                default:
                    return;
            }
            Consume(tool);
            AnimateTool(tool);
        }

        void AnimateTool(Tool tool)
        {
            int index = (int)tool;
            if (toolPulses[index] != null) StopCoroutine(toolPulses[index]);
            var art = toolButtons[index].t.GetChild(0);
            art.localScale = Vector3.one * 1.49f;
            toolPulses[index] = StartCoroutine(Pulse(art, 1.22f, 0.22f));
        }

        void ResetToolPulses()
        {
            for (int i = 0; i < toolPulses.Length; i++)
            {
                toolPulses[i] = null;
                if (toolButtons[i] != null) toolButtons[i].t.GetChild(0).localScale = Vector3.one * 1.49f;
            }
        }

        IEnumerator StoreScrew(Transform screw, float delay)
        {
            pending++;
            toolAnimations++;
            if (delay > 0) yield return new WaitForSeconds(delay);
            Vector3 origin = screw.position;
            Vector3 scale = screw.localScale;
            const float duration = 0.23f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float progress = t / duration;
                screw.position = origin + Vector3.down * (0.16f * progress);
                screw.localScale = scale * (1f - progress);
                screw.Rotate(0f, 0f, 320f * Time.deltaTime);
                yield return null;
            }
            Destroy(screw.gameObject);
            toolAnimations--;
            pending--;
            EvaluateEnd();
        }

        void Consume(Tool tool)
        {
            stock[(int)tool]--;
            SaveWallet();
        }

        void TryBreakAt(Vector2 point)
        {
            if (pending > 0) return;
            for (int i = plates.Count - 1; i >= 0; i--)
            {
                var plate = plates[i];
                if (!plate.revealed || plate.remaining == 0 || !Geo.GlassContains(L.plates[i], ShapeStyle(i), point.x, point.y - BoardCy, 0f))
                    continue;
                breakMode = false;
                Consume(Tool.Break);
                StartCoroutine(HammerStrike(i, point));
                return;
            }
        }

        IEnumerator HammerStrike(int i, Vector2 point)
        {
                pending++;
                toolAnimations++;
                var plate = plates[i];
                var hammer = MakeSprite("Hammer strike", world, illustratedTools[(int)Tool.Break], Color.white, 1700).transform;
                hammer.localScale = Vector3.one * 1.95f;
                hammer.position = (Vector3)point + new Vector3(0.42f, 0.63f, 0);
                for (float t = 0f; t < 0.18f; t += Time.deltaTime)
                {
                    float p = EaseOutCubic(t / 0.18f);
                    hammer.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(48f, -30f, p));
                    hammer.position = (Vector3)point + new Vector3(0.42f, Mathf.Lerp(0.95f, 0.18f, p), 0);
                    yield return null;
                }
                Destroy(hammer.gameObject);
                var flushes = new List<Sim.Flush>();
                sim.BreakPlate(i, flushes);
                foreach (var screw in screws)
                {
                    if (screw.plate != plate || screw.removed) continue;
                    screw.removed = true;
                    Destroy(screw.t.gameObject);
                }
                plate.remaining = 0;
                AnimateFlushes(flushes);
                SyncBoxes();
                StartCoroutine(PlateFall(plate));
                RevealPlayablePlates(true);
                RefreshHud();
                toolAnimations--;
                pending--;
                EvaluateEnd();
        }

        // The provider invokes the callback on Unity's main thread with the SDK's reward result.
        public static System.Action<System.Action<bool>> ShowRewardedVideo { get; set; }
        bool rewardInFlight;
        int rewardRequestId;
        float rewardDeadline;

        void BuildLockedBaskets(Transform root)
        {
            for (int i = 0; i < Sim.ExtraLaneCount; i++)
            {
                int lane = i;
                int index = Const.ActiveBoxes + i;
                var button = AddButton("", new Vector2(BoxX[index], BoxY), new Vector2(1.93f, 1.58f),
                    new Color32(222, 168, 66, 255), 400, root,
                    () => (CanUseTools || shellScreen == ShellScreen.Home) && sim != null && slotViews[index] == null,
                    () => { if (CanUseTools) RequestBasket(lane, false); });
                var icon = MakeSprite("Locked basket art", button.t, illustratedTools[(int)Tool.Basket], Color.white, 402);
                icon.transform.localScale = Vector3.one * 1.37f;
                var tag = MakePanel("Video tag", button.t, new Vector2(0.61f, 0.28f), Teal, 403);
                tag.transform.localPosition = new Vector3(0.56f, -0.52f, 0);
                basketVideoTags[i] = tag;
            }
        }

        readonly SpriteRenderer[] basketVideoTags = new SpriteRenderer[Sim.ExtraLaneCount];

        int FindRewardLane(bool matching)
        {
            for (int i = 0; i < Sim.ExtraLaneCount; i++)
                if (slotViews[Const.ActiveBoxes + i] == null && sim.FindExtraBox(i, matching) >= 0) return i;
            return -1;
        }

        void RequestRecovery()
        {
            int lane = FindRewardLane(true);
            if (lane >= 0) RequestBasket(lane, true);
        }

        void RequestBasket(int lane, bool recovery)
        {
            if (rewardInFlight || pending > 0 || sim.FindExtraBox(lane, recovery) < 0) return;
            if (ShowRewardedVideo == null)
            {
                if (recovery) overlayHint = "VIDEO UNAVAILABLE";
                else shopNotice = "VIDEO UNAVAILABLE";
                return;
            }
            Sim requestedBoard = sim;
            rewardInFlight = true;
            int request = ++rewardRequestId;
            rewardDeadline = Time.realtimeSinceStartup + 120f;
            try
            {
                ShowRewardedVideo(earned =>
                {
                    if (!rewardInFlight || request != rewardRequestId) return;
                    rewardInFlight = false;
                    if (!earned || sim != requestedBoard) return;
                    if (!InGame || (recovery ? state != State.Failed : state != State.Playing)) return;
                    if (!OpenMatchingBox(lane, recovery)) return;
                    if (recovery)
                    {
                        state = State.Playing;
                        overlay.gameObject.SetActive(false);
                    }
                    shopNotice = "";
                    RefreshButtons();
                });
            }
            catch (System.Exception error)
            {
                rewardInFlight = false;
                Debug.LogWarning("Rewarded provider could not start: " + error.Message);
                if (recovery) overlayHint = "VIDEO UNAVAILABLE";
                else shopNotice = "VIDEO UNAVAILABLE";
            }
        }

        bool OpenMatchingBox(int lane, bool mustMatchBuffer)
        {
            var flushes = new List<Sim.Flush>();
            int box = sim.OpenExtraBox(lane, mustMatchBuffer, flushes);
            if (box < 0) return false;
            EnsureBox(box);
            AnimateFlushes(flushes);
            SyncBoxes();
            return true;
        }

        static Sprite LoadIllustration(string name)
        {
            var texture = Resources.Load<Texture2D>("Workshop/" + name);
            return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width, 0, SpriteMeshType.FullRect);
        }

        void AnimateFlushes(List<Sim.Flush> flushes)
        {
            foreach (var flush in flushes)
            {
                BoxView box = EnsureBox(flush.box);
                ScrewView source = null;
                if (flush.fromReserve)
                {
                    StartCoroutine(SeatStoredScrew(box, flush.color, flush.slot));
                    continue;
                }
                else
                {
                    for (int i = 0; i < bufferOcc.Length; i++)
                    {
                        if (bufferOcc[i] == null || bufferOcc[i].color != flush.color) continue;
                        source = bufferOcc[i];
                        bufferOcc[i] = null;
                        break;
                    }
                }
                if (source != null) StartCoroutine(FlushToBox(source, box, flush.slot));
            }
        }

        IEnumerator SeatStoredScrew(BoxView box, int color, int slot)
        {
            pending++;
            while (!box.ready) yield return null;
            var sprite = MakeSprite("Stored screw", box.t, sprScrew, Palette[color], 410);
            sprite.transform.localPosition = BoxView.SlotOffset(slot);
            sprite.transform.localScale = Vector3.one * 0.18f;
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                sprite.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 1f, EaseOutCubic(t / 0.15f));
                yield return null;
            }
            sprite.transform.localScale = Vector3.one;
            OnBoxArrive(box, new ScrewView { t = sprite.transform, sr = sprite, color = color, removed = true });
            pending--;
            EvaluateEnd();
        }

        static Sprite MakeToolIcon(Tool tool)
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x - 23.5f, py = y - 23.5f;
                float a = Mathf.Abs(px), b = Mathf.Abs(py);
                bool plus = (a < 2.4f && b < 9f) || (b < 2.4f && a < 9f);
                bool ring = Mathf.Abs(Mathf.Sqrt(px * px + py * py) - 17f) < 2f;
                bool mark;
                switch (tool)
                {
                    case Tool.Slot:
                        mark = ring || plus;
                        break;
                    case Tool.Store:
                        mark = (a < 17f && py > -15f && py < -10f) || (a > 13f && a < 17f && py > -15f && py < 0f) ||
                            (a < 2.5f && py > -4f && py < 17f) || (py > 4f && py < 12f && Mathf.Abs(a - (14f - py)) < 2.7f);
                        break;
                    case Tool.Basket:
                        mark = (a < 18f && b < 14f && (a > 14f || b > 10f)) || plus;
                        break;
                    default:
                        float u = (px + py) * 0.7071f, v = (py - px) * 0.7071f;
                        mark = (Mathf.Abs(u) < 3.3f && v > -17f && v < 8f) || (Mathf.Abs(u) < 14f && v > 5f && v < 14f);
                        break;
                }
                if (mark) pixels[y * size + x] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
