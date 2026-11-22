using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class MapSystemTests
    {
        private readonly List<Object> _cleanupObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            ClearMapPlayerPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanupObjects.Count - 1; i >= 0; i--)
            {
                if (_cleanupObjects[i] != null)
                {
                    Object.DestroyImmediate(_cleanupObjects[i]);
                }
            }
            _cleanupObjects.Clear();
            ClearMapPlayerPrefs();
        }

        private static void ClearMapPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(MapManager.SelectedMapPrefKey);
            PlayerPrefs.DeleteKey("RacingGame_BestTime_city_morning");
            PlayerPrefs.DeleteKey("RacingGame_BestTime_coastal_highway");
            PlayerPrefs.DeleteKey("RacingGame_BestTime_neon_night");
            PlayerPrefs.DeleteKey(SkinManager.SelectedSkinPrefKey);
            PlayerPrefs.Save();
        }

        private T TrackCleanup<T>(T obj) where T : Object
        {
            _cleanupObjects.Add(obj);
            return obj;
        }

        [Test]
        public void TrackDefinition_And_WaypointTrack_SupportTwoToSixLanesAndDynamicWidth()
        {
            var trackGo = TrackCleanup(new GameObject("TestTrack"));
            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();

            MapTheme theme = TrackCleanup(MapTheme.CreateCoastalHighwayPreset());
            TrackDefinition narrow2 = TrackCleanup(TrackDefinition.CreateRuntime(
                "test_narrow_2",
                "Đèo Hẹp 2 Làn",
                TrackDefinition.GetDefaultCityWaypoints(),
                lanes: 2,
                widthPerLane: 3.5f,
                mapTheme: theme,
                stars: 4,
                rewardMult: 1.5f,
                obstacles: 8));

            track.ApplyTrackDefinition(narrow2);

            Assert.AreEqual(2, track.LaneCount);
            Assert.AreEqual(3.5f, track.LaneWidth, 0.001f);
            Assert.AreEqual(7.0f, track.TrackWidth, 0.001f);
            Assert.AreEqual(3.5f, track.TrackWidth * 0.5f, 0.001f);
            Assert.AreEqual(theme, track.ActiveTheme);

            TrackDefinition wide6 = TrackCleanup(TrackDefinition.CreateCoastalHighwayPreset(theme));
            track.ApplyTrackDefinition(wide6);

            Assert.AreEqual(6, track.LaneCount);
            Assert.AreEqual(3.8f, track.LaneWidth, 0.001f);
            Assert.AreEqual(22.8f, track.TrackWidth, 0.001f);
            Assert.AreEqual(11.4f, track.TrackWidth * 0.5f, 0.001f);

            TrackSample sample = track.SampleAtDistance(50f);
            Assert.IsFalse(float.IsNaN(sample.elevation));
            Assert.Greater(track.TotalLength, 100f);
        }

        [Test]
        public void MapManager_AutoUnlocksAllThreeDefaultMaps_PersistsSelectionAndResets()
        {
            var go = TrackCleanup(new GameObject("TestMapManager"));
            MapManager mm = go.AddComponent<MapManager>();
            mm.EnsureInitialized();

            Assert.AreEqual(4, mm.MapCount);
            Assert.IsTrue(mm.IsMapUnlocked("city_morning"));
            Assert.IsTrue(mm.IsMapUnlocked("coastal_highway"));
            Assert.IsTrue(mm.IsMapUnlocked("neon_night"));
            Assert.IsTrue(mm.IsMapUnlocked("city_district"));

            var trackGo = TrackCleanup(new GameObject("TestTrack"));
            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();

            bool selected = mm.SelectMap("neon_night", track);
            Assert.IsTrue(selected);
            Assert.AreEqual("neon_night", mm.SelectedMapId);
            Assert.AreEqual(5, track.LaneCount);
            Assert.AreEqual("neon_night", PlayerPrefs.GetString(MapManager.SelectedMapPrefKey));

            // New MapManager instance restores selection from PlayerPrefs
            var go2 = TrackCleanup(new GameObject("TestMapManager2"));
            MapManager mm2 = go2.AddComponent<MapManager>();
            mm2.EnsureInitialized();
            Assert.AreEqual("neon_night", mm2.SelectedMapId);

            // Step advances from neon_night (index 2) to city_district (index 3), then wraps back to index 0 (city_morning)
            TrackDefinition fourth = mm2.StepMapSelection(1, track);
            Assert.AreEqual("city_district", fourth.MapId);
            Assert.AreEqual(4, track.LaneCount);

            TrackDefinition next = mm2.StepMapSelection(1, track);
            Assert.AreEqual("city_morning", next.MapId);
            Assert.AreEqual(4, track.LaneCount);

            // Best times save per map and clear on ResetProgress
            Assert.IsTrue(mm2.RecordBestTime("coastal_highway", 82.5f));
            Assert.IsFalse(mm2.RecordBestTime("coastal_highway", 90.0f));
            Assert.AreEqual(82.5f, mm2.GetBestTime("coastal_highway"), 0.001f);

            mm2.ResetProgress();
            Assert.AreEqual(0f, mm2.GetBestTime("coastal_highway"), 0.001f);
            Assert.AreEqual("city_morning", mm2.SelectedMapId);
        }

        [Test]
        public void EnvironmentDecorator_And_TrackObstacleSystem_RespectMapThemeAndNarrowTrackBounds()
        {
            var envGo = TrackCleanup(new GameObject("EnvAndObstacleHost"));
            WaypointTrack track = envGo.AddComponent<WaypointTrack>();
            EnvironmentDecorator env = envGo.AddComponent<EnvironmentDecorator>();
            TrackObstacleSystem obs = envGo.AddComponent<TrackObstacleSystem>();

            MapTheme coastalTheme = TrackCleanup(MapTheme.CreateCoastalHighwayPreset());
            TrackDefinition narrowMap = TrackCleanup(TrackDefinition.CreateRuntime(
                "narrow_2",
                "Đường Hẹp 2 Làn",
                TrackDefinition.GetDefaultCityWaypoints(),
                lanes: 2,
                widthPerLane: 3.2f,
                mapTheme: coastalTheme,
                stars: 3,
                rewardMult: 1.2f,
                obstacles: 10));

            track.ApplyTrackDefinition(narrowMap);
            env.RebuildEnvironment(track, coastalTheme, 42);
            obs.ApplyMapSettings(narrowMap);
            obs.SpawnObstacles(track, 99);

            Assert.Greater(env.Decorations.Count, 10);
            Assert.AreEqual(coastalTheme, env.ActiveTheme);

            Assert.AreEqual(10, obs.Obstacles.Count);
            float maxExpectedOffset = track.TrackWidth * 0.36f + 0.001f;
            for (int i = 0; i < obs.Obstacles.Count; i++)
            {
                Assert.LessOrEqual(
                    Mathf.Abs(obs.Obstacles[i].LateralOffset),
                    maxExpectedOffset,
                    "Obstacle lateral offset must stay safely within narrow 2-lane TrackWidth.");
            }
        }

        [Test]
        public void AI_And_Police_ScaleCleanlyOnNarrowAndWideTracks()
        {
            MapTheme theme = TrackCleanup(MapTheme.CreateNeonNightExpressPreset());
            TrackDefinition[] configs =
            {
                TrackCleanup(TrackDefinition.CreateRuntime("t2", "Narrow 2", TrackDefinition.GetDefaultCityWaypoints(), 2, 3.4f, theme, 3, 1.0f, 6)),
                TrackCleanup(TrackDefinition.CreateCoastalHighwayPreset(theme))
            };

            for (int c = 0; c < configs.Length; c++)
            {
                TrackDefinition def = configs[c];
                var host = TrackCleanup(new GameObject($"RaceHost_{def.MapId}"));
                WaypointTrack track = host.AddComponent<WaypointTrack>();
                TrackObstacleSystem obs = host.AddComponent<TrackObstacleSystem>();
                track.ApplyTrackDefinition(def);
                obs.ApplyMapSettings(def);
                obs.SpawnObstacles(track, 123);

                Car playerCar = host.AddComponent<Car>();
                var aiGo = new GameObject("AI");
                aiGo.transform.SetParent(host.transform);
                Car aiCar = aiGo.AddComponent<Car>();
                var polGo = new GameObject("Police");
                polGo.transform.SetParent(host.transform);
                Car policeCar = polGo.AddComponent<Car>();

                AIRacerDriver aiDriver = aiCar.gameObject.AddComponent<AIRacerDriver>();
                aiDriver.SetMapCornerSpeedMultiplier(def.AiCornerSpeedMultiplier);
                aiDriver.Configure("AI_1", 0.8f, -def.LaneWidth * 0.5f, 777);

                PoliceRacerDriver policeDriver = policeCar.gameObject.AddComponent<PoliceRacerDriver>();
                policeDriver.ConfigurePolice(playerCar);

                InitializeTestCar(playerCar, null, track, 60f, 0f);
                InitializeTestCar(aiCar, aiDriver, track, 50f, -def.LaneWidth * 0.5f);
                InitializeTestCar(policeCar, policeDriver, track, 10f, 0f);

                float maxHalfOffRoad = track.MaxOffRoadWidth * 0.5f;
                const float dt = 0.02f;
                for (int step = 0; step < 120; step++)
                {
                    aiCar.StepPhysics(dt, track);
                    policeCar.StepPhysics(dt, track);
                }

                Assert.Greater(aiCar.Physics.Speed, 5f, $"AI failed to accelerate on {def.MapId}");
                Assert.LessOrEqual(Mathf.Abs(aiCar.LateralOffset), maxHalfOffRoad, $"AI went outside track on {def.MapId}");
                Assert.Greater(policeCar.Physics.Speed, 5f, $"Police failed to accelerate on {def.MapId}");
                Assert.LessOrEqual(Mathf.Abs(policeCar.LateralOffset), maxHalfOffRoad, $"Police went outside track on {def.MapId}");
            }
        }

        [Test]
        public void GameMenuUI_MapCarousel_DisplaysThreeMapsAndStepsSelection()
        {
            var lobbyGo = TrackCleanup(new GameObject("LobbyTestObj"));
            WaypointTrack track = TrackCleanup(new GameObject("LobbyTrack")).AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();
            EnvironmentDecorator env = lobbyGo.AddComponent<EnvironmentDecorator>();
            MapManager mm = lobbyGo.AddComponent<MapManager>();
            SkinManager sm = lobbyGo.AddComponent<SkinManager>();
            sm.EnsureInitialized();
            mm.EnsureInitialized();

            GameMenuUI menuUi = lobbyGo.AddComponent<GameMenuUI>();
            menuUi.InitializeStandaloneLobby();
            if (menuUi.MenuCanvas != null)
            {
                TrackCleanup(menuUi.MenuCanvas.gameObject);
            }

            Assert.IsNotNull(menuUi.MenuCanvas);
            Assert.AreEqual(4, menuUi.MapCount);
            Assert.AreEqual("city_morning", menuUi.SelectedMap.MapId);
            Assert.AreEqual(4, track.LaneCount);

            TrackDefinition second = menuUi.StepMapFromLobby(1);
            Assert.AreEqual("coastal_highway", second.MapId);
            Assert.AreEqual(6, track.LaneCount);
            Assert.Greater(env.Decorations.Count, 10);

            TrackDefinition third = menuUi.StepMapFromLobby(1);
            Assert.AreEqual("neon_night", third.MapId);
            Assert.AreEqual(5, track.LaneCount);

            TrackDefinition backToSecond = menuUi.StepMapFromLobby(-1);
            Assert.AreEqual("coastal_highway", backToSecond.MapId);
            Assert.AreEqual(6, track.LaneCount);
        }

        [Test]
        public void MapTheme_Valley_MatchesRoadRashVisualTheme_AndWaypointTrackDefaultsTo16m()
        {
            MapTheme valley = TrackCleanup(MapTheme.CreateValleyPreset());
            Assert.AreEqual("MapTheme_Valley", valley.ThemeName);
            Assert.AreEqual(RoadRashVisualTheme.SkyTop, valley.SkyTop);
            Assert.AreEqual(RoadRashVisualTheme.SkyMid, valley.SkyMid);
            Assert.AreEqual(RoadRashVisualTheme.SkyHorizon, valley.SkyHorizon);
            Assert.AreEqual(RoadRashVisualTheme.AsphaltLight, valley.AsphaltLight);
            Assert.AreEqual(RoadRashVisualTheme.CenterYellowLine, valley.CenterYellowLine);
            Assert.AreEqual(RoadRashVisualTheme.StartCheckDark, valley.StartCheckDark);
            Assert.AreEqual(RoadRashVisualTheme.StartCheckLight, valley.StartCheckLight);

            var trackGo = TrackCleanup(new GameObject("DefaultTrack"));
            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();
            track.BuildDefaultCircuit();

            Assert.AreEqual(22, track.WaypointCount);
            Assert.AreEqual(4, track.LaneCount);
            Assert.AreEqual(4.0f, track.LaneWidth, 0.001f);
            Assert.AreEqual(16.0f, track.TrackWidth, 0.001f);
        }

        [Test]
        public void EnvironmentDecorator_ReadsDynamicZoneListAndDensityFromMapTheme_InsteadOfHardcodedThreeZones()
        {
            var host = TrackCleanup(new GameObject("DynamicZonesHost"));
            WaypointTrack track = host.AddComponent<WaypointTrack>();
            TrackDefinition cityDef = TrackCleanup(TrackDefinition.CreateCityPreset(null));
            track.ApplyTrackDefinition(cityDef);

            // Build a MapTheme with 5 distinct zones (more than 3) and unique props per zone.
            MapTheme customTheme = TrackCleanup(ScriptableObject.CreateInstance<MapTheme>());
            RoadsidePropKind[] zoneProps =
            {
                RoadsidePropKind.FireHydrant,
                RoadsidePropKind.VendingMachine,
                RoadsidePropKind.BusStop,
                RoadsidePropKind.HighwaySign,
                RoadsidePropKind.CityBuilding
            };

            var zones = new MapZonePropConfig[5];
            for (int z = 0; z < 5; z++)
            {
                zones[z] = new MapZonePropConfig
                {
                    zoneName = $"Zone_{z}",
                    propDensityMultiplier = z == 1 ? 2.0f : (z == 3 ? 0.35f : 1.0f),
                    leftProps = new[] { zoneProps[z] },
                    rightProps = new[] { zoneProps[z] }
                };
            }

            // Assign private serialized fields via UnityEditor.SerializedObject in EditMode
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(customTheme);
            so.FindProperty("slotIntervalMeters").floatValue = 10f;
            UnityEditor.SerializedProperty zonesProp = so.FindProperty("zones");
            zonesProp.arraySize = 5;
            for (int z = 0; z < 5; z++)
            {
                UnityEditor.SerializedProperty elem = zonesProp.GetArrayElementAtIndex(z);
                elem.FindPropertyRelative("zoneName").stringValue = zones[z].zoneName;
                elem.FindPropertyRelative("propDensityMultiplier").floatValue = zones[z].propDensityMultiplier;
                UnityEditor.SerializedProperty lp = elem.FindPropertyRelative("leftProps");
                lp.arraySize = 1;
                lp.GetArrayElementAtIndex(0).intValue = (int)zoneProps[z];
                UnityEditor.SerializedProperty rp = elem.FindPropertyRelative("rightProps");
                rp.arraySize = 1;
                rp.GetArrayElementAtIndex(0).intValue = (int)zoneProps[z];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EnvironmentDecorator env = host.AddComponent<EnvironmentDecorator>();
            env.RebuildEnvironment(track, customTheme, 77);

            // Verify all 5 zone-specific props appear (proving it does not wrap at % 3)
            var seenKinds = new HashSet<RoadsidePropKind>();
            int zone1Count = 0;
            int zone3Count = 0;
            float totalLen = track.TotalLength;
            for (int i = 0; i < env.Decorations.Count; i++)
            {
                RoadsideDecoration d = env.Decorations[i];
                seenKinds.Add(d.Kind);
                int zoneIdx = EnvironmentDecorator.GetZoneIndexAtDistance(d.TrackDistance, totalLen, 5);
                if (zoneIdx == 1) zone1Count++;
                else if (zoneIdx == 3) zone3Count++;
            }

            for (int z = 0; z < 5; z++)
            {
                Assert.IsTrue(seenKinds.Contains(zoneProps[z]), $"Expected prop {zoneProps[z]} from zone {z} of 5-zone MapTheme.");
            }
            Assert.Greater(zone1Count, zone3Count * 2, "Higher propDensityMultiplier (2.0) must spawn significantly more props than low density (0.35).");
#endif
        }

        [Test]
        public void PlayerSaveData_And_Lobby_PersistUnlockedMapsAndGuardLockedSelection()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"racing2d_map_save_{System.Guid.NewGuid():N}.json");
            try
            {
                MapTheme theme = TrackCleanup(MapTheme.CreateCityPreset());
                TrackDefinition openMap = TrackCleanup(TrackDefinition.CreateCityMorningPreset(theme));
                TrackDefinition lockedMap = TrackCleanup(TrackDefinition.CreateCityPreset(theme));

#if UNITY_EDITOR
                // Mark second map as locked by default
                var so = new UnityEditor.SerializedObject(lockedMap);
                so.FindProperty("unlockedByDefault").boolValue = false;
                so.FindProperty("difficultyStars").intValue = 2; // Unlock cost = 500
                so.ApplyModifiedPropertiesWithoutUndo();
#endif

                var saveSys = new CarUpgradeSystem(tempPath);
                saveSys.AddMoney(600); // Total = 800

                var host = TrackCleanup(new GameObject("LockedMapLobbyHost"));
                WaypointTrack track = TrackCleanup(new GameObject("LockedMapTrack")).AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();
                MapManager mm = host.AddComponent<MapManager>();
                mm.SetMapAssets(new[] { openMap, lockedMap });
                mm.BindSaveSystem(saveSys);

                Assert.IsTrue(mm.IsMapUnlocked(openMap.MapId));
                Assert.IsFalse(mm.IsMapUnlocked(lockedMap.MapId));
                Assert.IsFalse(mm.SelectMap(lockedMap.MapId, track), "Locked map must not be directly selectable.");
                Assert.AreEqual(openMap.MapId, mm.SelectedMapId);

                // Stepping in MapManager skips locked maps
                TrackDefinition stepped = mm.StepMapSelection(1, track);
                Assert.AreEqual(openMap.MapId, stepped.MapId);

                // Lobby lets player browse locked map, shows locked state, and unlocks + persists to PlayerSaveData
                SkinManager sm = host.AddComponent<SkinManager>();
                sm.EnsureInitialized();
                GameMenuUI menuUi = host.AddComponent<GameMenuUI>();
                menuUi.BindStandaloneSaveSystem(saveSys);
                menuUi.InitializeStandaloneLobby();
                if (menuUi.MenuCanvas != null) TrackCleanup(menuUi.MenuCanvas.gameObject);

                TrackDefinition browsed = menuUi.StepMapFromLobby(1);
                Assert.AreEqual(lockedMap.MapId, browsed.MapId);
                Assert.IsTrue(menuUi.IsBrowsedMapLocked);
                Assert.AreEqual(openMap.MapId, menuUi.SelectedMap.MapId);
                Assert.IsTrue(menuUi.MapActionButtonLabel.Contains("MỞ KHÓA"));

                Assert.IsTrue(menuUi.TryUnlockMapFromLobby(lockedMap.MapId));
                Assert.IsTrue(mm.IsMapUnlocked(lockedMap.MapId));
                Assert.AreEqual(lockedMap.MapId, mm.SelectedMapId);
                Assert.AreEqual(300, saveSys.Money); // 800 - 500

                // Reload into fresh CarUpgradeSystem + MapManager from disk JSON and verify persistence
                var reloadedSave = new CarUpgradeSystem(tempPath);
                Assert.IsTrue(reloadedSave.Load());
                Assert.IsTrue(reloadedSave.IsMapUnlocked(lockedMap.MapId));
                Assert.AreEqual(lockedMap.MapId, reloadedSave.SelectedMapId);

                var host2 = TrackCleanup(new GameObject("ReloadedMapHost"));
                MapManager mm2 = host2.AddComponent<MapManager>();
                mm2.SetMapAssets(new[] { openMap, lockedMap });
                mm2.BindSaveSystem(reloadedSave);
                Assert.IsTrue(mm2.IsMapUnlocked(lockedMap.MapId));
                Assert.AreEqual(lockedMap.MapId, mm2.SelectedMapId);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
        }

        private static void InitializeTestCar(Car car, Driver driver, WaypointTrack track, float startDistance, float lateralOffset)
        {
            TrackSample s = track.SampleAtDistance(startDistance);
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            Vector2 pos = s.position + right * lateralOffset;
            float headingRad = Mathf.Atan2(s.tangent.y, s.tangent.x);
            car.Initialize(driver, pos, headingRad, track);
        }
    }
}
