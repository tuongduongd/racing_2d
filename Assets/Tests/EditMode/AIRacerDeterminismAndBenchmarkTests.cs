using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class AIRacerDeterminismAndBenchmarkTests
    {
        private struct StepSnapshot
        {
            public VehicleInput Input;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Heading;
            public float TrackDistance;
            public int TargetWaypoint;
        }

        private struct LapBenchmarkResult
        {
            public float Skill;
            public int Seed;
            public int Steps;
            public float LapTimeSeconds;
            public float TrackLengthMeters;
            public float AverageSpeedMps;
            public float PeakSpeedMps;
            public int OffTrackSteps;
            public int Checkpoints;
            public int Waypoints;
        }

        [Test]
        public void AIRacerDriver_SameSeed_ProducesIdenticalTrajectoryAndInputs()
        {
            StepSnapshot[] runA = SimulateSteps(
                seed: 2026,
                globalRandomSeed: 111,
                skill: 0.7f,
                steps: 400,
                withBlockerCar: true,
                setSeedBeforeConfigure: false,
                resetAndReplay: false);

            StepSnapshot[] runB = SimulateSteps(
                seed: 2026,
                globalRandomSeed: 999999,
                skill: 0.7f,
                steps: 400,
                withBlockerCar: true,
                setSeedBeforeConfigure: false,
                resetAndReplay: false);

            StepSnapshot[] runAfterReset = SimulateSteps(
                seed: 2026,
                globalRandomSeed: 555,
                skill: 0.7f,
                steps: 400,
                withBlockerCar: true,
                setSeedBeforeConfigure: false,
                resetAndReplay: true);

            AssertSnapshotsExactMatch(runA, runB, "Two independent runs with the same seed must match identically.");
            AssertSnapshotsExactMatch(runA, runAfterReset, "Re-initializing / OnReset() with the same seed must replay identically.");
        }

        [Test]
        public void AIRacerDriver_SetSeedBeforeOrAfterConfigure_ProducesIdenticalResult()
        {
            StepSnapshot[] seedAfter = SimulateSteps(
                seed: 42,
                globalRandomSeed: 17,
                skill: 0.7f,
                steps: 300,
                withBlockerCar: false,
                setSeedBeforeConfigure: false,
                resetAndReplay: false);

            StepSnapshot[] seedBefore = SimulateSteps(
                seed: 42,
                globalRandomSeed: 88,
                skill: 0.7f,
                steps: 300,
                withBlockerCar: false,
                setSeedBeforeConfigure: true,
                resetAndReplay: false);

            AssertSnapshotsExactMatch(seedAfter, seedBefore,
                "Calling SetSeed() before or after Configure() must produce the exact same AI behavior.");
        }

        [Test]
        public void AIRacerDriver_DifferentSeeds_ProduceDistinctSteeringVariationWithoutBreakingDriving()
        {
            StepSnapshot[] seed101 = SimulateSteps(
                seed: 101,
                globalRandomSeed: 1,
                skill: 0.7f,
                steps: 300,
                withBlockerCar: false,
                setSeedBeforeConfigure: false,
                resetAndReplay: false);

            StepSnapshot[] seed777 = SimulateSteps(
                seed: 777,
                globalRandomSeed: 1,
                skill: 0.7f,
                steps: 300,
                withBlockerCar: false,
                setSeedBeforeConfigure: false,
                resetAndReplay: false);

            float maxSteerDiff = 0f;
            for (int i = 0; i < seed101.Length; i++)
            {
                maxSteerDiff = Mathf.Max(maxSteerDiff, Mathf.Abs(seed101[i].Input.steering - seed777[i].Input.steering));
            }

            Assert.Greater(maxSteerDiff, 0.0001f,
                "Different seeds should produce distinct wobble phases for imperfect skill levels.");
            Assert.Greater(seed101[seed101.Length - 1].TrackDistance, 150f);
            Assert.Greater(seed777[seed777.Length - 1].TrackDistance, 150f);
        }

        [Test]
        public void AIRacerDriver_Benchmark_SingleLapTimeAcrossSkills()
        {
            const int benchmarkSeed = 2026;
            const float dt = 1f / 50f;
            float[] skills = { 0.4f, 0.7f, 1.0f };
            var results = new List<LapBenchmarkResult>(skills.Length);

            for (int i = 0; i < skills.Length; i++)
            {
                LapBenchmarkResult result = RunSingleLapBenchmark(skills[i], benchmarkSeed, dt);
                results.Add(result);

                Assert.Greater(result.LapTimeSeconds, 0f,
                    $"Skill {skills[i]:F1} must complete one lap with positive time.");
            }

            // Higher skill must complete a lap strictly faster than lower skill.
            Assert.Less(results[1].LapTimeSeconds, results[0].LapTimeSeconds,
                $"Skill 0.7 ({results[1].LapTimeSeconds:F2}s) should finish faster than skill 0.4 ({results[0].LapTimeSeconds:F2}s).");
            Assert.Less(results[2].LapTimeSeconds, results[1].LapTimeSeconds,
                $"Skill 1.0 ({results[2].LapTimeSeconds:F2}s) should finish faster than skill 0.7 ({results[1].LapTimeSeconds:F2}s).");

            WriteExperimentReport(results, dt, benchmarkSeed);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[AI Lap Benchmark Seed={0}] 0.4 => {1:F2}s | 0.7 => {2:F2}s | 1.0 => {3:F2}s",
                benchmarkSeed,
                results[0].LapTimeSeconds,
                results[1].LapTimeSeconds,
                results[2].LapTimeSeconds));
        }

        private static StepSnapshot[] SimulateSteps(
            int seed,
            int globalRandomSeed,
            float skill,
            int steps,
            bool withBlockerCar,
            bool setSeedBeforeConfigure,
            bool resetAndReplay)
        {
            UnityEngine.Random.InitState(globalRandomSeed);

            var trackGo = new GameObject("DetTrack");
            var aiGo = new GameObject("DetAI");
            GameObject blockerGo = null;

            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var car = aiGo.AddComponent<Car>();
                var ai = aiGo.AddComponent<AIRacerDriver>();

                if (setSeedBeforeConfigure)
                {
                    ai.SetSeed(seed);
                    ai.Configure("DetRacer", skill, 0f);
                }
                else
                {
                    ai.Configure("DetRacer", skill, 0f, seed);
                }

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                var allCars = new List<Car> { car };
                if (withBlockerCar)
                {
                    blockerGo = new GameObject("DetBlocker");
                    var blockerCar = blockerGo.AddComponent<Car>();
                    blockerCar.Initialize(null, startPos + startDir * 45f + new Vector2(0f, -1.2f), startHeading, track);
                    allCars.Add(blockerCar);
                }
                ai.SetAllCars(allCars);

                const float dt = 1f / 50f;
                car.Initialize(ai, startPos, startHeading, track);

                if (resetAndReplay)
                {
                    for (int warmup = 0; warmup < 75; warmup++)
                    {
                        car.StepPhysics(dt, track);
                    }
                    UnityEngine.Random.InitState(globalRandomSeed + 12345);
                    car.Initialize(ai, startPos, startHeading, track);
                }

                var snapshots = new StepSnapshot[steps];
                for (int i = 0; i < steps; i++)
                {
                    car.StepPhysics(dt, track);
                    snapshots[i] = new StepSnapshot
                    {
                        Input = car.LastInput,
                        Position = car.Physics.Position,
                        Velocity = car.Physics.Velocity,
                        Heading = car.Physics.Heading,
                        TrackDistance = car.TrackDistance,
                        TargetWaypoint = ai.TargetWaypointIndex
                    };
                }

                return snapshots;
            }
            finally
            {
                if (blockerGo != null) UnityEngine.Object.DestroyImmediate(blockerGo);
                UnityEngine.Object.DestroyImmediate(aiGo);
                UnityEngine.Object.DestroyImmediate(trackGo);
            }
        }

        private static LapBenchmarkResult RunSingleLapBenchmark(float skill, int seed, float dt)
        {
            var trackGo = new GameObject("BenchTrack");
            var managerGo = new GameObject("BenchLapManager");
            var aiGo = new GameObject($"BenchAI_{skill:F1}");

            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var lapManager = managerGo.AddComponent<LapCheckpointManager>();
                var car = aiGo.AddComponent<Car>();
                var aiDriver = aiGo.AddComponent<AIRacerDriver>();
                aiDriver.Configure($"Benchmark_{skill:F1}", skill, 0f, seed);

                Vector2 startPos = track.GetWaypoint(0);
                Vector2 startDir = track.GetTrackDirection(0);
                float startHeading = Mathf.Atan2(startDir.y, startDir.x);

                car.Initialize(aiDriver, startPos, startHeading, track);
                lapManager.Initialize(track, new[] { car }, 1);
                RacerProgress progress = lapManager.GetProgress(car);

                const int maxSteps = 50 * 120; // 120s safety cap
                int offTrackSteps = 0;
                float peakSpeed = 0f;
                int executedSteps = 0;

                for (int step = 0; step < maxSteps; step++)
                {
                    car.StepPhysics(dt, track);
                    lapManager.Tick(dt);
                    executedSteps = step + 1;

                    if (!car.IsOnTrack) offTrackSteps++;
                    if (car.Physics.Speed > peakSpeed) peakSpeed = car.Physics.Speed;

                    if (progress.CompletedLaps >= 1)
                    {
                        break;
                    }
                }

                Assert.AreEqual(1, progress.CompletedLaps,
                    $"Benchmark AI (skill={skill:F1}) did not finish 1 lap within {maxSteps * dt:F0}s.");

                float lapTime = progress.LastLapTime;
                return new LapBenchmarkResult
                {
                    Skill = skill,
                    Seed = seed,
                    Steps = executedSteps,
                    LapTimeSeconds = lapTime,
                    TrackLengthMeters = track.TotalLength,
                    AverageSpeedMps = lapTime > 0f ? track.TotalLength / lapTime : 0f,
                    PeakSpeedMps = peakSpeed,
                    OffTrackSteps = offTrackSteps,
                    Checkpoints = lapManager.CheckpointCount,
                    Waypoints = track.WaypointCount
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aiGo);
                UnityEngine.Object.DestroyImmediate(managerGo);
                UnityEngine.Object.DestroyImmediate(trackGo);
            }
        }

        private static void AssertSnapshotsExactMatch(StepSnapshot[] expected, StepSnapshot[] actual, string message)
        {
            Assert.AreEqual(expected.Length, actual.Length, message);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Input.throttle, actual[i].Input.throttle, $"{message} (step {i} throttle)");
                Assert.AreEqual(expected[i].Input.brake, actual[i].Input.brake, $"{message} (step {i} brake)");
                Assert.AreEqual(expected[i].Input.steering, actual[i].Input.steering, $"{message} (step {i} steering)");
                Assert.AreEqual(expected[i].Position.x, actual[i].Position.x, $"{message} (step {i} pos.x)");
                Assert.AreEqual(expected[i].Position.y, actual[i].Position.y, $"{message} (step {i} pos.y)");
                Assert.AreEqual(expected[i].Velocity.x, actual[i].Velocity.x, $"{message} (step {i} vel.x)");
                Assert.AreEqual(expected[i].Velocity.y, actual[i].Velocity.y, $"{message} (step {i} vel.y)");
                Assert.AreEqual(expected[i].Heading, actual[i].Heading, $"{message} (step {i} heading)");
                Assert.AreEqual(expected[i].TrackDistance, actual[i].TrackDistance, $"{message} (step {i} trackDistance)");
                Assert.AreEqual(expected[i].TargetWaypoint, actual[i].TargetWaypoint, $"{message} (step {i} targetWaypoint)");
            }
        }

        private static void WriteExperimentReport(IReadOnlyList<LapBenchmarkResult> results, float dt, int seed)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string docsDir = Path.Combine(projectRoot, "Docs");
            Directory.CreateDirectory(docsDir);

            LapBenchmarkResult first = results[0];
            VehicleStats defaults = VehicleStats.Default;

            var sb = new StringBuilder();
            sb.AppendLine("# Experiment: Benchmark thời gian 1 vòng của AIRacerDriver theo mức Skill");
            sb.AppendLine();
            sb.AppendLine("## Điều kiện thí nghiệm");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "- **Đường đua:** `WaypointTrack.BuildDefaultCircuit()` ({0} waypoints, chiều dài `{1:F2} m`, `{2}` checkpoint).",
                first.Waypoints, first.TrackLengthMeters, first.Checkpoints));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "- **Thông số xe:** `VehicleStats.Default` (`maxSpeed = {0:F1} m/s`, `enginePower = {1:F0}`, `mass = {2:F0}`).",
                defaults.maxSpeed, defaults.enginePower, defaults.mass));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "- **Bước mô phỏng:** `dt = {0:F4} s` (`{1:F0} Hz`), cấu hình `AIRacerDriver.Configure(..., seed: {2})`.",
                dt, 1f / dt, seed));
            sb.AppendLine("- **Bài test thực thi:** `AIRacerDeterminismAndBenchmarkTests.AIRacerDriver_Benchmark_SingleLapTimeAcrossSkills` (EditMode).");
            sb.AppendLine();
            sb.AppendLine("## Kết quả đo 1 vòng");
            sb.AppendLine();
            sb.AppendLine("| Skill | Seed | Thời gian 1 vòng (s) | Số bước (50 Hz) | Tốc độ TB (m/s) | Tốc độ TB (km/h) | Tốc độ đỉnh (m/s) | Số bước ngoài đường |");
            sb.AppendLine("| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

            for (int i = 0; i < results.Count; i++)
            {
                LapBenchmarkResult r = results[i];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0:F1} | {1} | {2:F2} | {3} | {4:F2} | {5:F2} | {6:F2} | {7} |",
                    r.Skill,
                    r.Seed,
                    r.LapTimeSeconds,
                    r.Steps,
                    r.AverageSpeedMps,
                    r.AverageSpeedMps * 3.6f,
                    r.PeakSpeedMps,
                    r.OffTrackSteps));
            }

            if (results.Count == 3)
            {
                float diffSlowMid = results[0].LapTimeSeconds - results[1].LapTimeSeconds;
                float diffMidFast = results[1].LapTimeSeconds - results[2].LapTimeSeconds;
                sb.AppendLine();
                sb.AppendLine("## Nhận xét");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "- AI skill `0.7` nhanh hơn skill `0.4` là **{0:F2} s** (`{1:F1}%`).",
                    diffSlowMid, (diffSlowMid / results[0].LapTimeSeconds) * 100f));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "- AI skill `1.0` nhanh hơn skill `0.7` là **{0:F2} s** (`{1:F1}%`).",
                    diffMidFast, (diffMidFast / results[1].LapTimeSeconds) * 100f));
                sb.AppendLine("- Toàn bộ mô phỏng sử dụng seed cố định và đồng hồ mô phỏng nội bộ (`_simulationTime`), đảm bảo chạy lại cho kết quả giống hệt nhau.");
            }

            string reportPath = Path.Combine(docsDir, "EXPERIMENT.md");
            File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
