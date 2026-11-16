using UnityEngine;

namespace BoltRush
{
    public sealed partial class BoltRushGame
    {
        const int CampaignEnd = 150;
        int chestTool = -1, chestToolAmount, chestCoins;
        static readonly int[] ChapterEnds = { 15, 35, 60, 90, 120, CampaignEnd };
        static readonly string[] ChapterNames =
        {
            "The Dark Workshop", "The Lantern", "The Clock", "The Observatory", "The Rose Window", "First Light"
        };
        static readonly string[] ChapterStories =
        {
            "A storm broke the workshop's windows. Find the first shards among the old tools.",
            "A lantern in the window brings the neighbours back to the workshop.",
            "Beneath the glass is a clock. Its hands stopped on the night of the storm.",
            "Follow the clockmaker's notes to the glass dome above the roof.",
            "Each restored colour reveals a piece of the workshop's final window.",
            "Put the last colours in place and light the workshop for the whole street."
        };
        static readonly Color32[] ChapterColors =
        {
            new Color32(237, 184, 99, 255), new Color32(131, 218, 188, 255),
            new Color32(165, 197, 245, 255), new Color32(215, 178, 247, 255),
            new Color32(247, 165, 194, 255), new Color32(252, 225, 142, 255)
        };
        readonly Btn[] chapterTiles = new Btn[ChapterEnds.Length];
        int journalChapter;

        static int ChapterFor(int level)
        {
            for (int i = 0; i < ChapterEnds.Length; i++)
                if (level <= ChapterEnds[i]) return i;
            return ChapterEnds.Length - 1;
        }

        Vector2 ChapterTilePosition(int index) => new Vector2(-2.28f + index % 3 * 2.28f, 1.76f - index / 3 * 1.85f);

        void BuildJournal()
        {
            for (int i = 0; i < chapterTiles.Length; i++)
            {
                int chapter = i;
                chapterTiles[i] = IconButton(Symbol.Journal, ChapterTilePosition(i), new Vector2(1.58f, 1.39f),
                    ChapterColors[i], 3105, shellPanel, () => shellScreen == ShellScreen.Journal,
                    () => { journalChapter = chapter; RefreshJournal(); });
            }
            IconButton(Symbol.Back, new Vector2(0, -3.90f), new Vector2(1.54f, 1.10f), Midnight, 3105, shellPanel,
                () => shellScreen == ShellScreen.Journal, () => ShowShell(ShellScreen.Home));
        }

        void RefreshJournal()
        {
            int earned = PlayerPrefs.GetInt("br_restored_windows", 0);
            int unlocked = ChapterFor(SavedLevel);
            for (int i = 0; i < chapterTiles.Length; i++)
            {
                if (chapterTiles[i] == null) continue;
                bool restored = (earned & (1 << i)) != 0 || i < unlocked;
                chapterTiles[i].sr.color = i == journalChapter ? Brass :
                    restored ? ChapterColors[i] : new Color32(153, 163, 184, 255);
            }
        }

        void DrawJournal()
        {
            DrawLabel("RESTORATION", new Vector3(0, 3.72f, 0), 0.034f, Ink, 6.6f);
            for (int i = 0; i < ChapterEnds.Length; i++)
            {
                Vector2 position = ChapterTilePosition(i);
                DrawLabel((i + 1).ToString(), new Vector3(position.x, position.y - 0.84f, 0), 0.014f, Ink, 0.7f);
            }
            int first = journalChapter == 0 ? 1 : ChapterEnds[journalChapter - 1] + 1;
            DrawLabel(ChapterNames[journalChapter], new Vector3(0, -1.44f, 0), 0.027f, Ink, 7f);
            DrawLabel(first + "–" + ChapterEnds[journalChapter], new Vector3(0, -1.99f, 0), 0.017f, Ink, 5.5f);
            DrawStory(ChapterStories[journalChapter], -2.81f);
        }

        void DrawStory(string story, float worldY)
        {
            Vector3 center = cam.WorldToScreenPoint(new Vector3(0, worldY, 0));
            float pixels = Screen.height / (2f * halfH);
            labelStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(0.016f * Screen.height));
            labelStyle.normal.textColor = Ink;
            labelStyle.wordWrap = true;
            GUI.Label(new Rect(center.x - 3.35f * pixels, Screen.height - center.y - 0.6f * pixels, 6.7f * pixels, 1.2f * pixels),
                story, labelStyle);
            labelStyle.wordWrap = false;
        }

        int RecordChapterCompletion()
        {
            int chapter = ChapterFor(levelNumber);
            if (levelNumber != ChapterEnds[chapter]) return 0;
            int mask = PlayerPrefs.GetInt("br_restored_windows", 0);
            if ((mask & (1 << chapter)) != 0) return 0;
            PlayerPrefs.SetInt("br_restored_windows", mask | (1 << chapter));
            int tier = chapter + 1;
            if (UnityEngine.Random.value < 0.58f)
                chestCoins = UnityEngine.Random.Range(75 * tier, 111 * tier);
            else
            {
                chestCoins = 16 * tier;
                chestTool = new[] { (int)Tool.Slot, (int)Tool.Store, (int)Tool.Break }[UnityEngine.Random.Range(0, 3)];
                chestToolAmount = UnityEngine.Random.Range(1, 3) + chapter / 2;
                stock[chestTool] += chestToolAmount;
            }
            return chestCoins;
        }

        void DrawChestReward()
        {
            if (state != State.Won || chestCoins <= 0) return;
            Sprite chest = illustratedTools[(int)Tool.Basket];
            Vector3 p = cam.WorldToScreenPoint(new Vector3(-2.12f, 2.48f, 0));
            float size = 1.08f * Screen.height / (2f * halfH);
            CatalogArt(chest, new Rect(p.x - size / 2f, Screen.height - p.y - size / 2f, size, size));
            if (chestTool < 0) return;
            p = cam.WorldToScreenPoint(new Vector3(1.30f, 2.48f, 0));
            CatalogArt(illustratedTools[chestTool], new Rect(p.x - size / 2f, Screen.height - p.y - size / 2f, size, size));
            DrawLabel("+" + chestToolAmount, new Vector3(2.16f, 2.48f, 0), 0.019f, Color.white, 0.85f);
        }

        void NextAfterWin()
        {
            if (levelNumber >= CampaignEnd)
            {
                ReturnHome();
                journalChapter = ChapterEnds.Length - 1;
                ShowShell(ShellScreen.Journal);
                return;
            }
            LoadLevel(levelNumber + 1);
        }
    }
}
