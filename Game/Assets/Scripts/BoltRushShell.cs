using System.Collections;
using UnityEngine;

namespace BoltRush
{
    public sealed partial class BoltRushGame
    {
        enum ShellScreen { Home, Game, Pause, Settings, Shop, Journal }
        const int WinCoins = 20;
        static readonly Color Ink = new Color32(57, 44, 37, 255);
        static readonly Color Teal = new Color32(36, 128, 111, 255);
        static readonly Color Brick = new Color32(174, 89, 55, 255);
        static readonly Color Paper = new Color32(255, 238, 203, 255);

        ShellScreen shellScreen, settingsBack, shopBack;
        Transform shellPanel;
        Btn pauseButton, soundButton, effectsButton, vibrationButton, shopButton;
        SpriteRenderer walletCoin;
        bool soundEnabled, effectsEnabled, vibrationEnabled;
        int coins;
        string walletLabel, shopNotice = "";
        bool InGame => shellScreen == ShellScreen.Game;
        int SavedLevel => Mathf.Max(1, PlayerPrefs.GetInt("br_level", 1));

        void BuildShell()
        {
            coins = Mathf.Max(0, PlayerPrefs.GetInt("br_coins", 60));
            for (int i = 0; i < stock.Length; i++) stock[i] = Mathf.Max(0, PlayerPrefs.GetInt("br_tool_" + i, i == 0 ? PlayerPrefs.GetInt("br_slots", 1) : 1));
            soundEnabled = PlayerPrefs.GetInt("br_sound", 1) != 0;
            effectsEnabled = PlayerPrefs.GetInt("br_effects", 1) != 0;
            vibrationEnabled = PlayerPrefs.GetInt("br_vibration", 0) != 0;
#if BOLTRUSH_QA && !UNITY_EDITOR
            if (PlayerPrefs.GetInt("br_qa_supplies_v3", 0) == 0)
            {
                coins = Mathf.Max(coins, 100000);
                for (int i = 0; i < stock.Length; i++) stock[i] = Mathf.Max(stock[i], 999);
                PlayerPrefs.SetInt("br_qa_supplies_v3", 1);
                SaveWallet();
            }
#endif
            var root = new GameObject("Workshop menu").transform;
            shellPanel = new GameObject("Menu panel").transform;
            shellPanel.SetParent(root, false);
            shellBackdrop = MakePanel("Backdrop", shellPanel, new Vector2(60, 60), new Color(0.15f, 0.11f, 0.09f, 0.54f), 3100);
            shellShadow = MakePanel("Card shadow", shellPanel, new Vector2(7.9f, 10f), new Color(0.20f, 0.12f, 0.08f, 0.40f), 3101);
            shellCard = MakePanel("Card", shellPanel, new Vector2(7.9f, 10f), Paper, 3102);
            walletCoin = MakeSprite("Wallet coin", root, sprCoin, Color.white, 3200);
            walletCoin.transform.localScale = Vector3.one * 0.46f;
            pauseButton = IconButton(Symbol.Pause, new Vector2(-3.55f, 8.3f), new Vector2(1.12f, 1.02f), Midnight, 610, root,
                () => InGame && state == State.Playing, () => ShowShell(ShellScreen.Pause));
            shopButton = IconButton(Symbol.Shop, new Vector2(-3.55f, 8.3f), new Vector2(1.21f, 1.08f), Brass, 3320, root,
                () => shellScreen != ShellScreen.Shop, OpenShop);
            IconButton(Symbol.Play, new Vector2(0, -6.21f), new Vector2(2.72f, 1.39f), Enamel, 3105, shellPanel,
                () => shellScreen == ShellScreen.Home, () => ShowShell(ShellScreen.Game));
            IconButton(Symbol.Journal, new Vector2(-2.48f, -6.21f), new Vector2(1.46f, 1.24f), Brass, 3105, shellPanel,
                () => shellScreen == ShellScreen.Home, () => { journalChapter = ChapterFor(SavedLevel); ShowShell(ShellScreen.Journal); });
            IconButton(Symbol.Settings, new Vector2(2.48f, -6.21f), new Vector2(1.46f, 1.24f), Midnight, 3105, shellPanel,
                () => shellScreen == ShellScreen.Home, OpenSettings);
            IconButton(Symbol.Settings, new Vector2(0, -2.03f), new Vector2(1.59f, 1.23f), Midnight, 3105, shellPanel,
                () => shellScreen == ShellScreen.Pause, OpenSettings);
            IconButton(Symbol.Play, new Vector2(0, 0.22f), new Vector2(1.88f, 1.44f), Enamel, 3105, shellPanel,
                () => shellScreen == ShellScreen.Pause, () => ShowShell(ShellScreen.Game));
            IconButton(Symbol.Retry, new Vector2(-2.37f, 0.22f), new Vector2(1.54f, 1.24f), Brick, 3105, shellPanel,
                () => shellScreen == ShellScreen.Pause, () => LoadLevel(levelNumber));
            IconButton(Symbol.Home, new Vector2(2.37f, 0.22f), new Vector2(1.54f, 1.24f), Midnight, 3105, shellPanel,
                () => shellScreen == ShellScreen.Pause, ReturnHome);
            soundButton = IconButton(Symbol.Sound, new Vector2(-2.12f, 0.30f), new Vector2(1.72f, 1.55f), Enamel, 3105, shellPanel,
                () => shellScreen == ShellScreen.Settings, () => ToggleOption("br_sound", ref soundEnabled));
            effectsButton = IconButton(Symbol.Effects, new Vector2(0, 0.30f), new Vector2(1.72f, 1.55f), Enamel, 3105, shellPanel,
                () => shellScreen == ShellScreen.Settings, () => ToggleOption("br_effects", ref effectsEnabled));
            vibrationButton = IconButton(Symbol.Vibration, new Vector2(2.12f, 0.30f), new Vector2(1.72f, 1.55f), Enamel, 3105, shellPanel,
                () => shellScreen == ShellScreen.Settings, () => ToggleOption("br_vibration", ref vibrationEnabled));
            IconButton(Symbol.Back, new Vector2(0, -2.64f), new Vector2(1.59f, 1.23f), Midnight, 3105, shellPanel,
                () => shellScreen == ShellScreen.Settings, () => ShowShell(settingsBack));
            BuildJournal();
            BuildShop();
            RefreshWallet();
        }

