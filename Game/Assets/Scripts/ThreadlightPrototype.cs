using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threadlight
{
    /// <summary>
    /// First gray-box build. The rules live in PuzzleBoard; this component only draws
    /// the board and forwards taps. It uses IMGUI temporarily so the prototype needs
    /// no imported art or UI packages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreadlightPrototype : MonoBehaviour
    {
        private const int Capacity = 4;
        private const int LevelCount = 10;
        private const string UnlockedKey = "Threadlight.UnlockedLevel";
        private static ThreadlightPrototype instance;

        private PuzzleBoard board;
        private int levelIndex;
        private int selectedSpool = -1;
        private string message = "Tap a spool, then tap where its top thread should go.";
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle spoolStyle;
        private GUIStyle smallStyle;
        private GUIStyle cellStyle;

        private static readonly Color[] ThreadColors =
        {
            new Color(0.91f, 0.27f, 0.31f), // coral
            new Color(0.24f, 0.65f, 0.55f), // sea green
            new Color(0.42f, 0.48f, 0.88f), // periwinkle
            new Color(0.96f, 0.69f, 0.25f)  // amber
        };

        // Archived 2026-11-15: BoltRush replaced this prototype; flip to true to restore the old slice.
        private static bool active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreatePrototype()
        {
            if (!active) return;

            if (FindObjectOfType<ThreadlightPrototype>() != null)
                return;

            var root = new GameObject("Threadlight Prototype");
            root.AddComponent<ThreadlightPrototype>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            LoadLevel(Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 0), 0, LevelCount - 1));
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float width = Screen.width;
            float height = Screen.height;
            float margin = width * 0.055f;

            Fill(new Rect(0f, 0f, width, height), new Color(0.96f, 0.93f, 0.87f));
            DrawText("THREADLIGHT", new Rect(margin, height * 0.045f, width - margin * 2f, height * 0.065f), titleStyle);
            string progress = board.IsSolved ? "Level complete" : "Level " + (levelIndex + 1) + " / " + LevelCount;
            DrawText(progress, new Rect(margin, height * 0.115f, width - margin * 2f, height * 0.045f), bodyStyle);
            DrawText(message, new Rect(margin, height * 0.18f, width - margin * 2f, height * 0.09f), smallStyle);

            DrawBoard(width, height, margin);
            DrawActions(width, height, margin);
        }

        private void DrawBoard(float width, float height, float margin)
        {
            int spoolCount = board.SpoolCount;
            float gap = width * 0.018f;
            float spoolWidth = (width - margin * 2f - gap * (spoolCount - 1)) / spoolCount;
            float top = height * 0.32f;
            float tubeHeight = height * 0.33f;
            float cellHeight = tubeHeight / Capacity;

            for (int i = 0; i < spoolCount; i++)
            {
                float x = margin + i * (spoolWidth + gap);
                Rect tube = new Rect(x, top, spoolWidth, tubeHeight);
                bool isSelected = selectedSpool == i;
                string label = isSelected ? "SELECTED" : (i + 1).ToString();
                Rect cap = new Rect(x, top, spoolWidth, cellHeight * 0.58f);

                Fill(tube, isSelected ? new Color(1f, 0.84f, 0.47f) : new Color(0.86f, 0.83f, 0.77f));
                Fill(cap, isSelected ? new Color(0.95f, 0.72f, 0.25f) : new Color(0.74f, 0.70f, 0.63f));
                GUI.Label(cap, label, smallStyle);

                IReadOnlyList<int> layers = board.GetSpool(i);
                for (int layer = 0; layer < layers.Count; layer++)
                {
                    float y = top + tubeHeight - (layer + 1) * cellHeight;
                    Rect cell = new Rect(x + spoolWidth * 0.09f, y, spoolWidth * 0.82f, cellHeight * 0.9f);
                    Fill(cell, ThreadColors[layers[layer]]);
                    GUI.Label(cell, ColorName(layers[layer]), cellStyle);
                }

                if (GUI.Button(tube, GUIContent.none, GUIStyle.none))
                    TapSpool(i);
            }
        }

        private void DrawActions(float width, float height, float margin)
        {
            float gap = width * 0.025f;
            float buttonWidth = (width - margin * 2f - gap * 2f) / 3f;
            float buttonHeight = height * 0.075f;
            float y = height * 0.77f;

            if (GUI.Button(new Rect(margin, y, buttonWidth, buttonHeight), "UNDO", spoolStyle))
            {
                if (!board.Undo())
                    message = "No move to undo yet.";
                selectedSpool = -1;
            }

            if (GUI.Button(new Rect(margin + buttonWidth + gap, y, buttonWidth, buttonHeight), "RESTART", spoolStyle))
            {
                board.Restart();
                selectedSpool = -1;
                message = "Level reset.";
            }

            string finalLabel = board.IsSolved ? (levelIndex == LevelCount - 1 ? "REPLAY" : "NEXT") : "HINT";
            if (GUI.Button(new Rect(margin + (buttonWidth + gap) * 2f, y, buttonWidth, buttonHeight), finalLabel, spoolStyle))
            {
                if (board.IsSolved)
                    LoadLevel(levelIndex == LevelCount - 1 ? 0 : levelIndex + 1);
                else
                    ShowHint();
            }

            DrawText("No timer. Take your time.", new Rect(margin, height * 0.88f, width - margin * 2f, height * 0.045f), smallStyle);
        }

        private void TapSpool(int index)
        {
            if (selectedSpool < 0)
            {
                if (board.GetSpool(index).Count == 0)
                {
                    message = "Choose a spool that has a thread on it.";
                    return;
                }

                selectedSpool = index;
                message = "Now choose an empty spool or one with the same top color.";
                return;
            }

            if (selectedSpool == index)
            {
                selectedSpool = -1;
                message = "Selection cleared.";
                return;
            }

            if (board.TryMove(selectedSpool, index, out int moved, out string reason))
            {
                message = "Moved " + moved + (moved == 1 ? " thread." : " threads.");
                selectedSpool = -1;
                if (board.IsSolved)
                {
                    PlayerPrefs.SetInt(UnlockedKey, Mathf.Max(PlayerPrefs.GetInt(UnlockedKey, 0), levelIndex + 1));
                    PlayerPrefs.Save();
                    message = levelIndex == LevelCount - 1 ? "You finished the prototype. Nice work!" : "Beautiful! Tap NEXT to continue.";
                }
            }
            else
            {
                selectedSpool = -1;
                message = reason;
            }
        }

        private void ShowHint()
        {
            if (board.IsSolved)
                return;

            message = "Move one matching top color at a time. Use an empty spool as a buffer.";
        }

        private void LoadLevel(int index)
        {
            levelIndex = index;
            board = LevelCatalog.Create(index);
            selectedSpool = -1;
            message = "Tap a spool, then tap where its top thread should go.";
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && cellStyle != null)
                return;

            float scale = Mathf.Clamp(Screen.width / 420f, 0.82f, 1.35f);
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(27f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.16f, 0.19f, 0.22f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.24f, 0.27f, 0.29f) }
            };
            spoolStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(15f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.12f, 0.14f, 0.17f) }
            };
            cellStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.08f, 0.09f, 0.11f) }
            };
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        }

        private static void DrawText(string text, Rect rect, GUIStyle style)
        {
            GUI.Label(rect, text, style);
        }

        private static string ColorName(int color)
        {
            switch (color)
            {
                case 0: return "CORAL";
                case 1: return "MINT";
                case 2: return "BLUE";
                default: return "GOLD";
            }
        }
    }

    /// <summary>Rules and undo history are kept separate from the temporary screen code.</summary>
    internal sealed class PuzzleBoard
    {
        private readonly int capacity;
        private readonly List<int>[] initial;
        private readonly Stack<List<int>[]> undo = new Stack<List<int>[]>();
        private List<int>[] spools;
        private readonly int colorCount;

        public int SpoolCount => spools.Length;

        public PuzzleBoard(int capacity, int colorCount, List<int>[] startingSpools)
        {
            this.capacity = capacity;
            this.colorCount = colorCount;
            initial = Clone(startingSpools);
            spools = Clone(startingSpools);
        }

        public IReadOnlyList<int> GetSpool(int index) => spools[index];

        public bool IsSolved
        {
            get
            {
                bool[] colorsFound = new bool[colorCount];
                int completed = 0;
                for (int i = 0; i < spools.Length; i++)
                {
                    List<int> spool = spools[i];
                    if (spool.Count == 0)
                        continue;
                    if (spool.Count != capacity)
                        return false;
                    int color = spool[0];
                    for (int j = 1; j < spool.Count; j++)
                        if (spool[j] != color)
                            return false;
                    if (color < 0 || color >= colorCount || colorsFound[color])
                        return false;
                    colorsFound[color] = true;
                    completed++;
                }
                return completed == colorCount;
            }
        }

        public bool TryMove(int from, int to, out int movedCount, out string reason)
        {
            movedCount = 0;
            reason = "That move is not possible. Try a different spool.";
            if (from < 0 || from >= spools.Length || to < 0 || to >= spools.Length || from == to)
                return false;

            List<int> source = spools[from];
            List<int> destination = spools[to];
            if (source.Count == 0)
            {
                reason = "That spool is empty. Choose one with a thread.";
                return false;
            }

            int color = source[source.Count - 1];
            if (destination.Count > 0 && destination[destination.Count - 1] != color)
            {
                reason = "Only matching colors can share a spool.";
                return false;
            }

            int group = TopGroupSize(source);
            int available = capacity - destination.Count;
            if (available <= 0)
            {
                reason = "That spool is full.";
                return false;
            }

            movedCount = Mathf.Min(group, available);
            undo.Push(Clone(spools));
            for (int i = 0; i < movedCount; i++)
            {
                int last = source.Count - 1;
                destination.Add(source[last]);
                source.RemoveAt(last);
            }
            reason = string.Empty;
            return true;
        }

        public bool Undo()
        {
            if (undo.Count == 0)
                return false;
            spools = undo.Pop();
            return true;
        }

        public void Restart()
        {
            spools = Clone(initial);
            undo.Clear();
        }

        private static int TopGroupSize(List<int> spool)
        {
            int color = spool[spool.Count - 1];
            int count = 0;
            for (int i = spool.Count - 1; i >= 0 && spool[i] == color; i--)
                count++;
            return count;
        }

        private static List<int>[] Clone(List<int>[] source)
        {
            var copy = new List<int>[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = new List<int>(source[i]);
            return copy;
        }
    }

    /// <summary>
    /// Levels are created by reversing legal moves from solved boards. This gives each
    /// generated board a known solution without storing generated content in assets.
    /// </summary>
    internal static class LevelCatalog
    {
        private const int Capacity = 4;

        public static PuzzleBoard Create(int levelIndex)
        {
            int colors = levelIndex < 2 ? 2 : (levelIndex < 7 ? 3 : 4);
            int emptySpools = colors == 2 ? 1 : 2;
            int movesToReverse = 3 + levelIndex * 2;
            var random = new System.Random(7331 + levelIndex * 97);
            List<int>[] stacks = null;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                stacks = CreateSolved(colors, emptySpools);
                for (int move = 0; move < movesToReverse; move++)
                    ReverseOneMove(stacks, random);
                if (!IsSolved(stacks, colors))
                    break;
            }

            return new PuzzleBoard(Capacity, colors, stacks ?? CreateSolved(colors, emptySpools));
        }

        private static List<int>[] CreateSolved(int colors, int emptySpools)
        {
            var stacks = new List<int>[colors + emptySpools];
            for (int color = 0; color < colors; color++)
            {
                stacks[color] = new List<int>(Capacity);
                for (int i = 0; i < Capacity; i++)
                    stacks[color].Add(color);
            }
            for (int i = colors; i < stacks.Length; i++)
                stacks[i] = new List<int>(Capacity);
            return stacks;
        }

        private static void ReverseOneMove(List<int>[] stacks, System.Random random)
        {
            var sources = new List<int>();
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i].Count > 0 && IsSingleColor(stacks[i]))
                    sources.Add(i);
            }
            if (sources.Count == 0)
                return;

            int sourceIndex = sources[random.Next(sources.Count)];
            List<int> source = stacks[sourceIndex];
            int color = source[source.Count - 1];
            var destinations = new List<int>();
            for (int i = 0; i < stacks.Length; i++)
            {
                if (i == sourceIndex || stacks[i].Count >= Capacity)
                    continue;
                if (stacks[i].Count == 0 || stacks[i][stacks[i].Count - 1] != color)
                    destinations.Add(i);
            }
            if (destinations.Count == 0)
                return;

            int destinationIndex = destinations[random.Next(destinations.Count)];
            List<int> destination = stacks[destinationIndex];
            int count = random.Next(1, Mathf.Min(source.Count, Capacity - destination.Count) + 1);
            for (int i = 0; i < count; i++)
            {
                int last = source.Count - 1;
                destination.Add(source[last]);
                source.RemoveAt(last);
            }
        }

        private static bool IsSingleColor(List<int> stack)
        {
            if (stack.Count == 0)
                return false;
            int color = stack[0];
            for (int i = 1; i < stack.Count; i++)
                if (stack[i] != color)
                    return false;
            return true;
        }

        private static bool IsSolved(List<int>[] stacks, int colors)
        {
            int full = 0;
            foreach (List<int> stack in stacks)
            {
                if (stack.Count == 0)
                    continue;
                if (stack.Count != Capacity || !IsSingleColor(stack))
                    return false;
                full++;
            }
            return full == colors;
        }
    }
}
