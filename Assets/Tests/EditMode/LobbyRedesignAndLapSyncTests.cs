using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace RacingGame.Tests
{
    /// <summary>
    /// Stage 4A EditMode tests verifying TMP Lobby UI redesign, CRT toggle wiring,
    /// Skin Selector modal API, and standalone Lobby -> SampleScene lap/difficulty PlayerPrefs sync.
    /// </summary>
    public class LobbyRedesignAndLapSyncTests
    {
        private bool _hadLaps;
        private int _prevLaps;
        private bool _hadDiff;
        private int _prevDiff;
        private bool _hadCrt;
        private int _prevCrt;
        private bool _hadSkin;
        private int _prevSkin;
        private bool _hadMap;
        private string _prevMap;

        [SetUp]
        public void SetUp()
        {
            _hadLaps = PlayerPrefs.HasKey(GameManager.SelectedLapsPrefKey);
            _prevLaps = PlayerPrefs.GetInt(GameManager.SelectedLapsPrefKey, 3);
            _hadDiff = PlayerPrefs.HasKey(GameManager.SelectedDifficultyPrefKey);
            _prevDiff = PlayerPrefs.GetInt(GameManager.SelectedDifficultyPrefKey, (int)DifficultyLevel.Normal);
            _hadCrt = PlayerPrefs.HasKey(CrtScanlineController.PlayerPrefsKey);
            _prevCrt = PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, 1);
            _hadSkin = PlayerPrefs.HasKey(SkinManager.SelectedSkinPrefKey);
            _prevSkin = PlayerPrefs.GetInt(SkinManager.SelectedSkinPrefKey, 0);
            _hadMap = PlayerPrefs.HasKey(MapManager.SelectedMapPrefKey);
            _prevMap = PlayerPrefs.GetString(MapManager.SelectedMapPrefKey, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            RestoreIntKey(_hadLaps, GameManager.SelectedLapsPrefKey, _prevLaps);
            RestoreIntKey(_hadDiff, GameManager.SelectedDifficultyPrefKey, _prevDiff);
            RestoreIntKey(_hadCrt, CrtScanlineController.PlayerPrefsKey, _prevCrt);
            RestoreIntKey(_hadSkin, SkinManager.SelectedSkinPrefKey, _prevSkin);

            if (_hadMap) PlayerPrefs.SetString(MapManager.SelectedMapPrefKey, _prevMap);
            else PlayerPrefs.DeleteKey(MapManager.SelectedMapPrefKey);

            PlayerPrefs.Save();
        }

        private static void RestoreIntKey(bool hadKey, string key, int value)
        {
            if (hadKey) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        [Test]
        public void StandaloneLobby_LapAndDifficultySelectionsPersistToPlayerPrefsAndReloadInGameManager()
        {
            PlayerPrefs.DeleteKey(GameManager.SelectedLapsPrefKey);
            PlayerPrefs.DeleteKey(GameManager.SelectedDifficultyPrefKey);
            PlayerPrefs.Save();

            var host1 = new GameObject("LobbySyncHost_1");
            var host2 = new GameObject("LobbySyncHost_2");
            GameMenuUI ui1 = null;
            GameMenuUI ui2 = null;

            try
            {
                ui1 = host1.AddComponent<GameMenuUI>();
                ui1.InitializeStandaloneLobby();

                Assert.AreEqual(3, ui1.SelectedLaps);
                Assert.AreEqual(DifficultyLevel.Normal, ui1.SelectedDifficulty);

                ui1.SelectLapsFromLobby(5);
                ui1.SelectDifficultyFromLobby(DifficultyLevel.Hard);

                Assert.AreEqual(5, ui1.SelectedLaps);
                Assert.AreEqual(DifficultyLevel.Hard, ui1.SelectedDifficulty);
                Assert.AreEqual(5, PlayerPrefs.GetInt(GameManager.SelectedLapsPrefKey, -1));
                Assert.AreEqual(5, GameManager.LoadSavedTotalLaps(3));
                Assert.AreEqual(DifficultyLevel.Hard, GameManager.LoadSavedDifficulty(DifficultyLevel.Normal));

                // Fresh Lobby instance simulating scene reload restores saved lap & difficulty
                ui2 = host2.AddComponent<GameMenuUI>();
                ui2.InitializeStandaloneLobby();
                Assert.AreEqual(5, ui2.SelectedLaps);
                Assert.AreEqual(DifficultyLevel.Hard, ui2.SelectedDifficulty);
            }
            finally
            {
                if (ui1 != null && ui1.MenuCanvas != null) Object.DestroyImmediate(ui1.MenuCanvas.gameObject);
                if (ui2 != null && ui2.MenuCanvas != null) Object.DestroyImmediate(ui2.MenuCanvas.gameObject);
                Object.DestroyImmediate(host1);
                Object.DestroyImmediate(host2);
            }
        }

        [Test]
        public void LobbyRedesign_UsesTmpWidgets_TogglesCrtSafely_AndPreservesSkinSelectorAndQuitButton()
        {
            PlayerPrefs.DeleteKey(CrtScanlineController.PlayerPrefsKey);
            PlayerPrefs.Save();

            var host = new GameObject("LobbyRedesignHost");
            CrtScanlineController crt = host.AddComponent<CrtScanlineController>();
            crt.LoadSavedPreference();

            GameMenuUI menuUi = host.AddComponent<GameMenuUI>();
            try
            {
                menuUi.InitializeStandaloneLobby();

                Assert.IsNotNull(menuUi.MenuCanvas);
                Assert.IsNotNull(menuUi.OuterQuitButton);
                Assert.AreEqual(4, menuUi.SkinCardCount);

                TMP_Text[] tmpLabels = menuUi.MenuCanvas.GetComponentsInChildren<TMP_Text>(true);
                Assert.GreaterOrEqual(tmpLabels.Length, 10, "Lobby redesign should render labels with TextMeshPro.");

                Assert.IsFalse(menuUi.IsSkinSelectorOpen);
                menuUi.OpenSkinSelector();
                Assert.IsTrue(menuUi.IsSkinSelectorOpen);
                Assert.IsTrue(menuUi.SelectSkinFromLobby(DriverBrandSkin.Grab));
                menuUi.CloseSkinSelector();
                Assert.IsFalse(menuUi.IsSkinSelectorOpen);

                bool initialCrt = crt.IsEnabled;
                bool toggledCrt = menuUi.ToggleCrtFromLobby();
                Assert.AreEqual(!initialCrt, toggledCrt);
                Assert.AreEqual(toggledCrt, crt.IsEnabled);
                Assert.AreEqual(toggledCrt ? 1 : 0, PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, -1));
            }
            finally
            {
                crt.ReleaseRuntimeResources();
                if (menuUi != null && menuUi.MenuCanvas != null)
                {
                    Object.DestroyImmediate(menuUi.MenuCanvas.gameObject);
                }
                Object.DestroyImmediate(host);
            }
        }
    }
}