        void ShowShell(ShellScreen screen)
        {
            if (shellScreen == ShellScreen.Shop && screen != ShellScreen.Shop)
                shopNotice = "";
            shellScreen = screen;
            Time.timeScale = InGame ? 1f : 0f;
            shellPanel.gameObject.SetActive(!InGame);
            bool home = screen == ShellScreen.Home;
            shellBackdrop.enabled = !home;
            shellCard.size = shellShadow.size = home ? new Vector2(8.05f, 3.48f) :
                new Vector2(7.9f, screen == ShellScreen.Shop ? 13.8f : 10f);
            shellCard.transform.localPosition = new Vector3(0, home ? -6.18f : 0f, 0);
            shellShadow.transform.localPosition = shellCard.transform.localPosition + new Vector3(0.08f, -0.23f, 0);
            soundButton.sr.color = soundEnabled ? Enamel : new Color32(113, 114, 121, 255);
            effectsButton.sr.color = effectsEnabled ? Enamel : new Color32(113, 114, 121, 255);
            vibrationButton.sr.color = vibrationEnabled ? Enamel : new Color32(113, 114, 121, 255);
            RefreshJournal();
            RefreshButtons();
        }

        void OpenShop()
        {
            if (rewardInFlight) return;
            shopBack = shellScreen;
            shopNotice = "";
            shopScroll = 0f;
            shopPressed = false;
            ShowShell(ShellScreen.Shop);
        }

