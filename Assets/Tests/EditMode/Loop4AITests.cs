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
    }
}
