using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class PureCoreArchitectureAndDipTests
    {
        private sealed class InMemorySaveStorage : ISaveStorage
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public bool Exists(string path) => !string.IsNullOrEmpty(path) && _files.ContainsKey(path);

            public string ReadAllText(string path)
            {
                if (!_files.TryGetValue(path, out string content))
                {
                    throw new FileNotFoundException("Not found in InMemorySaveStorage", path);
                }
                return content;
            }

            public void WriteAllText(string path, string content)
            {
                _files[path] = content;
            }

            public void Delete(string path)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    _files.Remove(path);
                }
            }
        }

        private sealed class FakeInputProvider : IInputProvider
        {
            public VehicleInput NextVehicleInput;
            public bool PauseTrigger;
            public bool RestartTrigger;
            public bool RecoverTrigger;
            public bool MuteTrigger;

            public VehicleInput ReadInput() => NextVehicleInput;

            public bool WasPausePressed()
            {
                bool v = PauseTrigger;
                PauseTrigger = false;
                return v;
            }

            public bool WasRestartPressed()
            {
                bool v = RestartTrigger;
                RestartTrigger = false;
                return v;
            }

            public bool WasRecoverPressed()
            {
                bool v = RecoverTrigger;
                RecoverTrigger = false;
                return v;
            }

            public bool WasMutePressed()
            {
                bool v = MuteTrigger;
                MuteTrigger = false;
                return v;
            }

            public void ResetState()
            {
                NextVehicleInput = default;
                PauseTrigger = false;
                RestartTrigger = false;
                RecoverTrigger = false;
                MuteTrigger = false;
            }
        }

        private sealed class FakeTrackDataProvider : ITrackDataProvider
        {
            private readonly List<TrackJsonDto> _tracks = new List<TrackJsonDto>();

            public void AddTrack(TrackJsonDto dto) => _tracks.Add(dto);
            public IReadOnlyList<TrackJsonDto> LoadTracks(string directoryPath) => _tracks;
            public TrackJsonDto LoadTrackFile(string filePath) => _tracks.Count > 0 ? _tracks[0] : null;
        }

        // Open-Closed Principle demonstration: new Decorator added without changing existing classes
        private sealed class CustomNitroDecorator : VehicleStatsDecorator
        {
            private readonly float _bonusSpeed;

            public CustomNitroDecorator(IVehicleStatsProvider inner, float bonusSpeed) : base(inner)
            {
                _bonusSpeed = bonusSpeed;
            }

            protected override PureVehicleStats ModifyStats(PureVehicleStats current)
            {
                current.maxSpeed += _bonusSpeed;
                return current;
            }
        }

        // Open-Closed Principle demonstration: new Obstacle Blueprint registered into TrackObstacleFactory
        private sealed class CustomConeHeavyBlueprint : IObstacleBlueprint
        {
            public ObstacleKind Kind => ObstacleKind.TrafficCone;

            public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
            {
                return new ObstacleSpec(Kind, defaultHitRadius * 1.5f, 0.50f, defaultLateralImpulse * 1.4f);
            }
        }

        [Test]
        public void PureCore_NoEngineReferencesAudit_ZeroFilesUseUnityEngine()
        {
            string asmdefPath = Path.GetFullPath("Assets/Scripts/Core/Pure/RacingGame.Core.asmdef");
            Assert.IsTrue(File.Exists(asmdefPath), "RacingGame.Core.asmdef must exist.");
            string asmdefText = File.ReadAllText(asmdefPath);
            StringAssert.Contains("\"noEngineReferences\": true", asmdefText);

            string pureDir = Path.GetFullPath("Assets/Scripts/Core/Pure");
            string[] csFiles = Directory.GetFiles(pureDir, "*.cs", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(csFiles.Length, 18, "Pure Core assembly should hold domain entities, contracts, exceptions, and rules.");

            for (int i = 0; i < csFiles.Length; i++)
            {
                string content = File.ReadAllText(csFiles[i]);
                Assert.IsFalse(
                    content.Contains("using UnityEngine"),
                    $"Pure Core file '{csFiles[i]}' must not contain 'using UnityEngine'.");
            }
        }

        [Test]
        public void ProgressionRuleEngine_WithInMemoryStorage_SavesLoadsAndTracksTopHighScores()
        {
            var storage = new InMemorySaveStorage();
            var codec = new UnityJsonCodec();
            const string memPath = "memory://save.json";

            var engine = new ProgressionRuleEngine(storage, codec, memPath, PureVehicleStats.Default);
            Assert.AreEqual(200, engine.Money);

            engine.AwardRaceResult(1, 48.25f, 1.3f); // 200 + round(500 * 1.3) = 850
            Assert.AreEqual(850, engine.Money);

            engine.RecordHighScore("coastal_highway", "Player", 48.25f, 3, DifficultyLevel.Normal);
            engine.RecordHighScore("coastal_highway", "Player", 43.10f, 3, DifficultyLevel.Hard);
            engine.RecordHighScore("city_morning", "Player", 39.50f, 3, DifficultyLevel.Easy);

            var reloaded = new ProgressionRuleEngine(storage, codec, memPath, PureVehicleStats.Default);
            Assert.IsTrue(reloaded.LoadStrict());
            Assert.AreEqual(850, reloaded.Money);

            IReadOnlyList<HighScoreEntry> coastalScores = reloaded.GetHighScores("coastal_highway");
            Assert.AreEqual(2, coastalScores.Count);
            Assert.AreEqual(43.10f, coastalScores[0].finishTime, 0.001f, "High scores must be sorted fastest-first.");
            Assert.AreEqual(48.25f, coastalScores[1].finishTime, 0.001f);
        }

        [Test]
        public void ProgressionRuleEngine_LoadStrict_ThrowsSaveDataCorruptedExceptionOnCorruptJson()
        {
            var storage = new InMemorySaveStorage();
            var codec = new UnityJsonCodec();
            const string memPath = "memory://corrupt_save.json";

            storage.WriteAllText(memPath, "{{CORRUPT_NOT_JSON!!!");
            var engine = new ProgressionRuleEngine(storage, codec, memPath, PureVehicleStats.Default);

            SaveDataCorruptedException thrown = Assert.Throws<SaveDataCorruptedException>(() => engine.LoadStrict());
            Assert.AreEqual("SAVE_CORRUPTED", thrown.ErrorCode);
            Assert.IsTrue(thrown is GameDomainException);

            // Non-strict Load() catches SaveDataCorruptedException cleanly and recovers default state
            Assert.IsFalse(engine.Load());
            Assert.AreEqual(200, engine.Money);
        }

        [Test]
        public void ProgressionRuleEngine_BuyUpgradeStrict_ThrowsInvalidUpgradeTransactionExceptionOnErrors()
        {
            var storage = new InMemorySaveStorage();
            var codec = new UnityJsonCodec();
            var engine = new ProgressionRuleEngine(storage, codec, "memory://tx.json", PureVehicleStats.Default);

            Assert.IsTrue(engine.BuyUpgrade(UpgradeType.Engine)); // 200 -> 50 remaining; next costs 225

            InvalidUpgradeTransactionException fundsEx =
                Assert.Throws<InvalidUpgradeTransactionException>(() => engine.BuyUpgradeStrict(UpgradeType.Engine));
            Assert.AreEqual("INVALID_UPGRADE_TX", fundsEx.ErrorCode);
            Assert.AreEqual(UpgradeType.Engine, fundsEx.Upgrade);

            engine.AddMoney(50000);
            while (!engine.IsMaxLevel(UpgradeType.Engine))
            {
                engine.BuyUpgradeStrict(UpgradeType.Engine);
            }

            Assert.Throws<InvalidUpgradeTransactionException>(() => engine.BuyUpgradeStrict(UpgradeType.Engine));
        }

        [Test]
        public void VehicleStatsDecorators_StackUpgradesPoliceAndCustomOcpDecorator()
        {
            PureVehicleStats baseline = PureVehicleStats.Default;
            IVehicleStatsProvider chain = new BaseVehicleStatsProvider(baseline);
            chain = new EngineUpgradeDecorator(chain, level: 2);
            chain = new BrakesUpgradeDecorator(chain, level: 1);
            chain = new GripUpgradeDecorator(chain, level: 1);
            chain = new WeightReductionDecorator(chain, level: 1);
            chain = new PoliceInterceptorDecorator(chain, speedBonus: 4.5f);
            chain = new CustomNitroDecorator(chain, bonusSpeed: 5.0f);

            PureVehicleStats result = chain.GetStats();
            Assert.AreEqual(baseline.mass - 14f, result.mass, 0.001f);
            Assert.AreEqual(baseline.enginePower + 1500f + 450f, result.enginePower, 0.001f);
            Assert.AreEqual(baseline.brakePower + 1400f, result.brakePower, 0.001f);
            Assert.AreEqual(baseline.grip + 1.3f + 0.8f, result.grip, 0.001f);
            Assert.AreEqual(baseline.maxSpeed + 7.0f + 4.5f + 5.0f, result.maxSpeed, 0.001f);
        }

        [Test]
        public void TrackObstacleFactory_CreatesDefaultSpecs_AndSupportsOcpBlueprintRegistration()
        {
            var factory = new TrackObstacleFactory();
            Assert.IsTrue(factory.HasBlueprint(ObstacleKind.TrafficCone));
            Assert.IsTrue(factory.HasBlueprint(ObstacleKind.OilBarrel));
            Assert.IsTrue(factory.HasBlueprint(ObstacleKind.RoadBarrier));
            Assert.IsTrue(factory.HasBlueprint(ObstacleKind.OilSlick));
            Assert.IsTrue(factory.HasBlueprint(ObstacleKind.ParkedBus));

            ObstacleSpec busSpec = factory.CreateSpec(ObstacleKind.ParkedBus, 1.65f, 0.62f, 8.5f);
            Assert.IsTrue(busSpec.OuterLaneOnly);
            Assert.IsTrue(busSpec.AvoidTunnelAndOverpass);

            // Live OCP extension: register new blueprint without modifying TrackObstacleFactory
            factory.Register(new CustomConeHeavyBlueprint());
            ObstacleSpec customCone = factory.CreateSpec(ObstacleKind.TrafficCone, 2.0f, 0.62f, 10f);
            Assert.AreEqual(3.0f, customCone.HitRadius, 0.001f);
            Assert.AreEqual(0.50f, customCone.SpeedMultiplierOnHit, 0.001f);
            Assert.AreEqual(14.0f, customCone.LateralKnockImpulse, 0.001f);
        }

        [Test]
        public void RaceStateTransitionRules_GuardsEnforceValidTransitionsAndPausePermissions()
        {
            Assert.IsFalse(RaceStateTransitionRules.CanPause(GameFlowState.MainMenu));
            Assert.IsFalse(RaceStateTransitionRules.CanPause(GameFlowState.UpgradeShop));
            Assert.IsFalse(RaceStateTransitionRules.CanPause(GameFlowState.Finished));
            Assert.IsTrue(RaceStateTransitionRules.CanPause(GameFlowState.Countdown));
            Assert.IsTrue(RaceStateTransitionRules.CanPause(GameFlowState.Racing));

            Assert.DoesNotThrow(() => RaceStateTransitionRules.ValidateTransitionOrThrow(GameFlowState.MainMenu, GameFlowState.Countdown));
            Assert.DoesNotThrow(() => RaceStateTransitionRules.ValidateTransitionOrThrow(GameFlowState.Racing, GameFlowState.Finished));

            InvalidRaceStateTransitionException ex = Assert.Throws<InvalidRaceStateTransitionException>(() =>
                RaceStateTransitionRules.ValidateTransitionOrThrow(GameFlowState.MainMenu, GameFlowState.Finished));
            Assert.AreEqual("INVALID_STATE_TRANSITION", ex.ErrorCode);
            Assert.AreEqual(GameFlowState.MainMenu, ex.FromState);
            Assert.AreEqual(GameFlowState.Finished, ex.TargetState);
        }

        [Test]
        public void CheckpointGateMath_PureHeadlessGeometry_DetectsForwardOnlyWithinGate()
        {
            Assert.IsTrue(CheckpointGateMath.IsSegmentCrossingGateForward(-1f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 5f));
            Assert.IsFalse(CheckpointGateMath.IsSegmentCrossingGateForward(1f, 0f, -1f, 0f, 0f, 0f, 1f, 0f, 5f));
            Assert.IsFalse(CheckpointGateMath.IsSegmentCrossingGateForward(-1f, 12f, 1f, 12f, 0f, 0f, 1f, 0f, 5f));
        }

        [Test]
        public void CollisionSolverMath_PureHeadlessNewtonian_ConservesMomentum()
        {
            PureCollisionResult res = CollisionSolverMath.ResolveCircleImpact(
                0f, 0f, 40f, 4f, 220f, 1.3f,
                1.4f, 0.2f, 18f, -6f, 200f, 1.3f);

            Assert.IsTrue(res.Collided);
            float pxBefore = 220f * 40f + 200f * 18f;
            float pyBefore = 220f * 4f + 200f * -6f;
            float pxAfter = 220f * (40f + res.VelocityDeltaAX) + 200f * (18f + res.VelocityDeltaBX);
            float pyAfter = 220f * (4f + res.VelocityDeltaAY) + 200f * (-6f + res.VelocityDeltaBY);

            Assert.AreEqual(pxBefore, pxAfter, 0.01f);
            Assert.AreEqual(pyBefore, pyAfter, 0.01f);
        }

        [Test]
        public void TrackJsonDto_And_FakeTrackDataProvider_ValidateDomainInvariants()
        {
            var badDto = new TrackJsonDto
            {
                mapId = "broken_track",
                laneCount = 9, // invalid > 6
                laneWidth = 5.0f,
                waypoints = new[] { new TrackPointDto { x = 0f, y = 0f }, new TrackPointDto { x = 10f, y = 0f } }
            };

            InvalidTrackConfigurationException ex =
                Assert.Throws<InvalidTrackConfigurationException>(() => badDto.ValidateOrThrow());
            Assert.AreEqual("INVALID_TRACK_CONFIG", ex.ErrorCode);

            var fakeRepo = new FakeTrackDataProvider();
            var validDto = new TrackJsonDto
            {
                mapId = "stub_circuit",
                displayName = "Stub Circuit",
                laneCount = 4,
                laneWidth = 5.5f,
                waypoints = new[]
                {
                    new TrackPointDto { x = 0f, y = 0f },
                    new TrackPointDto { x = 50f, y = 0f },
                    new TrackPointDto { x = 50f, y = 50f },
                    new TrackPointDto { x = 0f, y = 50f }
                }
            };
            fakeRepo.AddTrack(validDto);
            Assert.AreEqual(1, fakeRepo.LoadTracks("unused").Count);
            Assert.DoesNotThrow(() => fakeRepo.LoadTrackFile("unused").ValidateOrThrow());
        }

        [Test]
        public void GameBalanceConfig_ValidateOrThrow_RejectsInvalidBalance()
        {
            GameBalanceConfig bad = GameBalanceConfig.CreateDefault();
            bad.upgradeCostGrowthFactor = 0.8f;
            Assert.Throws<InvalidTrackConfigurationException>(() => bad.ValidateOrThrow());
        }

        [Test]
        public void PlayerDriver_And_GameManager_OperateViaFakeInputProviderWithoutKeyboard()
        {
            var host = new GameObject("FakeInputHost");
            try
            {
                var gm = host.AddComponent<GameManager>();
                var playerDriver = host.AddComponent<PlayerDriver>();
                var fakeInput = new FakeInputProvider
                {
                    NextVehicleInput = new VehicleInput(0.85f, 0f, -0.4f, true),
                    PauseTrigger = true
                };

                playerDriver.SetInputProvider(fakeInput);
                VehicleInput read = playerDriver.DecideInput(null, null);
                Assert.AreEqual(0.85f, read.throttle, 0.001f);
                Assert.AreEqual(-0.4f, read.steering, 0.001f);
                Assert.IsTrue(read.kick);

                gm.ConfigureServices(inputProvider: fakeInput);
                gm.TransitionTo(GameFlowState.Racing);
                Assert.IsFalse(gm.IsPaused);

                // Invoke RacingState.Update through GameManager's injected FakeInputProvider
                var racingState = new RacingState();
                racingState.Update(gm, 0.02f);
                Assert.IsTrue(gm.IsPaused, "Pause must trigger through injected IInputProvider without Keyboard.current.");
            }
            finally
            {
                Time.timeScale = 1f;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
