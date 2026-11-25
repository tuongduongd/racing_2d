using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class ExternalJsonMidRaceSaveAndBgmTests
    {
        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();
        private string _tempDir;
        private bool _hadMutePref;
        private int _prevMutePref;
        private bool _hadBgmPref;
        private float _prevBgmPref;
        private bool _hadSfxPref;
        private float _prevSfxPref;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rr2d_ext_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

            _hadMutePref = PlayerPrefs.HasKey(RaceAudioController.MutePrefKey);
            _prevMutePref = PlayerPrefs.GetInt(RaceAudioController.MutePrefKey, 0);
            _hadBgmPref = PlayerPrefs.HasKey(RaceAudioController.BgmVolumePrefKey);
            _prevBgmPref = PlayerPrefs.GetFloat(RaceAudioController.BgmVolumePrefKey, 0.28f);
            _hadSfxPref = PlayerPrefs.HasKey(RaceAudioController.SfxVolumePrefKey);
            _prevSfxPref = PlayerPrefs.GetFloat(RaceAudioController.SfxVolumePrefKey, 1.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null) UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();

            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }

            if (_hadMutePref) PlayerPrefs.SetInt(RaceAudioController.MutePrefKey, _prevMutePref);
            else PlayerPrefs.DeleteKey(RaceAudioController.MutePrefKey);

            if (_hadBgmPref) PlayerPrefs.SetFloat(RaceAudioController.BgmVolumePrefKey, _prevBgmPref);
            else PlayerPrefs.DeleteKey(RaceAudioController.BgmVolumePrefKey);

            if (_hadSfxPref) PlayerPrefs.SetFloat(RaceAudioController.SfxVolumePrefKey, _prevSfxPref);
            else PlayerPrefs.DeleteKey(RaceAudioController.SfxVolumePrefKey);

            PlayerPrefs.Save();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) _cleanup.Add(obj);
            return obj;
        }

        [Test]
        public void ExternalJsonTrackLoader_LoadsAllFourStreamingAssetsMaps_AndReflectsJsonEditsWithoutRecompile()
        {
            var loader = new ExternalJsonTrackLoader();
            IReadOnlyList<TrackJsonDto> maps = loader.LoadTracks(ExternalJsonTrackLoader.DefaultMapsDirectory);

            Assert.AreEqual(4, maps.Count, "StreamingAssets/Maps must contain 4 valid map JSON files.");
            Assert.AreEqual("city_morning", maps[0].mapId);
            Assert.AreEqual("coastal_highway", maps[1].mapId);
            Assert.AreEqual("neon_night", maps[2].mapId);
            Assert.AreEqual("city_district", maps[3].mapId);

            // Modify a copy in a temp folder and verify MapManager reads the live edited JSON value
            string tempMapsDir = Path.Combine(_tempDir, "Maps");
            Directory.CreateDirectory(tempMapsDir);
            string jsonPath = Path.Combine(tempMapsDir, "01_custom.json");
            var editedDto = new TrackJsonDto
            {
                mapId = "edited_json_track",
                displayName = "Map JSON Chỉnh Tay",
                laneCount = 3,
                laneWidth = 5.2f,
                obstacleCount = 7,
                rewardMultiplier = 2.5f,
                waypoints = new[]
                {
                    new TrackPointDto { x = 0f, y = 0f },
                    new TrackPointDto { x = 100f, y = 0f },
                    new TrackPointDto { x = 100f, y = 100f },
                    new TrackPointDto { x = 0f, y = 100f }
                }
            };
            File.WriteAllText(jsonPath, JsonUtility.ToJson(editedDto, true));

            var host = Track(new GameObject("JsonMapHost"));
            MapManager mm = host.AddComponent<MapManager>();
            int loadedCount = mm.LoadMapsFromJsonDirectory(tempMapsDir);

            Assert.AreEqual(1, loadedCount);
            Assert.AreEqual("edited_json_track", mm.SelectedMap.MapId);
            Assert.AreEqual(3, mm.SelectedMap.LaneCount);
            Assert.AreEqual(5.2f, mm.SelectedMap.LaneWidth, 0.001f);
            Assert.AreEqual(2.5f, mm.SelectedMap.RewardMultiplier, 0.001f);
        }

        [Test]
        public void ExternalBalanceConfigLoader_LoadsStreamingAssetsConfig_AndHandlesCorruptFilesSafely()
        {
            var loader = new ExternalBalanceConfigLoader();
            GameBalanceConfig loaded = loader.LoadStrict(ExternalBalanceConfigLoader.DefaultConfigPath);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(5, loaded.maxUpgradeLevel);
            Assert.AreEqual(150, loaded.engineBaseCost);
            Assert.AreEqual(48f, loaded.defaultVehicleStats.maxSpeed, 0.001f);

            string badPath = Path.Combine(_tempDir, "bad_balance.json");
            File.WriteAllText(badPath, "{invalid json");
            Assert.Throws<InvalidTrackConfigurationException>(() => loader.LoadStrict(badPath));

            GameBalanceConfig fallback = loader.LoadOrDefault(badPath);
            Assert.IsNotNull(fallback);
            Assert.AreEqual(5, fallback.maxUpgradeLevel);
        }

        [Test]
        public void MidRaceSaveService_RoundTripAndCorruptFileRecovery()
        {
            var storage = new FileSaveStorageAdapter();
            var codec = new UnityJsonCodec();
            string path = Path.Combine(_tempDir, "midrace.json");
            var service = new MidRaceSaveService(storage, codec, path);

            Assert.IsFalse(service.HasSavedRace());

            var snapshot = new MidRaceSaveData
            {
                mapId = "neon_night",
                totalLaps = 4,
                difficulty = (int)DifficultyLevel.Hard,
                raceTime = 17.42f,
                finishedCount = 0,
                racers = new List<RacerMidRaceSnapshot>
                {
                    new RacerMidRaceSnapshot
                    {
                        racerName = "Player",
                        isPlayer = true,
                        posX = 123.5f,
                        posY = -14.25f,
                        velX = 32.1f,
                        velY = 1.5f,
                        headingRad = 0.35f,
                        completedLaps = 1,
                        nextCheckpointIndex = 3,
                        totalCheckpointsPassed = 8
                    }
                }
            };

            service.SaveSnapshot(snapshot);
            Assert.IsTrue(service.HasSavedRace());

            Assert.IsTrue(service.TryLoadSnapshot(out MidRaceSaveData restored));
            Assert.AreEqual("neon_night", restored.mapId);
            Assert.AreEqual(4, restored.totalLaps);
            Assert.AreEqual(17.42f, restored.raceTime, 0.001f);
            Assert.AreEqual(1, restored.racers.Count);
            Assert.AreEqual(123.5f, restored.racers[0].posX, 0.001f);
            Assert.AreEqual(3, restored.racers[0].nextCheckpointIndex);

            File.WriteAllText(path, "CORRUPTED_MIDRACE{{{");
            Assert.Throws<SaveDataCorruptedException>(() => service.LoadSnapshotStrict());
            Assert.IsFalse(service.TryLoadSnapshot(out _));
        }

        [Test]
        public void GameManager_SaveAndLoadMidRaceState_RestoresPositionsVelocitiesLapsAndCheckpoints()
        {
            var sysGo = Track(new GameObject("MidRaceGMHost"));
            var trackGo = Track(new GameObject("MidRaceTrack"));
            var playerGo = Track(new GameObject("MidRacePlayer"));
            var aiGo = Track(new GameObject("MidRaceAI"));

            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            Car playerCar = playerGo.AddComponent<Car>();
            var playerDriver = playerGo.AddComponent<PlayerDriver>();
            playerDriver.SetDriverName("Player");

            Car aiCar = aiGo.AddComponent<Car>();
            aiCar.ConfigureIdentity(false, Color.green);
            var aiDriver = aiGo.AddComponent<AIRacerDriver>();
            aiDriver.Configure("Viper", 0.85f, 2.0f, 2026);

            var gm = sysGo.AddComponent<GameManager>();
            var lapManager = sysGo.AddComponent<LapCheckpointManager>();

            string savePath = Path.Combine(_tempDir, "save_progress.json");
            string midPath = Path.Combine(_tempDir, "midrace_state.json");
            var storage = new FileSaveStorageAdapter();
            var codec = new UnityJsonCodec();
            var upg = new CarUpgradeSystem(storage, codec, savePath, VehicleStats.Default);
            var midService = new MidRaceSaveService(storage, codec, midPath);

            SetPrivateField(gm, "track", track);
            SetPrivateField(gm, "playerCar", playerCar);
            SetPrivateField(gm, "playerDriver", playerDriver);
            SetPrivateField(gm, "lapManager", lapManager);
            SetPrivateField(gm, "aiOpponentCount", 1);
            gm.ConfigureServices(storage: storage, jsonCodec: codec, upgradeSystem: upg, midRaceSaveService: midService);

            InvokePrivateMethod(gm, "SetupReferences");
            InvokePrivateMethod(gm, "SpawnOrSyncOpponents");
            InvokePrivateMethod(gm, "EnsurePoliceSetup");
            for (int i = 0; i < gm.AllCars.Count; i++)
            {
                if (gm.AllCars[i] != null) Track(gm.AllCars[i].gameObject);
            }
            var spawnedRenderer = UnityEngine.Object.FindAnyObjectByType<RoadRashRenderer2D>();
            if (spawnedRenderer != null) Track(spawnedRenderer.gameObject);
            lapManager.Initialize(track, new List<Car>(gm.AllCars), 3);
            gm.ResetRaceGrid();
            gm.TransitionTo(GameFlowState.Racing);

            // Drive AI and push player forward across checkpoints to accumulate real race state
            Vector2 wp0 = track.GetWaypoint(0);
            Vector2 dir0 = track.GetTrackDirection(0);
            Vector2 wp1 = track.GetWaypoint(track.WaypointCount / 6);
            playerCar.RestoreRuntimeState(wp1 - dir0 * 2f, dir0 * 35f, Mathf.Atan2(dir0.y, dir0.x), track);

            for (int step = 0; step < 30; step++)
            {
                playerCar.Physics.ApplyThrottle(1f);
                gm.StepRaceSimulation(0.02f);
            }

            Vector2 savedPlayerPos = playerCar.Physics.Position;
            Vector2 savedPlayerVel = playerCar.Physics.Velocity;
            float savedPlayerHeading = playerCar.Physics.Heading;
            float savedRaceTime = lapManager.RaceTime;
            RacerProgress savedProg = lapManager.GetProgress(playerCar);
            int savedNextCp = savedProg.NextCheckpointIndex;
            int savedTotalCp = savedProg.TotalCheckpointsPassed;
            Vector2 savedAiPos = aiCar.Physics.Position;

            Assert.Greater(savedRaceTime, 0.5f);
            Assert.IsTrue(gm.SaveMidRaceState());
            Assert.IsTrue(gm.HasSavedMidRace);

            // Scramble positions back to grid start, simulating a fresh launch before LoadMidRaceState()
            gm.ResetRaceGrid();
            Assert.AreEqual(0f, lapManager.RaceTime, 0.001f);
            Assert.Greater(Vector2.Distance(playerCar.Physics.Position, savedPlayerPos), 5f);

            Assert.IsTrue(gm.LoadMidRaceState(), "LoadMidRaceState must restore saved active race.");
            Assert.AreEqual(GameFlowState.Racing, gm.CurrentFlowState);
            Assert.AreEqual(savedRaceTime, lapManager.RaceTime, 0.001f);
            Assert.AreEqual(savedPlayerPos.x, playerCar.Physics.Position.x, 0.01f);
            Assert.AreEqual(savedPlayerPos.y, playerCar.Physics.Position.y, 0.01f);
            Assert.AreEqual(savedPlayerVel.x, playerCar.Physics.Velocity.x, 0.01f);
            Assert.AreEqual(savedPlayerVel.y, playerCar.Physics.Velocity.y, 0.01f);
            Assert.AreEqual(savedPlayerHeading, playerCar.Physics.Heading, 0.001f);
            Assert.AreEqual(savedAiPos.x, aiCar.Physics.Position.x, 0.01f);

            RacerProgress restoredProg = lapManager.GetProgress(playerCar);
            Assert.AreEqual(savedNextCp, restoredProg.NextCheckpointIndex);
            Assert.AreEqual(savedTotalCp, restoredProg.TotalCheckpointsPassed);
        }

        [Test]
        public void PauseMenuUI_And_LobbyUI_ExposeMidRaceSaveAndContinueButtons()
        {
            var host = Track(new GameObject("PauseAndLobbySaveHost"));
            var gm = host.AddComponent<GameManager>();
            var pauseUi = host.AddComponent<PauseMenuUI>();

            string midPath = Path.Combine(_tempDir, "ui_midrace.json");
            var storage = new FileSaveStorageAdapter();
            var codec = new UnityJsonCodec();
            var midService = new MidRaceSaveService(storage, codec, midPath);

            gm.ConfigureServices(storage: storage, jsonCodec: codec, midRaceSaveService: midService);
            pauseUi.Bind(gm);
            Assert.IsNotNull(pauseUi.SaveRaceButton, "PauseMenuUI must build a Save Race button.");

            var lobbyHost = Track(new GameObject("LobbySaveHost"));
            var menuUi = lobbyHost.AddComponent<GameMenuUI>();
            menuUi.BindStandaloneMidRaceSaveService(midService);
            menuUi.InitializeStandaloneLobby();
            if (menuUi.MenuCanvas != null) Track(menuUi.MenuCanvas.gameObject);

            Assert.IsNotNull(menuUi.ContinueSavedRaceButton);
            Assert.IsFalse(menuUi.ContinueSavedRaceButton.gameObject.activeSelf,
                "Continue button must stay hidden when no mid-race save exists.");

            midService.SaveSnapshot(new MidRaceSaveData
            {
                mapId = "city_morning",
                totalLaps = 3,
                racers = new List<RacerMidRaceSnapshot> { new RacerMidRaceSnapshot { isPlayer = true } }
            });
            menuUi.BindStandaloneMidRaceSaveService(midService);

            Assert.IsTrue(menuUi.ContinueSavedRaceButton.gameObject.activeSelf,
                "Continue button must appear when a mid-race save file is present.");
        }

        [Test]
        public void RaceAudioController_GeneratesBgmClip_AndPersistsAudioPreferences()
        {
            PlayerPrefs.DeleteKey(RaceAudioController.MutePrefKey);
            PlayerPrefs.DeleteKey(RaceAudioController.BgmVolumePrefKey);
            PlayerPrefs.DeleteKey(RaceAudioController.SfxVolumePrefKey);
            PlayerPrefs.Save();

            var host1 = Track(new GameObject("AudioHost1"));
            host1.AddComponent<AudioListener>();
            RaceAudioController audio1 = host1.AddComponent<RaceAudioController>();

            InvokePrivateMethod(audio1, "Awake");
            Assert.IsTrue(audio1 is IAudioService);
            Assert.IsNotNull(audio1.BgmClip, "RaceAudioController must generate a procedural BGM clip.");
            Assert.Greater(audio1.BgmClip.length, 1.5f);

            audio1.SetBgmVolume(0.65f);
            audio1.SetSfxVolume(0.80f);
            audio1.SetMuted(true);

            var host2 = Track(new GameObject("AudioHost2"));
            RaceAudioController audio2 = host2.AddComponent<RaceAudioController>();
            InvokePrivateMethod(audio2, "Awake");

            Assert.IsTrue(audio2.IsMuted);
            Assert.AreEqual(0.65f, audio2.BgmVolume, 0.001f);
            Assert.AreEqual(0.80f, audio2.SfxVolume, 0.001f);
        }

        [Test]
        public void GameManager_SubscribesPoliceAndSkinObservers_AndUnsubscribesCleanly()
        {
            var host = Track(new GameObject("ObserverSyncHost"));
            var trackGo = Track(new GameObject("ObserverSyncTrack"));
            var playerGo = Track(new GameObject("ObserverSyncPlayer"));

            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();
            Car playerCar = playerGo.AddComponent<Car>();
            var playerDriver = playerGo.AddComponent<PlayerDriver>();
            var gm = host.AddComponent<GameManager>();
            var policeCtrl = host.AddComponent<PoliceChaseController>();
            var skinMgr = host.AddComponent<SkinManager>();
            skinMgr.EnsureInitialized();

            SetPrivateField(gm, "track", track);
            SetPrivateField(gm, "playerCar", playerCar);
            SetPrivateField(gm, "playerDriver", playerDriver);
            SetPrivateField(gm, "policeController", policeCtrl);
            SetPrivateField(gm, "skinManager", skinMgr);

            InvokePrivateMethod(gm, "BindSystemEvents");

            Car cop = policeCtrl.EnsurePoliceUnit(playerCar, track);
            if (cop != null) Track(cop.gameObject);
            int beforeCount = gm.PoliceEngagementCount;
            policeCtrl.ResetPoliceOnGrid(playerCar, track);
            Assert.AreEqual(beforeCount + 1, gm.PoliceEngagementCount, "PoliceEngaged event should notify GameManager.");

            Assert.IsTrue(skinMgr.SelectSkin(DriverBrandSkin.Grab));
            Assert.AreEqual(DriverBrandSkin.Grab, playerCar.ActiveSkin.SkinId, "SkinChanged event should sync onto PlayerCar.");

            Assert.DoesNotThrow(() => InvokePrivateMethod(gm, "UnbindSystemEvents"));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var fi = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            fi.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var mi = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(mi, $"Missing private method '{methodName}' on {target.GetType().Name}.");
            mi.Invoke(target, null);
        }
    }
}
