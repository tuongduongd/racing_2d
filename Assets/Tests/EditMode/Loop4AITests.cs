using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class Loop4AITests
    {
        [Test]
        public void AIRacerDriver_InheritsDriverAndCompletesFullLapWithoutGettingStuck()
        {
            var trackGo = new GameObject("TestTrack");
            var aiGo = new GameObject("TestAI");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var car = aiGo.AddComponent<Car>();
                var aiDriver = aiGo.AddComponent<AIRacerDriver>();
                aiDriver.Configure("Viper", 0.90f, 0f);

                // Polymorphism verification
                Assert.IsInstanceOf<Driver>(aiDriver);

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                car.Initialize(aiDriver, startPos, startHeading, track);

                var visitedSegments = new HashSet<int>();
                float dt = 1f / 50f; // 50 Hz fixed step
                int maxSteps = 50 * 45; // up to 45 simulated seconds
                bool completedLap = false;

                for (int step = 0; step < maxSteps; step++)
                {
                    car.StepPhysics(dt, track);
                    visitedSegments.Add(car.CurrentWaypointIndex);

                    if (visitedSegments.Count >= track.WaypointCount - 1 && car.CurrentWaypointIndex == 0 && step > 250)
                    {
                        completedLap = true;
                        break;
                    }
                }

                Assert.IsTrue(completedLap,
                    $"AI should complete a full lap of {track.WaypointCount} waypoints without getting stuck. Visited: {visitedSegments.Count}");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void AIRacerDriver_HigherSkillCoversMoreDistanceThanLowerSkill()
        {
            var trackGo = new GameObject("TestTrack");
            var fastGo = new GameObject("FastAI");
            var slowGo = new GameObject("SlowAI");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                var fastCar = fastGo.AddComponent<Car>();
                var fastAi = fastGo.AddComponent<AIRacerDriver>();
                fastAi.Configure("Pro", 0.98f, -2f);
                fastCar.Initialize(fastAi, startPos, startHeading, track);

                var slowCar = slowGo.AddComponent<Car>();
                var slowAi = slowGo.AddComponent<AIRacerDriver>();
                slowAi.Configure("Rookie", 0.50f, 2f);
                slowCar.Initialize(slowAi, startPos, startHeading, track);

                float dt = 1f / 50f;
                float fastAccum = 0f;
                float slowAccum = 0f;

                for (int step = 0; step < 50 * 12; step++)
                {
                    Vector2 prevFast = fastCar.Physics.Position;
                    Vector2 prevSlow = slowCar.Physics.Position;

                    fastCar.StepPhysics(dt, track);
                    slowCar.StepPhysics(dt, track);

                    fastAccum += Vector2.Distance(prevFast, fastCar.Physics.Position);
                    slowAccum += Vector2.Distance(prevSlow, slowCar.Physics.Position);
                }

                Assert.Greater(fastAccum, slowAccum,
                    $"High-skill AI ({fastAccum:F1}m) should travel farther than low-skill AI ({slowAccum:F1}m).");
            }
            finally
            {
                Object.DestroyImmediate(slowGo);
                Object.DestroyImmediate(fastGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void AIRacerDriver_AIProfileDifficulties_OrderPerformanceHardGreaterThanNormalGreaterThanEasy()
        {
            var trackGo = new GameObject("TestTrack");
            var easyGo = new GameObject("EasyAI");
            var normalGo = new GameObject("NormalAI");
            var hardGo = new GameObject("HardAI");

            AIProfile easyProfile = AIProfile.CreateDefault(DifficultyLevel.Easy);
            AIProfile normalProfile = AIProfile.CreateDefault(DifficultyLevel.Normal);
            AIProfile hardProfile = AIProfile.CreateDefault(DifficultyLevel.Hard);

            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                var easyCar = easyGo.AddComponent<Car>();
                var easyAi = easyGo.AddComponent<AIRacerDriver>();
                easyAi.Configure("EasyRacer", easyProfile, 0f);
                easyCar.Initialize(easyAi, startPos, startHeading, track);

                var normalCar = normalGo.AddComponent<Car>();
                var normalAi = normalGo.AddComponent<AIRacerDriver>();
                normalAi.Configure("NormalRacer", normalProfile, 0f);
                normalCar.Initialize(normalAi, startPos, startHeading, track);

                var hardCar = hardGo.AddComponent<Car>();
                var hardAi = hardGo.AddComponent<AIRacerDriver>();
                hardAi.Configure("HardRacer", hardProfile, 0f);
                hardCar.Initialize(hardAi, startPos, startHeading, track);

                float dt = 1f / 50f;
                float easyDist = 0f;
                float normalDist = 0f;
                float hardDist = 0f;

                for (int step = 0; step < 50 * 12; step++)
                {
                    Vector2 prevEasy = easyCar.Physics.Position;
                    Vector2 prevNormal = normalCar.Physics.Position;
                    Vector2 prevHard = hardCar.Physics.Position;

                    easyCar.StepPhysics(dt, track);
                    normalCar.StepPhysics(dt, track);
                    hardCar.StepPhysics(dt, track);

                    easyDist += Vector2.Distance(prevEasy, easyCar.Physics.Position);
                    normalDist += Vector2.Distance(prevNormal, normalCar.Physics.Position);
                    hardDist += Vector2.Distance(prevHard, hardCar.Physics.Position);
                }

                Assert.Greater(normalDist, easyDist,
                    $"Normal AI ({normalDist:F1}m) should travel farther than Easy AI ({easyDist:F1}m).");
                Assert.Greater(hardDist, normalDist,
                    $"Hard AI ({hardDist:F1}m) should travel farther than Normal AI ({normalDist:F1}m).");
            }
            finally
            {
                Object.DestroyImmediate(easyProfile);
                Object.DestroyImmediate(normalProfile);
                Object.DestroyImmediate(hardProfile);
                Object.DestroyImmediate(hardGo);
                Object.DestroyImmediate(normalGo);
                Object.DestroyImmediate(easyGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void AIRacerDriver_NullProfileFallback_StillDrivesWithoutError()
        {
            var trackGo = new GameObject("TestTrack");
            var aiGo = new GameObject("FallbackAI");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var car = aiGo.AddComponent<Car>();
                var ai = aiGo.AddComponent<AIRacerDriver>();
                ai.Configure("Fallback", (AIProfile)null, 0f);

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                car.Initialize(ai, startPos, startHeading, track);
                for (int i = 0; i < 60; i++)
                {
                    car.StepPhysics(1f / 50f, track);
                }

                Assert.Greater(Vector2.Distance(startPos, car.Physics.Position), 5f,
                    "AI with null profile should fall back cleanly and drive forward.");
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void AIRacerDriver_StuckRecovery_RecoversOntoTrackAlignedAndAdvancesWaypointAfterTimeout()
        {
            var trackGo = new GameObject("TestTrack");
            var aiGo = new GameObject("StuckAI");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var car = aiGo.AddComponent<Car>();
                var aiDriver = aiGo.AddComponent<AIRacerDriver>();
                aiDriver.Configure("RecoveryAI", 0.90f, 0f);

                const int recoverSegmentIndex = 3;
                Vector2 segDir = track.GetTrackDirection(recoverSegmentIndex);
                Vector2 segRight = new Vector2(segDir.y, -segDir.x);
                Vector2 stuckPos = Vector2.Lerp(
                    track.GetWaypoint(recoverSegmentIndex),
                    track.GetWaypoint(recoverSegmentIndex + 1),
                    0.35f) + segRight * 12f;
                float wrongHeading = Mathf.Atan2(-segDir.y, -segDir.x);

                car.Initialize(aiDriver, stuckPos, wrongHeading, track);
                Assert.IsFalse(car.IsOnTrack, "Car should start off-track before recovery.");
                Assert.AreEqual(recoverSegmentIndex, car.CurrentWaypointIndex);
                Assert.AreEqual(1, aiDriver.TargetWaypointIndex);

                float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
                int preTimeoutSteps = Mathf.FloorToInt(2.4f / dt);
                for (int i = 0; i < preTimeoutSteps; i++)
                {
                    aiDriver.DecideInput(car, track);
                }

                Assert.Less(Vector2.Distance(car.Physics.Position, stuckPos), 0.01f,
                    "Car must not recover before stuckTimeout (2.5s) elapses.");
                Assert.AreEqual(1, aiDriver.TargetWaypointIndex);

                Vector2 expectedRecoverPos = track.GetWaypoint(recoverSegmentIndex);
                bool recovered = false;
                VehicleInput recoveryInput = default;
                int extraSteps = Mathf.CeilToInt(0.3f / dt);
                for (int i = 0; i < extraSteps; i++)
                {
                    VehicleInput input = aiDriver.DecideInput(car, track);
                    if (Vector2.Distance(car.Physics.Position, expectedRecoverPos) < 0.01f)
                    {
                        recovered = true;
                        recoveryInput = input;
                        break;
                    }
                }

                Assert.IsTrue(recovered, "AI should recover onto the track once stuck duration >= stuckTimeout (2.5s).");
                Assert.IsTrue(car.IsOnTrack, "Recovered car should be back on the track.");
                Assert.Greater(Vector2.Dot(car.Physics.Forward, segDir), 0.99f,
                    "Recovered car should be aligned with the track direction.");
                int expectedNextWaypoint = (recoverSegmentIndex + 1) % track.WaypointCount;
                Assert.AreEqual(expectedNextWaypoint, aiDriver.TargetWaypointIndex,
                    "Stuck recovery should advance TargetWaypointIndex to the next waypoint.");
                Assert.Greater(recoveryInput.throttle, 0f,
                    "Stuck recovery should output positive throttle.");

                // Verify stuck timer was reset so a subsequent brief stall does not immediately re-trigger recovery.
                const int secondSegmentIndex = 6;
                Vector2 segDir2 = track.GetTrackDirection(secondSegmentIndex);
                Vector2 segRight2 = new Vector2(segDir2.y, -segDir2.x);
                Vector2 stuckPos2 = Vector2.Lerp(
                    track.GetWaypoint(secondSegmentIndex),
                    track.GetWaypoint(secondSegmentIndex + 1),
                    0.4f) + segRight2 * 12f;

                car.ResetToTrackPoint(stuckPos2, wrongHeading, track);
                for (int i = 0; i < 10; i++)
                {
                    aiDriver.DecideInput(car, track);
                }

                Assert.Less(Vector2.Distance(car.Physics.Position, stuckPos2), 0.01f,
                    "Stuck timer should be reset after recovery rather than triggering again immediately.");
                Assert.AreEqual(expectedNextWaypoint, aiDriver.TargetWaypointIndex);
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void AIRacerDriver_BriefSlowDownShorterThanTimeout_DoesNotTriggerPrematureRecovery()
        {
            var trackGo = new GameObject("TestTrack");
            var aiGo = new GameObject("SlowAI");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var car = aiGo.AddComponent<Car>();
                var aiDriver = aiGo.AddComponent<AIRacerDriver>();
                aiDriver.Configure("BriefSlowAI", 0.90f, 0f);

                const int segmentIndex = 3;
                Vector2 segDir = track.GetTrackDirection(segmentIndex);
                Vector2 segRight = new Vector2(segDir.y, -segDir.x);
                Vector2 stuckPos = Vector2.Lerp(
                    track.GetWaypoint(segmentIndex),
                    track.GetWaypoint(segmentIndex + 1),
                    0.35f) + segRight * 12f;
                float wrongHeading = Mathf.Atan2(-segDir.y, -segDir.x);

                car.Initialize(aiDriver, stuckPos, wrongHeading, track);

                float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
                int twoSecondSteps = Mathf.FloorToInt(2.0f / dt);

                for (int i = 0; i < twoSecondSteps; i++)
                {
                    aiDriver.DecideInput(car, track);
                }

                Assert.Less(Vector2.Distance(car.Physics.Position, stuckPos), 0.01f,
                    "2.0s below stuckSpeedThreshold must not trigger recovery.");

                // Briefly move faster than stuckSpeedThreshold (2.5f) to reset stuck timer.
                car.Physics.ApplyVelocityImpulse(segDir * 4f);
                aiDriver.DecideInput(car, track);
                car.Physics.ScaleVelocity(0f);

                for (int i = 0; i < twoSecondSteps; i++)
                {
                    aiDriver.DecideInput(car, track);
                }

                Assert.Less(Vector2.Distance(car.Physics.Position, stuckPos), 0.01f,
                    "A brief speed-up should reset the stuck timer so a second <2.5s stall does not recover prematurely.");
                Assert.AreEqual(1, aiDriver.TargetWaypointIndex);
            }
            finally
            {
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void AIRacerDriver_VehicleAvoidance_SteersLeftAwayFromRightSideBlocker()
        {
            RunAvoidanceComparison(
                new[] { new Vector2(67f, -1.5f) },
                out float clearSteer,
                out float clearLateral,
                out float testSteer,
                out float testLateral);

            Assert.Less(testSteer, clearSteer - 0.05f,
                $"AI should steer left (more negative steering: {testSteer:F3} vs {clearSteer:F3}) to avoid a right-side blocker.");
            Assert.Less(testLateral, clearLateral - 0.05f,
                $"AI should move laterally left ({testLateral:F3} vs {clearLateral:F3}) away from a right-side blocker.");
        }

        [Test]
        public void AIRacerDriver_VehicleAvoidance_SteersRightAwayFromLeftSideBlocker()
        {
            RunAvoidanceComparison(
                new[] { new Vector2(67f, 1.5f) },
                out float clearSteer,
                out float clearLateral,
                out float testSteer,
                out float testLateral);

            Assert.Greater(testSteer, clearSteer + 0.05f,
                $"AI should steer right (more positive steering: {testSteer:F3} vs {clearSteer:F3}) to avoid a left-side blocker.");
            Assert.Greater(testLateral, clearLateral + 0.05f,
                $"AI should move laterally right ({testLateral:F3} vs {clearLateral:F3}) away from a left-side blocker.");
        }

        [Test]
        public void AIRacerDriver_VehicleAvoidance_IgnoresCarsBeyondLookAheadOrLateralRange()
        {
            RunAvoidanceComparison(
                new[]
                {
                    new Vector2(82f, 0f),   // > avoidanceLookAhead (20f) ahead of start (55f)
                    new Vector2(67f, -4.5f) // |dLat| = 4.5f > avoidanceLateralRange (3.5f)
                },
                out float clearSteer,
                out float clearLateral,
                out float testSteer,
                out float testLateral);

            Assert.AreEqual(clearSteer, testSteer, 0.001f,
                "Cars beyond avoidanceLookAhead or avoidanceLateralRange should not alter steering.");
            Assert.AreEqual(clearLateral, testLateral, 0.001f,
                "Cars beyond avoidanceLookAhead or avoidanceLateralRange should not alter lateral position.");
        }

        private static void RunAvoidanceComparison(
            Vector2[] blockerPositions,
            out float clearSteer,
            out float clearLateral,
            out float testSteer,
            out float testLateral)
        {
            var trackGo = new GameObject("TestTrack");
            var clearGo = new GameObject("ClearAI");
            var testGo = new GameObject("AvoidAI");
            var blockerGos = new List<GameObject>(blockerPositions.Length);

            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                Vector2 startPos = new Vector2(55f, 0f);
                const float startHeading = 0f;

                var clearCar = clearGo.AddComponent<Car>();
                var clearAi = clearGo.AddComponent<AIRacerDriver>();
                clearAi.Configure("AvoidTester", 0.95f, 0f);
                clearCar.Initialize(clearAi, startPos, startHeading, track);
                clearAi.SetAllCars(new[] { clearCar });

                var testCar = testGo.AddComponent<Car>();
                var testAi = testGo.AddComponent<AIRacerDriver>();
                testAi.Configure("AvoidTester", 0.95f, 0f);
                testCar.Initialize(testAi, startPos, startHeading, track);

                var allCars = new List<Car>(1 + blockerPositions.Length) { testCar };
                for (int i = 0; i < blockerPositions.Length; i++)
                {
                    var blockerGo = new GameObject($"Blocker_{i}");
                    blockerGos.Add(blockerGo);
                    var blockerCar = blockerGo.AddComponent<Car>();
                    blockerCar.Initialize(null, blockerPositions[i], startHeading, track);
                    allCars.Add(blockerCar);
                }
                testAi.SetAllCars(allCars);

                const float dt = 1f / 50f;
                const int steps = 25;
                for (int step = 0; step < steps; step++)
                {
                    clearCar.StepPhysics(dt, track);
                    testCar.StepPhysics(dt, track);
                }

                clearSteer = clearCar.LastInput.steering;
                clearLateral = clearCar.LateralOffset;
                testSteer = testCar.LastInput.steering;
                testLateral = testCar.LateralOffset;
            }
            finally
            {
                for (int i = 0; i < blockerGos.Count; i++)
                {
                    Object.DestroyImmediate(blockerGos[i]);
                }
                Object.DestroyImmediate(testGo);
                Object.DestroyImmediate(clearGo);
                Object.DestroyImmediate(trackGo);
            }
        }
    }
}
