using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class Loop6And7Tests
    {
        private string _tempSavePath;

        [SetUp]
        public void SetUp()
        {
            _tempSavePath = Path.Combine(Path.GetTempPath(), $"racing2d_test_{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempSavePath) && File.Exists(_tempSavePath))
            {
                File.Delete(_tempSavePath);
            }
        }

        [Test]
        public void UpgradeSystem_BuyUpgradeDeductsMoneyIncreasesCostAndRejectsInvalidPurchase()
        {
            var upg = new CarUpgradeSystem(_tempSavePath);
            Assert.AreEqual(200, upg.Money);
            Assert.AreEqual(0, upg.GetLevel(UpgradeType.Engine));

            int lv0Cost = upg.GetUpgradeCost(UpgradeType.Engine); // 150
            Assert.AreEqual(150, lv0Cost);
            Assert.IsTrue(upg.BuyUpgrade(UpgradeType.Engine));
            Assert.AreEqual(50, upg.Money);
            Assert.AreEqual(1, upg.GetLevel(UpgradeType.Engine));

            int lv1Cost = upg.GetUpgradeCost(UpgradeType.Engine); // round(150 * 1.5) = 225
            Assert.AreEqual(225, lv1Cost);

            // Insufficient funds -> fails cleanly without mutating state
            Assert.IsFalse(upg.BuyUpgrade(UpgradeType.Engine));
            Assert.AreEqual(50, upg.Money);
            Assert.AreEqual(1, upg.GetLevel(UpgradeType.Engine));

            // Max level cap enforcement
            upg.AddMoney(10000);
            while (upg.GetLevel(UpgradeType.Engine) < CarUpgradeSystem.MaxUpgradeLevel)
            {
                Assert.IsTrue(upg.BuyUpgrade(UpgradeType.Engine));
            }
            Assert.IsTrue(upg.IsMaxLevel(UpgradeType.Engine));
            Assert.AreEqual(0, upg.GetUpgradeCost(UpgradeType.Engine));
            Assert.IsFalse(upg.BuyUpgrade(UpgradeType.Engine));
        }

        [Test]
        public void UpgradeSystem_ApplyToCarMakesVehicleMeasurablyFasterAndIsIdempotent()
        {
            var upg = new CarUpgradeSystem(_tempSavePath);
            upg.AddMoney(5000);
            upg.BuyUpgrade(UpgradeType.Engine);
            upg.BuyUpgrade(UpgradeType.Engine);
            upg.BuyUpgrade(UpgradeType.WeightReduction);
            upg.BuyUpgrade(UpgradeType.Grip);

            var stockPhysics = new VehiclePhysics();
            stockPhysics.Reset(Vector2.zero, 0f);

            var upgradedPhysics = new VehiclePhysics();
            upgradedPhysics.Reset(Vector2.zero, 0f);
            upg.ApplyToCar(upgradedPhysics);
            float expectedEngine = upgradedPhysics.EnginePower;
            // Applying twice should produce exact same stats (no double-stacking)
            upg.ApplyToCar(upgradedPhysics);
            Assert.AreEqual(expectedEngine, upgradedPhysics.EnginePower, 0.001f);

            stockPhysics.ApplyThrottle(1f);
            upgradedPhysics.ApplyThrottle(1f);
            for (int i = 0; i < 180; i++)
            {
                stockPhysics.Update(1f / 60f);
                upgradedPhysics.Update(1f / 60f);
            }

            Assert.Greater(upgradedPhysics.ForwardSpeed, stockPhysics.ForwardSpeed,
                "Upgraded car should achieve higher speed than stock car.");
            Assert.Greater(upgradedPhysics.Position.x, stockPhysics.Position.x,
                "Upgraded car should travel farther in 3 seconds.");
        }

        [Test]
        public void UpgradeSystem_SaveAndLoadJsonPersistsMoneyUpgradesAndRecords()
        {
            var writer = new CarUpgradeSystem(_tempSavePath);
            int prize = writer.AwardRaceResult(1, 52.34f); // +500 -> 700 total
            Assert.AreEqual(500, prize);
            Assert.IsTrue(writer.BuyUpgrade(UpgradeType.Brakes)); // -120 -> 580 left

            Assert.IsTrue(File.Exists(_tempSavePath), "Save JSON file should be created on disk.");

            var reader = new CarUpgradeSystem(_tempSavePath);
            Assert.IsTrue(reader.Load());
            Assert.AreEqual(580, reader.Money);
            Assert.AreEqual(1, reader.GetLevel(UpgradeType.Brakes));
            Assert.AreEqual(1, reader.RacesPlayed);
            Assert.AreEqual(1, reader.RacesWon);
            Assert.AreEqual(52.34f, reader.BestRaceTime, 0.01f);
        }

        [Test]
        public void GameStates_PolymorphicStateTypesMatchExpectedFlow()
        {
            RaceStateBase[] states =
            {
                new MainMenuState(),
                new CountdownState(),
                new RacingState(),
                new FinishedState(),
                new UpgradeShopState()
            };

            Assert.AreEqual(GameFlowState.MainMenu, states[0].StateType);
            Assert.AreEqual(GameFlowState.Countdown, states[1].StateType);
            Assert.AreEqual(GameFlowState.Racing, states[2].StateType);
            Assert.AreEqual(GameFlowState.Finished, states[3].StateType);
            Assert.AreEqual(GameFlowState.UpgradeShop, states[4].StateType);
        }

        [Test]
        public void UpgradeSystem_UsesSharedVehicleStatsAndPreservesNonUpgradedFields()
        {
            VehicleStats customBase = VehicleStats.Default;
            customBase.mass = 200f;
            customBase.enginePower = 5800f;
            customBase.drag = 0.62f;
            customBase.rollingResistance = 1.15f;
            customBase.maxReverseSpeed = 10f;
            customBase.turnRate = 3.1f;

            var statsAsset = ScriptableObject.CreateInstance<VehicleStatsAsset>();
            try
            {
                statsAsset.SetStats(customBase);
                var upg = new CarUpgradeSystem(_tempSavePath, statsAsset.Stats);
                upg.AddMoney(5000);
                Assert.IsTrue(upg.BuyUpgrade(UpgradeType.Engine));
                Assert.IsTrue(upg.BuyUpgrade(UpgradeType.WeightReduction));

                var phys = new VehiclePhysics();
                upg.ApplyToCar(phys);

                Assert.AreEqual(186f, phys.Mass, 0.001f);
                Assert.AreEqual(6550f, phys.EnginePower, 0.001f);
                Assert.AreEqual(customBase.maxSpeed + 3.5f, phys.MaxSpeed, 0.001f);
                Assert.AreEqual(customBase.drag, phys.Drag, 0.001f);
                Assert.AreEqual(customBase.rollingResistance, phys.RollingResistance, 0.001f);
                Assert.AreEqual(customBase.maxReverseSpeed, phys.MaxReverseSpeed, 0.001f);
                Assert.AreEqual(customBase.turnRate, phys.TurnRate, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(statsAsset);
            }
        }

        [Test]
        public void UpgradeSystem_AllUpgradeTypesEnforceMaxLevelCapWithoutOvercharging()
        {
            var upg = new CarUpgradeSystem(_tempSavePath);
            upg.AddMoney(100000);

            UpgradeType[] allTypes =
            {
                UpgradeType.Engine,
                UpgradeType.Brakes,
                UpgradeType.Grip,
                UpgradeType.WeightReduction
            };

            foreach (UpgradeType type in allTypes)
            {
                Assert.AreEqual(0, upg.GetLevel(type), $"{type} should start at level 0.");
                Assert.IsFalse(upg.IsMaxLevel(type), $"{type} should not be max level at 0.");

                for (int lv = 0; lv < CarUpgradeSystem.MaxUpgradeLevel; lv++)
                {
                    Assert.IsTrue(upg.CanAfford(type), $"{type} level {lv} should be affordable with ample funds.");
                    Assert.IsTrue(upg.BuyUpgrade(type), $"{type} upgrade from {lv} to {lv + 1} should succeed.");
                }

                Assert.AreEqual(CarUpgradeSystem.MaxUpgradeLevel, upg.GetLevel(type),
                    $"{type} should reach MaxUpgradeLevel ({CarUpgradeSystem.MaxUpgradeLevel}).");
                Assert.IsTrue(upg.IsMaxLevel(type), $"{type} IsMaxLevel should be true at level 5.");
                Assert.AreEqual(0, upg.GetUpgradeCost(type), $"{type} GetUpgradeCost should return 0 at max level.");
                Assert.IsFalse(upg.CanAfford(type), $"{type} CanAfford should return false at max level.");

                int moneyBeforeExtraBuy = upg.Money;
                Assert.IsFalse(upg.BuyUpgrade(type), $"{type} BuyUpgrade beyond max level must return false.");
                Assert.AreEqual(moneyBeforeExtraBuy, upg.Money, $"{type} failed BuyUpgrade must not deduct money.");
                Assert.AreEqual(CarUpgradeSystem.MaxUpgradeLevel, upg.GetLevel(type),
                    $"{type} level must not exceed MaxUpgradeLevel.");
            }
        }

        [Test]
        public void UpgradeSystem_LoadCorruptedJsonClampsOutOfRangeLevelsToValidBounds()
        {
            // First case: Engine & Grip > MaxUpgradeLevel, Brakes & WeightReduction < 0
            string corruptedJsonA = @"{
                ""money"": -250,
                ""engineLevel"": 99,
                ""brakesLevel"": -3,
                ""gripLevel"": 6,
                ""weightLevel"": -10,
                ""racesPlayed"": -4,
                ""racesWon"": -1,
                ""bestRaceTime"": -12.5
            }";
            File.WriteAllText(_tempSavePath, corruptedJsonA);

            var upgA = new CarUpgradeSystem(_tempSavePath);
            Assert.IsTrue(upgA.Load());
            Assert.AreEqual(0, upgA.Money);
            Assert.AreEqual(CarUpgradeSystem.MaxUpgradeLevel, upgA.GetLevel(UpgradeType.Engine));
            Assert.AreEqual(0, upgA.GetLevel(UpgradeType.Brakes));
            Assert.AreEqual(CarUpgradeSystem.MaxUpgradeLevel, upgA.GetLevel(UpgradeType.Grip));
            Assert.AreEqual(0, upgA.GetLevel(UpgradeType.WeightReduction));
            Assert.IsTrue(upgA.IsMaxLevel(UpgradeType.Engine));
            Assert.AreEqual(0, upgA.GetUpgradeCost(UpgradeType.Engine));
            Assert.IsFalse(upgA.CanAfford(UpgradeType.Engine));
            Assert.AreEqual(0, upgA.RacesPlayed);
            Assert.AreEqual(0, upgA.RacesWon);
            Assert.AreEqual(0f, upgA.BestRaceTime, 0.0001f);

            // Second case: swap directions so every UpgradeType is tested for both < 0 and > MaxUpgradeLevel
            string corruptedJsonB = @"{
                ""money"": 500,
                ""engineLevel"": -5,
                ""brakesLevel"": 12,
                ""gripLevel"": -1,
                ""weightLevel"": 42,
                ""racesPlayed"": 3,
                ""racesWon"": 1,
                ""bestRaceTime"": 45.2
            }";
            File.WriteAllText(_tempSavePath, corruptedJsonB);

            var upgB = new CarUpgradeSystem(_tempSavePath);
            Assert.IsTrue(upgB.Load());
            Assert.AreEqual(500, upgB.Money);
            Assert.AreEqual(0, upgB.GetLevel(UpgradeType.Engine));
            Assert.AreEqual(CarUpgradeSystem.MaxUpgradeLevel, upgB.GetLevel(UpgradeType.Brakes));
            Assert.AreEqual(0, upgB.GetLevel(UpgradeType.Grip));
            Assert.AreEqual(CarUpgradeSystem.MaxUpgradeLevel, upgB.GetLevel(UpgradeType.WeightReduction));
            Assert.IsTrue(upgB.IsMaxLevel(UpgradeType.Brakes));
            Assert.IsTrue(upgB.IsMaxLevel(UpgradeType.WeightReduction));
            Assert.AreEqual(0, upgB.GetUpgradeCost(UpgradeType.Brakes));
            Assert.IsFalse(upgB.CanAfford(UpgradeType.WeightReduction));
        }
    }
}
