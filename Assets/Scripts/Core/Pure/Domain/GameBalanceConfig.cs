using System;

namespace RacingGame
{
    /// <summary>
    /// Externalized gameplay balancing configuration loaded from StreamingAssets/Config/game_balance.json.
    /// Allows tuning economy, vehicle upgrades, and difficulty curves without recompiling.
    /// </summary>
    [Serializable]
    public class GameBalanceConfig
    {
        public PureVehicleStats defaultVehicleStats = PureVehicleStats.Default;

        public int maxUpgradeLevel = 5;
        public float upgradeCostGrowthFactor = 1.5f;
        public int engineBaseCost = 150;
        public int brakesBaseCost = 120;
        public int gripBaseCost = 130;
        public int weightBaseCost = 140;

        public float enginePowerPerLevel = 750f;
        public float maxSpeedPerLevel = 3.5f;
        public float brakePowerPerLevel = 1400f;
        public float gripPerLevel = 1.3f;
        public float weightReductionPerLevel = 14f;

        public int[] finishPositionPrizes = { 500, 320, 200, 120, 75 };

        public float policeEngineBonus = 450f;
        public float policeGripBonus = 0.8f;

        public static GameBalanceConfig CreateDefault()
        {
            return new GameBalanceConfig();
        }

        public GameBalanceConfig Sanitize()
        {
            defaultVehicleStats = defaultVehicleStats.Sanitize();
            maxUpgradeLevel = CoreMath.Clamp(maxUpgradeLevel, 1, 10);
            upgradeCostGrowthFactor = CoreMath.Clamp(upgradeCostGrowthFactor, 1.05f, 3.0f);
            engineBaseCost = CoreMath.Max(10, engineBaseCost);
            brakesBaseCost = CoreMath.Max(10, brakesBaseCost);
            gripBaseCost = CoreMath.Max(10, gripBaseCost);
            weightBaseCost = CoreMath.Max(10, weightBaseCost);

            enginePowerPerLevel = CoreMath.Max(50f, enginePowerPerLevel);
            maxSpeedPerLevel = CoreMath.Max(0.5f, maxSpeedPerLevel);
            brakePowerPerLevel = CoreMath.Max(50f, brakePowerPerLevel);
            gripPerLevel = CoreMath.Max(0.1f, gripPerLevel);
            weightReductionPerLevel = CoreMath.Max(1f, weightReductionPerLevel);

            if (finishPositionPrizes == null || finishPositionPrizes.Length == 0)
            {
                finishPositionPrizes = new[] { 500, 320, 200, 120, 75 };
            }
            for (int i = 0; i < finishPositionPrizes.Length; i++)
            {
                finishPositionPrizes[i] = CoreMath.Max(0, finishPositionPrizes[i]);
            }

            policeEngineBonus = CoreMath.Max(0f, policeEngineBonus);
            policeGripBonus = CoreMath.Max(0f, policeGripBonus);
            return this;
        }

        public void ValidateOrThrow(string sourceIdentifier = "game_balance.json")
        {
            if (maxUpgradeLevel < 1 || maxUpgradeLevel > 10)
            {
                throw new InvalidTrackConfigurationException(sourceIdentifier, $"maxUpgradeLevel '{maxUpgradeLevel}' must be within [1, 10].");
            }
            if (upgradeCostGrowthFactor <= 1.0f)
            {
                throw new InvalidTrackConfigurationException(sourceIdentifier, $"upgradeCostGrowthFactor '{upgradeCostGrowthFactor}' must be > 1.0.");
            }
            if (finishPositionPrizes == null || finishPositionPrizes.Length < 4)
            {
                throw new InvalidTrackConfigurationException(sourceIdentifier, "finishPositionPrizes must define at least positions 1..4.");
            }
            if (defaultVehicleStats.maxSpeed < 10f || defaultVehicleStats.mass < 50f)
            {
                throw new InvalidTrackConfigurationException(sourceIdentifier, "defaultVehicleStats contains non-physical values.");
            }
        }

        public int GetBaseCost(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Engine => engineBaseCost,
                UpgradeType.Brakes => brakesBaseCost,
                UpgradeType.Grip => gripBaseCost,
                UpgradeType.WeightReduction => weightBaseCost,
                _ => engineBaseCost
            };
        }

        public int GetPrizeForPosition(int finishPosition)
        {
            if (finishPositionPrizes == null || finishPositionPrizes.Length == 0)
            {
                return 75;
            }
            if (finishPosition <= 1) return finishPositionPrizes[0];
            int idx = finishPosition - 1;
            if (idx < finishPositionPrizes.Length) return finishPositionPrizes[idx];
            return finishPositionPrizes[finishPositionPrizes.Length - 1];
        }
    }
}
