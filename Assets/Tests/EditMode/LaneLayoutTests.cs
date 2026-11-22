using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class LaneLayoutTests
    {
        private readonly List<Object> _cleanupObjects = new List<Object>();
        private HashSet<GameObject> _preExistingGameObjects;

        [SetUp]
        public void SetUp()
        {
            _preExistingGameObjects = new HashSet<GameObject>();
            GameObject[] existing = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    _preExistingGameObjects.Add(existing[i]);
                }
            }

            ClearLaneTestPlayerPrefs();
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

            if (_preExistingGameObjects != null)
            {
                GameObject[] remaining = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                for (int i = 0; i < remaining.Length; i++)
                {
                    GameObject go = remaining[i];
                    if (go != null && !_preExistingGameObjects.Contains(go))
                    {
                        Object.DestroyImmediate(go);
                    }
                }
                _preExistingGameObjects.Clear();
            }

            ClearLaneTestPlayerPrefs();
        }

        private static void ClearLaneTestPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(MapManager.SelectedMapPrefKey);
            PlayerPrefs.DeleteKey(SkinManager.SelectedSkinPrefKey);
            PlayerPrefs.Save();
        }

        private T TrackCleanup<T>(T obj) where T : Object
        {
            if (obj != null)
            {
                _cleanupObjects.Add(obj);
            }
            return obj;
        }

        private WaypointTrack CreateTrack(int lanes, float laneWidth, out TrackDefinition def, string idSuffix = "")
        {
            MapTheme theme = TrackCleanup(lanes == 6
                ? MapTheme.CreateCoastalHighwayPreset()
                : MapTheme.CreateValleyPreset());

            if (lanes == 4 && Mathf.Approximately(laneWidth, 4.0f))
            {
                def = TrackCleanup(TrackDefinition.CreateCityMorningPreset(theme));
            }
            else if (lanes == 6 && Mathf.Approximately(laneWidth, 3.8f))
            {
                def = TrackCleanup(TrackDefinition.CreateCoastalHighwayPreset(theme));
            }
            else
            {
                def = TrackCleanup(TrackDefinition.CreateRuntime(
                    $"test_{lanes}lane{idSuffix}",
                    $"Test {lanes} Lanes",
                    TrackDefinition.GetDefaultCityWaypoints(),
                    lanes,
                    laneWidth,
                    theme,
                    stars: 3,
                    rewardMult: 1.0f,
                    obstacles: 12));
            }

            GameObject trackGo = TrackCleanup(new GameObject($"TestTrack_{lanes}L{idSuffix}"));
            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(def);
            return track;
        }

        private static void PlaceCarOnTrack(Car car, Driver driver, WaypointTrack track, float distance, float lateralOffset)
        {
            TrackSample sample = track.SampleAtDistance(distance);
            Vector2 right = new Vector2(sample.tangent.y, -sample.tangent.x);
            Vector2 pos = sample.position + right * lateralOffset;
            float heading = Mathf.Atan2(sample.tangent.y, sample.tangent.x);
            car.Initialize(driver, pos, heading, track);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            fi.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(mi, $"Missing private method '{methodName}' on {target.GetType().Name}.");
            mi.Invoke(target, null);
        }

        [Test]
        public void WaypointTrack_GetLaneCenterOffset_ReturnsSymmetricLaneCentersInsideTrackForTwoFourAndSixLanes()
        {
            var cases = new (int lanes, float laneWidth, float[] expectedOffsets)[]
            {
                (2, 3.5f, new[] { -1.75f, 1.75f }),
                (2, 4.0f, new[] { -2.0f, 2.0f }),
                (4, 4.0f, new[] { -6.0f, -2.0f, 2.0f, 6.0f }),
                (6, 3.8f, new[] { -9.5f, -5.7f, -1.9f, 1.9f, 5.7f, 9.5f })
            };

            for (int c = 0; c < cases.Length; c++)
            {
                var cfg = cases[c];
                WaypointTrack track = CreateTrack(cfg.lanes, cfg.laneWidth, out _, $"_cfg{c}");

                Assert.AreEqual(cfg.lanes, track.LaneCount);
                Assert.AreEqual(cfg.laneWidth, track.LaneWidth, 0.001f);
                Assert.AreEqual(cfg.lanes * cfg.laneWidth, track.TrackWidth, 0.001f);

                float expectedMaxCenter = (cfg.lanes - 1) * 0.5f * cfg.laneWidth;
                Assert.AreEqual(expectedMaxCenter, track.MaxLaneCenterOffset, 0.001f);

                float halfTrack = track.TrackWidth * 0.5f;
                for (int i = 0; i < cfg.lanes; i++)
                {
                    float offset = track.GetLaneCenterOffset(i);
                    Assert.AreEqual(cfg.expectedOffsets[i], offset, 0.001f,
                        $"Lane {i} offset mismatch for {cfg.lanes}x{cfg.laneWidth}m.");
                    Assert.Less(Mathf.Abs(offset), halfTrack,
                        $"Lane {i} center must be strictly inside +/-TrackWidth*0.5f.");
                    Assert.LessOrEqual(Mathf.Abs(offset) + cfg.laneWidth * 0.5f, halfTrack + 0.0001f,
                        $"Lane {i} outer boundary must stay within TrackWidth*0.5f.");
                }

                // Symmetry and uniform LaneWidth spacing
                for (int i = 0; i < cfg.lanes; i++)
                {
                    float left = track.GetLaneCenterOffset(i);
                    float right = track.GetLaneCenterOffset(cfg.lanes - 1 - i);
                    Assert.AreEqual(-left, right, 0.001f, $"Lane offsets must be symmetric for {cfg.lanes} lanes.");

                    if (i + 1 < cfg.lanes)
                    {
                        float spacing = track.GetLaneCenterOffset(i + 1) - left;
                        Assert.AreEqual(cfg.laneWidth, spacing, 0.001f,
                            $"Adjacent lane centers must be spaced by LaneWidth for {cfg.lanes} lanes.");
                    }
                }

                // Out-of-range laneIndex clamping
                Assert.AreEqual(cfg.expectedOffsets[0], track.GetLaneCenterOffset(-3), 0.001f);
                Assert.AreEqual(cfg.expectedOffsets[cfg.lanes - 1], track.GetLaneCenterOffset(cfg.lanes + 4), 0.001f);
            }
        }

        [Test]
        public void GameManager_GridAndAiLaneHelpers_AlignCleanlyToLanesForTwoFourAndSixLanes()
        {
            WaypointTrack track2 = CreateTrack(2, 3.5f, out _);
            WaypointTrack track4 = CreateTrack(4, 4.0f, out _);
            WaypointTrack track6 = CreateTrack(6, 3.8f, out _);

            // 2 lanes x 3.5m -> centers [-1.75, +1.75]
            Assert.AreEqual(-1.75f, GameManager.GetGridSlotLateralOffset(track2, 0), 0.001f);
            Assert.AreEqual(1.75f, GameManager.GetGridSlotLateralOffset(track2, 1), 0.001f);
            Assert.AreEqual(-1.75f, GameManager.GetGridSlotLateralOffset(track2, 2), 0.001f);
            Assert.AreEqual(-1.75f, GameManager.GetAiPreferredLateralOffset(track2, 0), 0.001f);
            Assert.AreEqual(1.75f, GameManager.GetAiPreferredLateralOffset(track2, 1), 0.001f);

            // 4 lanes x 4.0m -> centers [-6, -2, +2, +6]; grid uses inner lanes [-2, +2]
            Assert.AreEqual(-2.0f, GameManager.GetGridSlotLateralOffset(track4, 0), 0.001f);
            Assert.AreEqual(2.0f, GameManager.GetGridSlotLateralOffset(track4, 1), 0.001f);
            Assert.AreEqual(-6.0f, GameManager.GetAiPreferredLateralOffset(track4, 0), 0.001f);
            Assert.AreEqual(6.0f, GameManager.GetAiPreferredLateralOffset(track4, 1), 0.001f);
            Assert.AreEqual(-2.0f, GameManager.GetAiPreferredLateralOffset(track4, 2), 0.001f);
            Assert.AreEqual(2.0f, GameManager.GetAiPreferredLateralOffset(track4, 3), 0.001f);

            // 6 lanes x 3.8m -> centers [-9.5, -5.7, -1.9, +1.9, +5.7, +9.5]; grid uses inner lanes [-1.9, +1.9]
            Assert.AreEqual(-1.9f, GameManager.GetGridSlotLateralOffset(track6, 0), 0.001f);
            Assert.AreEqual(1.9f, GameManager.GetGridSlotLateralOffset(track6, 1), 0.001f);
            Assert.AreEqual(-9.5f, GameManager.GetAiPreferredLateralOffset(track6, 0), 0.001f);
            Assert.AreEqual(9.5f, GameManager.GetAiPreferredLateralOffset(track6, 1), 0.001f);
            Assert.AreEqual(-5.7f, GameManager.GetAiPreferredLateralOffset(track6, 2), 0.001f);
            Assert.AreEqual(5.7f, GameManager.GetAiPreferredLateralOffset(track6, 3), 0.001f);
            Assert.AreEqual(-1.9f, GameManager.GetAiPreferredLateralOffset(track6, 4), 0.001f);
        }

        [Test]
        public void GameManager_ResetRaceGrid_SpawnsPlayerAiAndPoliceOnValidLanesAcrossTwoFourAndSixLanes()
        {
            var configs = new (int lanes, float laneWidth, float expectedInnerOffset)[]
            {
                (2, 3.5f, 1.75f),
                (4, 4.0f, 2.0f),
                (6, 3.8f, 1.9f)
            };

            for (int c = 0; c < configs.Length; c++)
            {
                var cfg = configs[c];
                WaypointTrack track = CreateTrack(cfg.lanes, cfg.laneWidth, out _);

                GameObject gmGo = TrackCleanup(new GameObject($"GMHost_{cfg.lanes}L"));
                GameObject playerGo = TrackCleanup(new GameObject($"PlayerCar_{cfg.lanes}L"));
                Car playerCar = playerGo.AddComponent<Car>();
                PlayerDriver playerDriver = playerGo.AddComponent<PlayerDriver>();
                PoliceChaseController policeCtrl = gmGo.AddComponent<PoliceChaseController>();
                AIProfile normalProfile = TrackCleanup(AIProfile.CreateDefault(DifficultyLevel.Normal));

                GameManager gm = gmGo.AddComponent<GameManager>();
                SetPrivateField(gm, "track", track);
                SetPrivateField(gm, "playerCar", playerCar);
                SetPrivateField(gm, "playerDriver", playerDriver);
                SetPrivateField(gm, "policeController", policeCtrl);
                SetPrivateField(gm, "normalProfile", normalProfile);

                InvokePrivateMethod(gm, "SpawnOrSyncOpponents");
                InvokePrivateMethod(gm, "ApplyDifficultyToAI");
                gm.ResetRaceGrid();

                for (int i = 0; i < gm.AllCars.Count; i++)
                {
                    if (gm.AllCars[i] != null)
                    {
                        TrackCleanup(gm.AllCars[i].gameObject);
                    }
                }

                // Default aiOpponentCount (4) + 1 player + 1 police = 6 cars
                Assert.AreEqual(6, gm.AllCars.Count, $"Unexpected total car count on {cfg.lanes}-lane grid.");

                Vector2 origin = track.GetWaypoint(0);
                Vector2 dir = track.GetTrackDirection(0);
                Vector2 right = new Vector2(dir.y, -dir.x);
                float halfTrack = track.TrackWidth * 0.5f;

                const float firstRowOffset = 4f;
                const float rowSpacing = 6.5f;

                // AI cars occupy slots 0..3, Player occupies slot 4
                int aiSlot = 0;
                for (int i = 0; i < gm.AllCars.Count; i++)
                {
                    Car car = gm.AllCars[i];
                    Assert.IsNotNull(car);

                    if (car.Driver is PoliceRacerDriver)
                    {
                        Assert.LessOrEqual(Mathf.Abs(car.LateralOffset), halfTrack,
                            $"Police spawn must stay inside track on {cfg.lanes} lanes.");
                        Assert.IsTrue(track.ProjectPosition(car.Physics.Position).isOnTrack,
                            $"Police must spawn on track for {cfg.lanes} lanes.");
                        continue;
                    }

                    int slot = car == playerCar ? 4 : aiSlot++;
                    int row = slot / 2;
                    float expectedSlotLat = (slot % 2 == 0) ? -cfg.expectedInnerOffset : cfg.expectedInnerOffset;
                    Vector2 expectedPos = origin - dir * (firstRowOffset + row * rowSpacing) + right * expectedSlotLat;

                    Assert.AreEqual(expectedPos.x, car.Physics.Position.x, 0.01f,
                        $"Slot {slot} X mismatch on {cfg.lanes}-lane grid.");
                    Assert.AreEqual(expectedPos.y, car.Physics.Position.y, 0.01f,
                        $"Slot {slot} Y mismatch on {cfg.lanes}-lane grid.");
                    Assert.AreEqual(expectedSlotLat, car.LateralOffset, 0.01f,
                        $"Slot {slot} LateralOffset must align to inner lane center on {cfg.lanes} lanes.");
                    Assert.Less(Mathf.Abs(car.LateralOffset), halfTrack,
                        $"Slot {slot} must be strictly inside track bounds on {cfg.lanes} lanes.");
                    Assert.IsTrue(track.ProjectPosition(car.Physics.Position).isOnTrack,
                        $"Slot {slot} must project onto track on {cfg.lanes} lanes.");

                    if (car.Driver is AIRacerDriver aiDriver)
                    {
                        float expectedPreferred = GameManager.GetAiPreferredLateralOffset(track, slot);
                        Assert.AreEqual(expectedPreferred, aiDriver.PreferredLateralOffset, 0.001f);
                        Assert.Less(Mathf.Abs(aiDriver.PreferredLateralOffset), halfTrack,
                            $"AI preferred lateral offset must be inside track bounds on {cfg.lanes} lanes.");
                    }
                }

                // Clean up immediately so FindObjectsByType in next loop iteration does not pick up prior AI/Police
                Object.DestroyImmediate(gmGo);
                for (int i = _cleanupObjects.Count - 1; i >= 0; i--)
                {
                    if (_cleanupObjects[i] != null)
                    {
                        Object.DestroyImmediate(_cleanupObjects[i]);
                    }
                }
                _cleanupObjects.Clear();
            }
        }

        [Test]
        public void AI_And_Police_StayStrictlyWithinTrackWidthAcrossTwoFourAndSixLaneCircuits()
        {
            var configs = new (int lanes, float laneWidth)[]
            {
                (2, 3.4f),
                (4, 4.0f),
                (6, 3.8f)
            };

            for (int c = 0; c < configs.Length; c++)
            {
                var cfg = configs[c];
                WaypointTrack track = CreateTrack(cfg.lanes, cfg.laneWidth, out _);
                float halfTrack = track.TrackWidth * 0.5f;

                GameObject playerGo = TrackCleanup(new GameObject($"OuterPlayer_{cfg.lanes}L"));
                Car playerCar = playerGo.AddComponent<Car>();
                float outerRightLane = track.GetLaneCenterOffset(cfg.lanes - 1);
                PlaceCarOnTrack(playerCar, null, track, 65f, outerRightLane);

                // Blocker ahead on the outer-left lane to exercise avoidance without exiting the road
                GameObject blockerGo = TrackCleanup(new GameObject($"Blocker_{cfg.lanes}L"));
                Car blockerCar = blockerGo.AddComponent<Car>();
                float outerLeftLane = track.GetLaneCenterOffset(0);
                PlaceCarOnTrack(blockerCar, null, track, 52f, outerLeftLane);

                GameObject aiOuterLeftGo = TrackCleanup(new GameObject($"AI_OuterLeft_{cfg.lanes}L"));
                Car aiLeftCar = aiOuterLeftGo.AddComponent<Car>();
                AIRacerDriver aiLeftDriver = aiOuterLeftGo.AddComponent<AIRacerDriver>();
                aiLeftDriver.Configure("AI_Left", 0.85f, outerLeftLane, 101 + c);
                PlaceCarOnTrack(aiLeftCar, aiLeftDriver, track, 40f, outerLeftLane);

                GameObject aiOuterRightGo = TrackCleanup(new GameObject($"AI_OuterRight_{cfg.lanes}L"));
                Car aiRightCar = aiOuterRightGo.AddComponent<Car>();
                AIRacerDriver aiRightDriver = aiOuterRightGo.AddComponent<AIRacerDriver>();
                aiRightDriver.Configure("AI_Right", 0.85f, outerRightLane, 202 + c);
                PlaceCarOnTrack(aiRightCar, aiRightDriver, track, 40f, outerRightLane);

                GameObject policeGo = TrackCleanup(new GameObject($"Police_{cfg.lanes}L"));
                Car policeCar = policeGo.AddComponent<Car>();
                PoliceRacerDriver policeDriver = policeGo.AddComponent<PoliceRacerDriver>();
                policeDriver.ConfigurePolice(playerCar);
                PlaceCarOnTrack(policeCar, policeDriver, track, 25f, 0f);

                var allCars = new List<Car> { playerCar, blockerCar, aiLeftCar, aiRightCar, policeCar };
                aiLeftDriver.SetAllCars(allCars);
                aiRightDriver.SetAllCars(allCars);

                const float dt = 0.02f;
                const int steps = 200; // 4.0 seconds through straight and opening corner
                for (int s = 0; s < steps; s++)
                {
                    aiLeftCar.StepPhysics(dt, track);
                    aiRightCar.StepPhysics(dt, track);
                    policeCar.StepPhysics(dt, track);

                    Assert.LessOrEqual(Mathf.Abs(aiLeftCar.LateralOffset), halfTrack,
                        $"Outer-left AI exceeded TrackWidth*0.5f at step {s} on {cfg.lanes} lanes.");
                    Assert.LessOrEqual(Mathf.Abs(aiRightCar.LateralOffset), halfTrack,
                        $"Outer-right AI exceeded TrackWidth*0.5f at step {s} on {cfg.lanes} lanes.");
                    Assert.LessOrEqual(Mathf.Abs(policeCar.LateralOffset), halfTrack,
                        $"Police exceeded TrackWidth*0.5f at step {s} on {cfg.lanes} lanes.");
                }

                Assert.Greater(aiLeftCar.Physics.Speed, 5f, $"AI left failed to accelerate on {cfg.lanes} lanes.");
                Assert.Greater(aiRightCar.Physics.Speed, 5f, $"AI right failed to accelerate on {cfg.lanes} lanes.");
                Assert.Greater(policeCar.Physics.Speed, 5f, $"Police failed to accelerate on {cfg.lanes} lanes.");
            }
        }

        [Test]
        public void TrackObstacleSystem_SpawnsEveryObstacleInsideValidLanesOnTwoFourAndSixLaneMaps()
        {
            var configs = new (int lanes, float laneWidth)[]
            {
                (2, 3.2f),
                (4, 4.0f),
                (6, 3.8f)
            };

            int[] seeds = { 17, 99, 2026 };

            for (int c = 0; c < configs.Length; c++)
            {
                var cfg = configs[c];
                WaypointTrack track = CreateTrack(cfg.lanes, cfg.laneWidth, out TrackDefinition def);

                GameObject obsGo = TrackCleanup(new GameObject($"ObsHost_{cfg.lanes}L"));
                TrackObstacleSystem obsSystem = obsGo.AddComponent<TrackObstacleSystem>();
                obsSystem.ApplyMapSettings(def);

                float halfTrack = track.TrackWidth * 0.5f;
                float maxExpectedBound = track.MaxLaneCenterOffset + track.LaneWidth * 0.22f + 0.0001f;

                for (int s = 0; s < seeds.Length; s++)
                {
                    obsSystem.SpawnObstacles(track, seeds[s]);
                    Assert.AreEqual(def.ObstacleCount, obsSystem.Obstacles.Count,
                        $"Expected {def.ObstacleCount} obstacles on {cfg.lanes} lanes.");

                    for (int i = 0; i < obsSystem.Obstacles.Count; i++)
                    {
                        float lat = obsSystem.Obstacles[i].LateralOffset;
                        Assert.LessOrEqual(Mathf.Abs(lat), halfTrack,
                            $"Obstacle {i} exceeded TrackWidth*0.5f on {cfg.lanes} lanes.");
                        Assert.LessOrEqual(Mathf.Abs(lat), maxExpectedBound,
                            $"Obstacle {i} exceeded outer lane jitter bound on {cfg.lanes} lanes.");

                        float minDistToLaneCenter = float.MaxValue;
                        for (int lane = 0; lane < track.LaneCount; lane++)
                        {
                            float d = Mathf.Abs(lat - track.GetLaneCenterOffset(lane));
                            if (d < minDistToLaneCenter)
                            {
                                minDistToLaneCenter = d;
                            }
                        }

                        Assert.LessOrEqual(minDistToLaneCenter, track.LaneWidth * 0.22f + 0.001f,
                            $"Obstacle {i} on {cfg.lanes} lanes was not centered inside any valid lane.");
                    }
                }
            }
        }

        [Test]
        public void RoadRashRenderer2D_GetNormalizedLaneDividerOffsets_ScalesSymmetricDividersForTwoFourAndSixLanes()
        {
            float inset = RoadRashRenderer2D.RoadEdgeNormalizedInset;
            Assert.AreEqual(0.92f, inset, 0.0001f);

            // 2 lanes -> 1 center double-yellow divider at 0
            float[] div2 = RoadRashRenderer2D.GetNormalizedLaneDividerOffsets(2);
            Assert.AreEqual(1, div2.Length);
            Assert.AreEqual(0f, div2[0], 0.0001f);
            Assert.IsTrue(RoadRashRenderer2D.IsCenterDoubleYellowDivider(2, 0));

            // 4 lanes -> 3 dividers symmetrically dividing the road into 4 equal strips
            float[] div4 = RoadRashRenderer2D.GetNormalizedLaneDividerOffsets(4);
            Assert.AreEqual(3, div4.Length);
            Assert.AreEqual(-0.5f * inset, div4[0], 0.0001f);
            Assert.AreEqual(0f, div4[1], 0.0001f);
            Assert.AreEqual(0.5f * inset, div4[2], 0.0001f);
            Assert.IsFalse(RoadRashRenderer2D.IsCenterDoubleYellowDivider(4, 0));
            Assert.IsTrue(RoadRashRenderer2D.IsCenterDoubleYellowDivider(4, 1));
            Assert.IsFalse(RoadRashRenderer2D.IsCenterDoubleYellowDivider(4, 2));

            // 6 lanes -> 5 dividers symmetrically dividing the road into 6 equal strips
            float[] div6 = RoadRashRenderer2D.GetNormalizedLaneDividerOffsets(6);
            Assert.AreEqual(5, div6.Length);
            float expectedStep6 = (2f * inset) / 6f;
            float[] expected6 =
            {
                -(2f / 3f) * inset,
                -(1f / 3f) * inset,
                0f,
                (1f / 3f) * inset,
                (2f / 3f) * inset
            };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(expected6[i], div6[i], 0.0001f, $"6-lane divider {i} mismatch.");
                Assert.AreEqual(-div6[i], div6[4 - i], 0.0001f, "6-lane dividers must be symmetric.");
                Assert.Less(Mathf.Abs(div6[i]), inset, "Dividers must stay strictly inside road edge inset.");
                if (i + 1 < 5)
                {
                    Assert.AreEqual(expectedStep6, div6[i + 1] - div6[i], 0.0001f,
                        "6-lane dividers must have uniform spacing.");
                }
                Assert.AreEqual(i == 2, RoadRashRenderer2D.IsCenterDoubleYellowDivider(6, i));
            }

            // Clamping edge cases (< 2 clamps to 2; > 6 clamps to 6)
            Assert.AreEqual(1, RoadRashRenderer2D.GetNormalizedLaneDividerOffsets(1).Length);
            Assert.AreEqual(5, RoadRashRenderer2D.GetNormalizedLaneDividerOffsets(9).Length);
        }
    }
}
