using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class CityCornerNavigationComparisonTests
    {
        private struct ModeMetrics
        {
            public AINavigationMode Mode;
            public int Runs;
            public int CompletedRuns;
            public int TotalStuckCount;
            public int TotalOffTrackSteps;
            public int TotalSteps;
            public float MaxAbsLateralOffset;
            public float MeanAbsLaneError;
            public float MeanLapTimeSeconds;
        }

        [Test]
        public void CompareNavigationModes_OnCityAndSharp90DegreeCorners_LogsMetricsAndVerifiesBest()
        {
            AINavigationMode[] modes =
            {
                AINavigationMode.LegacyFixed,
                AINavigationMode.SpeedAdaptiveWaypoint,
                AINavigationMode.SmoothedSplinePath
            };

            var citySummaries = new Dictionary<AINavigationMode, ModeMetrics>();
            var sharp90Summaries = new Dictionary<AINavigationMode, ModeMetrics>();

            for (int m = 0; m < modes.Length; m++)
            {
                AINavigationMode mode = modes[m];
                citySummaries[mode] = EvaluateCityMapSuite(mode);
                sharp90Summaries[mode] = EvaluateSharp90IntersectionSuite(mode);

                ModeMetrics c = citySummaries[mode];
                ModeMetrics s = sharp90Summaries[mode];
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[NavCompare:{0}] City(done={1}/{2}, stuck={3}, offTrack={4}/{5} [{6:F2}%], maxLat={7:F2}m, laneErr={8:F2}m, avgLap={9:F2}s) | Sharp90(done={10}/{11}, stuck={12}, offTrack={13}/{14} [{15:F2}%], maxLat={16:F2}m, laneErr={17:F2}m, avgLap={18:F2}s)",
                    mode,
                    c.CompletedRuns, c.Runs, c.TotalStuckCount, c.TotalOffTrackSteps, c.TotalSteps,
                    c.TotalSteps > 0 ? (100f * c.TotalOffTrackSteps / c.TotalSteps) : 0f,
                    c.MaxAbsLateralOffset, c.MeanAbsLaneError, c.MeanLapTimeSeconds,
                    s.CompletedRuns, s.Runs, s.TotalStuckCount, s.TotalOffTrackSteps, s.TotalSteps,
                    s.TotalSteps > 0 ? (100f * s.TotalOffTrackSteps / s.TotalSteps) : 0f,
                    s.MaxAbsLateralOffset, s.MeanAbsLaneError, s.MeanLapTimeSeconds));
            }

            // Both proposed directions must finish all City & Sharp-90 runs with STUCK=0
            for (int m = 1; m < modes.Length; m++)
            {
                AINavigationMode mode = modes[m];
                Assert.AreEqual(citySummaries[mode].Runs, citySummaries[mode].CompletedRuns,
                    $"{mode} must complete every City run.");
                Assert.AreEqual(0, citySummaries[mode].TotalStuckCount,
                    $"{mode} must have STUCK=0 across all City runs.");
                Assert.AreEqual(sharp90Summaries[mode].Runs, sharp90Summaries[mode].CompletedRuns,
                    $"{mode} must complete every Sharp-90 intersection run.");
                Assert.AreEqual(0, sharp90Summaries[mode].TotalStuckCount,
                    $"{mode} must have STUCK=0 across all Sharp-90 intersection runs.");
            }

            // Chosen best mode (default on AIRacerDriver) must outperform LegacyFixed on off-track/corner cutting & lane tracking
            var probeGo = new GameObject("ProbeAI");
            AINavigationMode defaultMode;
            try
            {
                defaultMode = probeGo.AddComponent<AIRacerDriver>().NavigationMode;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probeGo);
            }

            ModeMetrics bestCity = citySummaries[defaultMode];
            ModeMetrics legacyCity = citySummaries[AINavigationMode.LegacyFixed];
            ModeMetrics bestSharp = sharp90Summaries[defaultMode];
            ModeMetrics legacySharp = sharp90Summaries[AINavigationMode.LegacyFixed];

            Assert.LessOrEqual(bestCity.TotalOffTrackSteps, legacyCity.TotalOffTrackSteps,
                $"Default mode {defaultMode} should cut corners / go off-track less than LegacyFixed on City.");
            Assert.LessOrEqual(bestSharp.TotalOffTrackSteps, legacySharp.TotalOffTrackSteps,
                $"Default mode {defaultMode} should cut corners / go off-track less than LegacyFixed on Sharp-90 corners.");
            Assert.Less(bestSharp.MeanAbsLaneError, legacySharp.MeanAbsLaneError,
                $"Default mode {defaultMode} should track the target lane tighter than LegacyFixed on Sharp-90 corners.");
        }

        private static ModeMetrics EvaluateCityMapSuite(AINavigationMode mode)
        {
            MapTheme theme = MapTheme.CreateCityPreset();
            TrackDefinition def = TrackDefinition.CreateCityPreset(theme);
            try
            {
                float[] skills = { 0.4f, 0.7f, 1.0f };
                float[] lateralOffsets = { -5f, 0f, 5f };
                return RunMatrix(mode, def, skills, lateralOffsets, laps: 2);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        private static ModeMetrics EvaluateSharp90IntersectionSuite(AINavigationMode mode)
        {
            // Explicit tight 90-degree urban intersection block grid (45m-65m city blocks, 4 lanes = 16m width)
            Vector2[] sharp90Waypoints =
            {
                new Vector2(  0f,   0f),
                new Vector2( 80f,   0f),
                new Vector2(150f,   0f),
                new Vector2(150f,  75f),
                new Vector2(150f, 150f),
                new Vector2( 75f, 150f),
                new Vector2( 75f, 215f),
                new Vector2(  0f, 215f),
                new Vector2(-70f, 215f),
                new Vector2(-70f, 130f),
                new Vector2(-70f,  55f),
                new Vector2(-70f,   0f)
            };

            TrackDefinition def = TrackDefinition.CreateRuntime(
                "sharp_90_grid",
                "Sharp 90 Grid",
                sharp90Waypoints,
                lanes: 4,
                widthPerLane: 4.0f,
                mapTheme: null);

            try
            {
                float[] skills = { 0.4f, 0.7f, 1.0f };
                float[] lateralOffsets = { -5f, 0f, 5f };
                return RunMatrix(mode, def, skills, lateralOffsets, laps: 2);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
            }
        }

        private static ModeMetrics RunMatrix(
            AINavigationMode mode,
            TrackDefinition def,
            float[] skills,
            float[] lateralOffsets,
            int laps)
        {
            var agg = new ModeMetrics { Mode = mode };
            float laneErrSum = 0f;
            float lapTimeSum = 0f;
            int completedLapCount = 0;

            for (int si = 0; si < skills.Length; si++)
            {
                for (int li = 0; li < lateralOffsets.Length; li++)
                {
                    agg.Runs++;
                    SimulateSingleRun(
                        mode,
                        def,
                        skills[si],
                        lateralOffsets[li],
                        laps,
                        out bool completed,
                        out int stuck,
                        out int offTrackSteps,
                        out int steps,
                        out float maxAbsLat,
                        out float meanLaneErr,
                        out float totalTime);

                    if (completed)
                    {
                        agg.CompletedRuns++;
                        lapTimeSum += totalTime / laps;
                        completedLapCount++;
                    }
                    agg.TotalStuckCount += stuck;
                    agg.TotalOffTrackSteps += offTrackSteps;
                    agg.TotalSteps += steps;
                    if (maxAbsLat > agg.MaxAbsLateralOffset) agg.MaxAbsLateralOffset = maxAbsLat;
                    laneErrSum += meanLaneErr;
                }
            }

            agg.MeanAbsLaneError = agg.Runs > 0 ? laneErrSum / agg.Runs : 0f;
            agg.MeanLapTimeSeconds = completedLapCount > 0 ? lapTimeSum / completedLapCount : 0f;
            return agg;
        }

        private static void SimulateSingleRun(
            AINavigationMode mode,
            TrackDefinition def,
            float skill,
            float lateralOffset,
            int laps,
            out bool completed,
            out int stuckCount,
            out int offTrackSteps,
            out int executedSteps,
            out float maxAbsLat,
            out float meanLaneErr,
            out float totalTime)
        {
            var trackGo = new GameObject($"CompareTrack_{mode}");
            var managerGo = new GameObject($"CompareLapMgr_{mode}");
            var aiGo = new GameObject($"CompareAI_{mode}");

            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.ApplyTrackDefinition(def);

                var lapMgr = managerGo.AddComponent<LapCheckpointManager>();
                var car = aiGo.AddComponent<Car>();
                var ai = aiGo.AddComponent<AIRacerDriver>();
                ai.SetNavigationMode(mode);
                ai.Configure($"Sim_{mode}_{skill:F1}", skill, lateralOffset, 2026);
                ai.SetMapCornerSpeedMultiplier(def.AiCornerSpeedMultiplier);

                Vector2 startDir = track.GetTrackDirection(0);
                Vector2 startRight = new Vector2(startDir.y, -startDir.x);
                Vector2 startPos = track.GetWaypoint(0) + startRight * lateralOffset;
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                car.Initialize(ai, startPos, startHeading, track);
                lapMgr.Initialize(track, new[] { car }, laps);
                RacerProgress progress = lapMgr.GetProgress(car);

                const float dt = 1f / 50f;
                const int maxSteps = 50 * 220;
                float maxPossibleStepDist = car.Physics.MaxSpeed * dt * 2.2f + 1.0f;
                float lowSpeedStreak = 0f;
                bool lowSpeedCounted = false;
                float sumLaneErr = 0f;

                stuckCount = 0;
                offTrackSteps = 0;
                executedSteps = 0;
                maxAbsLat = 0f;

                for (int step = 0; step < maxSteps; step++)
                {
                    Vector2 posBefore = car.Physics.Position;
                    car.StepPhysics(dt, track);
                    lapMgr.Tick(dt);
                    executedSteps = step + 1;

                    if (Vector2.Distance(posBefore, car.Physics.Position) > maxPossibleStepDist)
                    {
                        stuckCount++;
                    }

                    if (executedSteps * dt >= 1.5f)
                    {
                        if (car.Physics.Speed < 2.5f)
                        {
                            lowSpeedStreak += dt;
                            if (lowSpeedStreak >= 2.5f && !lowSpeedCounted)
                            {
                                stuckCount++;
                                lowSpeedCounted = true;
                            }
                        }
                        else
                        {
                            lowSpeedStreak = 0f;
                            lowSpeedCounted = false;
                        }
                    }

                    if (!car.IsOnTrack) offTrackSteps++;
                    float absLat = Mathf.Abs(car.LateralOffset);
                    if (absLat > maxAbsLat) maxAbsLat = absLat;
                    sumLaneErr += Mathf.Abs(car.LateralOffset - lateralOffset);

                    if (progress.CompletedLaps >= laps && progress.IsFinished)
                    {
                        break;
                    }
                }

                completed = progress.CompletedLaps >= laps && progress.IsFinished;
                meanLaneErr = executedSteps > 0 ? sumLaneErr / executedSteps : 0f;
                totalTime = executedSteps * dt;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aiGo);
                UnityEngine.Object.DestroyImmediate(managerGo);
                UnityEngine.Object.DestroyImmediate(trackGo);
            }
        }
    }
}
