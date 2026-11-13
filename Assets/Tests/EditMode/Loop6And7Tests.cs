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
    }
}
