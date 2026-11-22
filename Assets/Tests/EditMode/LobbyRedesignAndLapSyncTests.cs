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

        [Test]
        public void GarageWorkshopRedesign_BuildsFourCardsWithFivePips_AndPurchasesViaPureViewChecks()
        {
            string tempSavePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"racing2d_garage_ui_{System.Guid.NewGuid():N}.json");
            var host = new GameObject("GarageUiHost");
            GameMenuUI menuUi = host.AddComponent<GameMenuUI>();

            try
            {
                var saveSys = new CarUpgradeSystem(tempSavePath);
                Assert.AreEqual(200, saveSys.Money);

                menuUi.BindStandaloneSaveSystem(saveSys);
                menuUi.InitializeStandaloneLobby();

                Assert.AreEqual(4, menuUi.UpgradeCardCount);
                Assert.IsFalse(menuUi.IsGarageOpen);

                menuUi.OpenGarageFromLobby();
                Assert.IsTrue(menuUi.IsGarageOpen);

                // First Engine purchase costs 150 <= 200 -> succeeds and leaves 50
                Assert.IsTrue(menuUi.TryBuyUpgradeFromGarage(UpgradeType.Engine));
                Assert.AreEqual(1, saveSys.GetLevel(UpgradeType.Engine));
                Assert.AreEqual(50, saveSys.Money);

                // Second Engine purchase costs 225 > 50 -> rejected cleanly via CanAfford
                Assert.IsFalse(menuUi.TryBuyUpgradeFromGarage(UpgradeType.Engine));
                Assert.AreEqual(1, saveSys.GetLevel(UpgradeType.Engine));
                Assert.AreEqual(50, saveSys.Money);

                menuUi.CloseGarageToLobby();
                Assert.IsFalse(menuUi.IsGarageOpen);
            }
            finally
            {
                if (menuUi != null && menuUi.MenuCanvas != null)
                {
                    Object.DestroyImmediate(menuUi.MenuCanvas.gameObject);
                }
                Object.DestroyImmediate(host);
                if (System.IO.File.Exists(tempSavePath))
                {
                    System.IO.File.Delete(tempSavePath);
                }
            }
        }

        [Test]
        public void RaceHudOverhaul_BuildsFourCornerTmpLayout_HandlesObserverEvents_AndUnsubscribesInOnDisableAndOnDestroy()
        {
            var trackGo = new GameObject("HudTestTrack");
            var playerGo = new GameObject("HudTestPlayer");
            var aiGo = new GameObject("HudTestAi");
            var sysGo = new GameObject("HudTestSystems");
            RaceHUD raceHud = null;
            Canvas hudCanvas = null;

            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                Vector2 wp0 = track.GetWaypoint(0);
                Vector2 dir0 = track.GetTrackDirection(0);
                float heading = Mathf.Atan2(dir0.y, dir0.x);

                var playerCar = playerGo.AddComponent<Car>();
                var playerDriver = playerGo.AddComponent<PlayerDriver>();
                playerDriver.SetDriverName("Player");
                playerCar.Initialize(playerDriver, wp0, heading, track);

                var aiCar = aiGo.AddComponent<Car>();
                var aiDriver = aiGo.AddComponent<AIRacerDriver>();
                aiDriver.SetDriverName("Viper");
                aiCar.Initialize(aiDriver, wp0 - dir0 * 3f, heading, track);

                var gm = sysGo.AddComponent<GameManager>();
                var lapManager = sysGo.AddComponent<LapCheckpointManager>();
                lapManager.Initialize(track, new[] { playerCar, aiCar }, 3);

                var obsSystem = sysGo.AddComponent<TrackObstacleSystem>();
                obsSystem.EnableObstacles = true;

                raceHud = sysGo.AddComponent<RaceHUD>();
                raceHud.Bind(gm, playerCar, lapManager, track, obsSystem);
                hudCanvas = raceHud.HudCanvas;

                Assert.IsNotNull(hudCanvas, "RaceHUD must build its overlay Canvas.");
                Assert.GreaterOrEqual(
                    hudCanvas.GetComponentsInChildren<TMP_Text>(true).Length,
                    12,
                    "RaceHUD should use TextMeshPro across its 4-corner layout.");

                raceHud.RefreshHudImmediate(GameFlowState.Racing);

                Assert.AreEqual("1st", raceHud.CurrentRankText);
                Assert.AreEqual("000", raceHud.CurrentSpeedText);
                Assert.AreEqual("G1", raceHud.CurrentGearText);
                Assert.AreEqual("ON ROAD", raceHud.CurrentTrackStateText);
                Assert.AreEqual("LAP 1/3", raceHud.CurrentLapText);
                Assert.AreEqual("00:00.00", raceHud.CurrentTimerText);
                Assert.AreEqual(2, raceHud.LeaderboardRowCount);
                Assert.GreaterOrEqual(raceHud.MinimapDotCount, 1);

                Assert.IsFalse(raceHud.IsCountdownVisible, "Countdown center overlay must stay hidden during Racing.");
                Assert.IsFalse(raceHud.IsWrongWayVisible, "Wrong-way center overlay must stay hidden unless going backward.");

                raceHud.RefreshHudImmediate(GameFlowState.Countdown);
                Assert.IsTrue(raceHud.IsCountdownVisible, "Countdown center overlay must activate during Countdown.");
                raceHud.RefreshHudImmediate(GameFlowState.Racing);
                Assert.IsFalse(raceHud.IsCountdownVisible);

                // Verify Observer subscription triggers tactical flash on obstacle collision
                Assert.IsTrue(raceHud.IsSubscribed);
                obsSystem.AddManualObstacle(ObstacleKind.OilBarrel, playerCar.TrackDistance, playerCar.LateralOffset, 2.5f);
                obsSystem.StepObstacles(track, new[] { playerCar }, 0.02f);
                Assert.IsTrue(raceHud.IsFlashVisible, "ObstacleHit should activate the tactical flash banner.");
                StringAssert.Contains("VA CHẠM", raceHud.CurrentFlashText);

                // Verify OnDisable cleanly unsubscribes all Observer events
                // (In EditMode without PlayMode loop, invoke Unity lifecycle callbacks explicitly)
                const System.Reflection.BindingFlags lifecycleFlags =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var onDisableMethod = typeof(RaceHUD).GetMethod("OnDisable", lifecycleFlags);
                var onEnableMethod = typeof(RaceHUD).GetMethod("OnEnable", lifecycleFlags);
                var onDestroyMethod = typeof(RaceHUD).GetMethod("OnDestroy", lifecycleFlags);
                Assert.IsNotNull(onDisableMethod, "RaceHUD must implement OnDisable() to unsubscribe Observer events.");
                Assert.IsNotNull(onEnableMethod, "RaceHUD must implement OnEnable() to re-subscribe Observer events.");
                Assert.IsNotNull(onDestroyMethod, "RaceHUD must implement OnDestroy() to unsubscribe Observer events.");

                raceHud.ClearMessage();
                Assert.IsFalse(raceHud.IsFlashVisible);
                raceHud.enabled = false;
                onDisableMethod.Invoke(raceHud, null);
                Assert.IsFalse(raceHud.IsSubscribed, "OnDisable must unsubscribe all Observer events.");

                obsSystem.AddManualObstacle(ObstacleKind.TrafficCone, playerCar.TrackDistance, playerCar.LateralOffset, 2.5f);
                obsSystem.StepObstacles(track, new[] { playerCar }, 5f);
                Assert.IsFalse(raceHud.IsFlashVisible, "Disabled RaceHUD must not react to ObstacleHit.");

                // Re-enable -> re-subscribes, then Destroy -> OnDestroy unsubscribes without NullReferenceException
                raceHud.enabled = true;
                onEnableMethod.Invoke(raceHud, null);
                Assert.IsTrue(raceHud.IsSubscribed, "OnEnable should re-subscribe when already bound.");

                obsSystem.AddManualObstacle(ObstacleKind.RoadBarrier, playerCar.TrackDistance, playerCar.LateralOffset, 2.5f);
                onDestroyMethod.Invoke(raceHud, null);
                Assert.IsFalse(raceHud.IsSubscribed, "OnDestroy must unsubscribe all Observer events.");
                Object.DestroyImmediate(raceHud);
                raceHud = null;

                Assert.DoesNotThrow(() => obsSystem.StepObstacles(track, new[] { playerCar }, 5f));
            }
            finally
            {
                if (raceHud != null) Object.DestroyImmediate(raceHud);
                if (hudCanvas != null) Object.DestroyImmediate(hudCanvas.gameObject);
                Object.DestroyImmediate(sysGo);
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(trackGo);
            }
        }
    }
}
