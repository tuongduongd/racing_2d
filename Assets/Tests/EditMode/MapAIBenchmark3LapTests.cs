using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class MapAIBenchmark3LapTests
    {
        private struct ThreeLapRunResult
        {
            public string MapKey;
            public string MapDisplayName;
            public float Skill;
            public int Seed;
            public int Waypoints;
            public float TrackLengthMeters;
            public int CheckpointsPerLap;
            public int TotalCheckpointsPassed;
            public bool CheckpointsInOrder;
            public int StuckCount;
            public int OffTrackSteps;
            public int TotalSteps;
            public float TotalTimeSeconds;
            public float[] LapTimes;
            public float AverageLapTimeSeconds;
            public float BestLapTimeSeconds;
            public float AverageSpeedMps;
            public float PeakSpeedMps;
        }

        private sealed class MapBenchmarkReportData
        {
            public string MapKey;
            public string MapTitle;
            public int Waypoints;
            public float TrackLengthMeters;
            public int CheckpointsPerLap;
            public float Dt;
            public int Seed;
            public List<ThreeLapRunResult> Results = new List<ThreeLapRunResult>();
        }

        private static readonly Dictionary<string, MapBenchmarkReportData> s_ReportsByMap =
            new Dictionary<string, MapBenchmarkReportData>(StringComparer.OrdinalIgnoreCase);

        [Test]
        public void Valley_AI_Skills_04_07_10_Complete_3_Laps_NoStuck_OrderedCheckpoints()
        {
            RunAndVerifyMapThreeLaps(
                mapKey: "Valley",
                mapTitle: "Valley (Thung Lũng Cổ Điển)",
                createTheme: MapTheme.CreateValleyPreset,
                createTrackDef: TrackDefinition.CreateValleyDefaultPreset);
        }

        [Test]
        public void City_AI_Skills_04_07_10_Complete_3_Laps_NoStuck_OrderedCheckpoints()
        {
            RunAndVerifyMapThreeLaps(
                mapKey: "City",
                mapTitle: "City (Thành Phố Đêm Neon)",
                createTheme: MapTheme.CreateCityPreset,
                createTrackDef: TrackDefinition.CreateCityPreset);
        }

        private static void RunAndVerifyMapThreeLaps(
            string mapKey,
            string mapTitle,
            Func<MapTheme> createTheme,
            Func<MapTheme, TrackDefinition> createTrackDef)
        {
            const int benchmarkSeed = 2026;
            const float dt = 1f / 50f;
            float[] skills = { 0.4f, 0.7f, 1.0f };
            var results = new List<ThreeLapRunResult>(skills.Length);

            for (int i = 0; i < skills.Length; i++)
            {
                ThreeLapRunResult result = SimulateThreeLaps(
                    mapKey,
                    mapTitle,
                    skills[i],
                    benchmarkSeed,
                    dt,
                    createTheme,
                    createTrackDef);

                results.Add(result);

                Assert.AreEqual(0, result.StuckCount,
                    $"[{mapKey}] AI skill {result.Skill:F1} must have STUCK=0 (got {result.StuckCount}).");
                Assert.IsTrue(result.CheckpointsInOrder,
                    $"[{mapKey}] AI skill {result.Skill:F1} must pass all checkpoints in strict sequential order.");
                Assert.AreEqual(3 * result.CheckpointsPerLap, result.TotalCheckpointsPassed,
                    $"[{mapKey}] AI skill {result.Skill:F1} must pass 3 laps * {result.CheckpointsPerLap} checkpoints.");
                Assert.AreEqual(3, result.LapTimes.Length,
                    $"[{mapKey}] AI skill {result.Skill:F1} must record exactly 3 lap times.");

                for (int lap = 0; lap < result.LapTimes.Length; lap++)
                {
                    Assert.Greater(result.LapTimes[lap], 0f,
                        $"[{mapKey}] AI skill {result.Skill:F1} lap {lap + 1} time must be positive.");
                }
            }

            Assert.Less(results[1].TotalTimeSeconds, results[0].TotalTimeSeconds,
                $"[{mapKey}] Skill 0.7 ({results[1].TotalTimeSeconds:F2}s) should finish 3 laps faster than skill 0.4 ({results[0].TotalTimeSeconds:F2}s).");
            Assert.Less(results[2].TotalTimeSeconds, results[1].TotalTimeSeconds,
                $"[{mapKey}] Skill 1.0 ({results[2].TotalTimeSeconds:F2}s) should finish 3 laps faster than skill 0.7 ({results[1].TotalTimeSeconds:F2}s).");

            s_ReportsByMap[mapKey] = new MapBenchmarkReportData
            {
                MapKey = mapKey,
                MapTitle = mapTitle,
                Waypoints = results[0].Waypoints,
                TrackLengthMeters = results[0].TrackLengthMeters,
                CheckpointsPerLap = results[0].CheckpointsPerLap,
                Dt = dt,
                Seed = benchmarkSeed,
                Results = results
            };

            WriteExperimentMarkdown();

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[{0} 3-Lap Benchmark Seed={1}] 0.4 avg/lap={2:F2}s (STUCK={3}) | 0.7 avg/lap={4:F2}s (STUCK={5}) | 1.0 avg/lap={6:F2}s (STUCK={7})",
                mapKey,
                benchmarkSeed,
                results[0].AverageLapTimeSeconds, results[0].StuckCount,
                results[1].AverageLapTimeSeconds, results[1].StuckCount,
                results[2].AverageLapTimeSeconds, results[2].StuckCount));
        }

        private static ThreeLapRunResult SimulateThreeLaps(
            string mapKey,
            string mapTitle,
            float skill,
            int seed,
            float dt,
            Func<MapTheme> createTheme,
            Func<MapTheme, TrackDefinition> createTrackDef)
        {
            var trackGo = new GameObject($"BenchTrack_{mapKey}_{skill:F1}");
            var managerGo = new GameObject($"BenchLapManager_{mapKey}_{skill:F1}");
            var aiGo = new GameObject($"BenchAI_{mapKey}_{skill:F1}");
            MapTheme theme = null;
            TrackDefinition trackDef = null;

            try
            {
                theme = createTheme();
                trackDef = createTrackDef(theme);

                var track = trackGo.AddComponent<WaypointTrack>();
                track.ApplyTrackDefinition(trackDef);

                var lapManager = managerGo.AddComponent<LapCheckpointManager>();
                var car = aiGo.AddComponent<Car>();
                var aiDriver = aiGo.AddComponent<AIRacerDriver>();
                aiDriver.Configure($"Benchmark_{mapKey}_{skill:F1}", skill, 0f, seed);
                aiDriver.SetMapCornerSpeedMultiplier(trackDef.AiCornerSpeedMultiplier);

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                car.Initialize(aiDriver, startPos, startHeading, track);
                lapManager.Initialize(track, new[] { car }, 3);

                RacerProgress progress = lapManager.GetProgress(car);
                int checkpointCount = lapManager.CheckpointCount;
                int expectedCheckpoint = 1 % checkpointCount;
                bool checkpointsInOrder = true;
                int passedCount = 0;
                var lapTimes = new List<float>(3);

                lapManager.CheckpointPassed += (_, cp) =>
                {
                    passedCount++;
                    if (cp.Index != expectedCheckpoint)
                    {
                        checkpointsInOrder = false;
                    }
                    expectedCheckpoint = (expectedCheckpoint + 1) % checkpointCount;
                };

                lapManager.LapCompleted += p =>
                {
                    lapTimes.Add(p.LastLapTime);
                };

                const int maxSteps = 50 * 400; // 400s safety cap for 3 laps
                int executedSteps = 0;
                int offTrackSteps = 0;
                int stuckCount = 0;
                float lowSpeedStreak = 0f;
                bool lowSpeedStuckCounted = false;
                float peakSpeed = 0f;
                float maxPossibleStepDist = car.Physics.MaxSpeed * dt * 2.2f + 1.0f;

                for (int step = 0; step < maxSteps; step++)
                {
                    Vector2 posBefore = car.Physics.Position;
                    car.StepPhysics(dt, track);
                    lapManager.Tick(dt);
                    executedSteps = step + 1;

                    float simTime = executedSteps * dt;
                    float distMoved = Vector2.Distance(posBefore, car.Physics.Position);
                    if (distMoved > maxPossibleStepDist)
                    {
                        // Teleport / stuck-recovery reset triggered
                        stuckCount++;
                    }

                    if (simTime >= 1.5f)
                    {
                        if (car.Physics.Speed < 2.5f)
                        {
                            lowSpeedStreak += dt;
                            if (lowSpeedStreak >= 2.5f && !lowSpeedStuckCounted)
                            {
                                stuckCount++;
                                lowSpeedStuckCounted = true;
                            }
                        }
                        else
                        {
                            lowSpeedStreak = 0f;
                            lowSpeedStuckCounted = false;
                        }
                    }

                    if (!car.IsOnTrack) offTrackSteps++;
                    if (car.Physics.Speed > peakSpeed) peakSpeed = car.Physics.Speed;

                    if (progress.CompletedLaps >= 3 && progress.IsFinished)
                    {
                        break;
                    }
                }

                Assert.AreEqual(3, progress.CompletedLaps,
                    $"[{mapKey}] AI skill {skill:F1} did not complete 3 laps within {maxSteps * dt:F0}s (completed={progress.CompletedLaps}, nextCp={progress.NextCheckpointIndex}).");
                Assert.IsTrue(progress.IsFinished,
                    $"[{mapKey}] AI skill {skill:F1} must be marked finished after 3 laps.");

                float totalTime = executedSteps * dt;
                float sumLaps = 0f;
                float bestLap = float.MaxValue;
                for (int i = 0; i < lapTimes.Count; i++)
                {
                    sumLaps += lapTimes[i];
                    if (lapTimes[i] < bestLap) bestLap = lapTimes[i];
                }
                float avgLap = lapTimes.Count > 0 ? sumLaps / lapTimes.Count : 0f;
                if (bestLap == float.MaxValue) bestLap = 0f;

                float totalDistance = track.TotalLength * 3f;
                return new ThreeLapRunResult
                {
                    MapKey = mapKey,
                    MapDisplayName = mapTitle,
                    Skill = skill,
                    Seed = seed,
                    Waypoints = track.WaypointCount,
                    TrackLengthMeters = track.TotalLength,
                    CheckpointsPerLap = checkpointCount,
                    TotalCheckpointsPassed = passedCount,
                    CheckpointsInOrder = checkpointsInOrder,
                    StuckCount = stuckCount,
                    OffTrackSteps = offTrackSteps,
                    TotalSteps = executedSteps,
                    TotalTimeSeconds = totalTime,
                    LapTimes = lapTimes.ToArray(),
                    AverageLapTimeSeconds = avgLap,
                    BestLapTimeSeconds = bestLap,
                    AverageSpeedMps = totalTime > 0f ? totalDistance / totalTime : 0f,
                    PeakSpeedMps = peakSpeed
                };
            }
            finally
            {
                if (aiGo != null) UnityEngine.Object.DestroyImmediate(aiGo);
                if (managerGo != null) UnityEngine.Object.DestroyImmediate(managerGo);
                if (trackGo != null) UnityEngine.Object.DestroyImmediate(trackGo);
                if (trackDef != null) UnityEngine.Object.DestroyImmediate(trackDef);
                if (theme != null) UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        private static void WriteExperimentMarkdown()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string docsDir = Path.Combine(projectRoot, "Docs");
            Directory.CreateDirectory(docsDir);
            string reportPath = Path.Combine(docsDir, "EXPERIMENT.md");

            VehicleStats defaults = VehicleStats.Default;
            var sb = new StringBuilder();
            sb.AppendLine("# Experiment: Benchmark AI 3 vòng theo từng Map & mức Skill");
            sb.AppendLine();
            sb.AppendLine("## Điều kiện thí nghiệm chung");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "- **Thông số xe:** `VehicleStats.Default` (`maxSpeed = {0:F1} m/s`, `enginePower = {1:F0}`, `mass = {2:F0}`).",
                defaults.maxSpeed, defaults.enginePower, defaults.mass));
            sb.AppendLine("- **Số vòng đua mỗi lượt test:** `3` vòng liên tiếp, `dt = 0.0200 s` (`50 Hz`), `seed = 2026`.");
            sb.AppendLine("- **Tiêu chí đạt:** `STUCK = 0`, qua toàn bộ checkpoint đúng thứ tự (`1 -> ... -> N-1 -> 0` lặp 3 vòng), hoàn thành đủ 3 vòng.");
            sb.AppendLine("- **Bài test thực thi:** `MapAIBenchmark3LapTests` (EditMode).");

            string[] orderedKeys = { "Valley", "City" };
            for (int m = 0; m < orderedKeys.Length; m++)
            {
                if (!s_ReportsByMap.TryGetValue(orderedKeys[m], out MapBenchmarkReportData mapReport) ||
                    mapReport.Results == null ||
                    mapReport.Results.Count == 0)
                {
                    continue;
                }

                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "## Map: {0}", mapReport.MapTitle));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "- **Đường đua:** `{0}` waypoints, chiều dài 1 vòng `{1:F2} m`, `{2}` checkpoint/vòng (tổng `{3}` checkpoint / 3 vòng).",
                    mapReport.Waypoints,
                    mapReport.TrackLengthMeters,
                    mapReport.CheckpointsPerLap,
                    mapReport.CheckpointsPerLap * 3));
                sb.AppendLine();
                sb.AppendLine("| Skill | Vòng 1 (s) | Vòng 2 (s) | Vòng 3 (s) | Thời gian 1 vòng TB (s) | Vòng nhanh nhất (s) | Tổng 3 vòng (s) | Tốc độ TB (m/s) | Tốc độ đỉnh (m/s) | STUCK | Checkpoint đúng thứ tự |");
                sb.AppendLine("| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

                for (int i = 0; i < mapReport.Results.Count; i++)
                {
                    ThreeLapRunResult r = mapReport.Results[i];
                    float lap1 = r.LapTimes.Length > 0 ? r.LapTimes[0] : 0f;
                    float lap2 = r.LapTimes.Length > 1 ? r.LapTimes[1] : 0f;
                    float lap3 = r.LapTimes.Length > 2 ? r.LapTimes[2] : 0f;
                    string cpStatus = r.CheckpointsInOrder
                        ? string.Format(CultureInfo.InvariantCulture, "OK ({0}/{1})", r.TotalCheckpointsPassed, r.CheckpointsPerLap * 3)
                        : "FAIL";

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "| {0:F1} | {1:F2} | {2:F2} | {3:F2} | {4:F2} | {5:F2} | {6:F2} | {7:F2} | {8:F2} | {9} | {10} |",
                        r.Skill,
                        lap1,
                        lap2,
                        lap3,
                        r.AverageLapTimeSeconds,
                        r.BestLapTimeSeconds,
                        r.TotalTimeSeconds,
                        r.AverageSpeedMps,
                        r.PeakSpeedMps,
                        r.StuckCount,
                        cpStatus));
                }

                if (mapReport.Results.Count == 3)
                {
                    float diff04To07 = mapReport.Results[0].AverageLapTimeSeconds - mapReport.Results[1].AverageLapTimeSeconds;
                    float diff07To10 = mapReport.Results[1].AverageLapTimeSeconds - mapReport.Results[2].AverageLapTimeSeconds;
                    sb.AppendLine();
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "- **Nhận xét ({0}):** Skill `0.7` nhanh hơn `0.4` trung bình **{1:F2} s/vòng**; Skill `1.0` nhanh hơn `0.7` trung bình **{2:F2} s/vòng**.",
                        mapReport.MapKey,
                        diff04To07,
                        diff07To10));
                }
            }

            File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