        void OpenSettings()
        {
            settingsBack = shellScreen;
            ShowShell(ShellScreen.Settings);
        }

        void ReturnHome()
        {
            StopAllCoroutines();
            LoadLevel(SavedLevel, ShellScreen.Home);
        }

        void ToggleOption(string key, ref bool option)
        {
            option = !option;
            if (!soundEnabled) audioSrc.Stop();
            PlayerPrefs.SetInt(key, option ? 1 : 0);
            PlayerPrefs.Save();
            ShowShell(ShellScreen.Settings);
        }

        void DrawShell()
        {
            if (InGame)
            {
                if (state == State.Playing)
                    DrawLabel(walletLabel, new Vector3(3.59f, pauseButton.t.position.y, 0), 0.019f, Ink, 1.15f);
                if (state == State.Playing && shopNotice.Length > 0)
                    DrawLabel(shopNotice, new Vector3(0, BtnY + 1.04f, 0), 0.017f, Ink, 6.8f);
                return;
            }
            if (shellScreen == ShellScreen.Shop)
            {
                DrawShop();
                return;
            }
            if (shellScreen == ShellScreen.Journal)
            {
                DrawJournal();
                return;
            }
            string heading = shellScreen == ShellScreen.Home ? "BOLT RUSH" :
                shellScreen == ShellScreen.Pause ? "PAUSED" : "SETTINGS";
            DrawLabel(heading, new Vector3(0, shellScreen == ShellScreen.Home ? pauseButton.t.position.y : 3.02f, 0), 0.035f, Ink, 6.7f);
            if (shellScreen == ShellScreen.Home)
            {
                DrawLabel(walletLabel, new Vector3(3.59f, pauseButton.t.position.y, 0), 0.019f, Ink, 1.15f);
                DrawLabel(levelNumber.ToString(), new Vector3(0, -7.24f, 0), 0.020f, Ink, 2.7f);
                DrawLabel(ChapterNames[ChapterFor(levelNumber)], new Vector3(0, -5.28f, 0), 0.017f, Ink, 6.8f);
            }
        }

        void RefreshWallet()
        {
            walletLabel = coins.ToString();
            for (int i = 0; i < stock.Length; i++) toolCounts[i] = stock[i].ToString();
        }

        int AwardLevel()
        {
            if (levelNumber == CampaignEnd && (PlayerPrefs.GetInt("br_restored_windows", 0) & (1 << (ChapterEnds.Length - 1))) != 0)
                return 0;
            int reward = WinCoins + RecordChapterCompletion();
            coins += reward;
            PlayerPrefs.SetInt("br_level", Mathf.Min(levelNumber + 1, CampaignEnd));
            SaveWallet();
            return reward;
        }

        void SetPlateVisible(PlateView plate, bool visible)
        {
            plate.revealed = visible;
            plate.t.gameObject.SetActive(visible);
            foreach (var screw in screws)
                if (screw.plate == plate && !screw.removed)
                    screw.sr.enabled = visible;
        }

        void RevealPlayablePlates(bool animate)
        {
            foreach (var screw in screws)
            {
                if (screw.plate.revealed || !CanUnscrew(screw.id)) continue;
                SetPlateVisible(screw.plate, true);
                if (animate && effectsEnabled)
                    StartCoroutine(RevealPlate(screw.plate));
            }
        }

        IEnumerator RevealPlate(PlateView plate)
        {
            var renderers = plate.all;
            var baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
            for (float elapsed = 0; elapsed < 0.16f && plate.remaining > 0; elapsed += Time.deltaTime)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color color = baseColors[i];
                    color.a *= Mathf.Lerp(0.45f, 1f, elapsed / 0.16f);
                    renderers[i].color = color;
                }
                yield return null;
            }
            if (plate.remaining > 0)
                for (int i = 0; i < renderers.Length; i++) renderers[i].color = baseColors[i];
        }
    }
}
