using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Engine-integrated facade over pure C# ProgressionRuleEngine (DIP + Decorator stats chain).
    /// Supports both legacy zero-arg/path constructors and full Constructor Injection via ISaveStorage / IJsonCodec.
    /// </summary>
    public class CarUpgradeSystem
    {
        public const int MaxUpgradeLevel = ProgressionRuleEngine.DefaultMaxUpgradeLevel;
        public const float CostGrowthFactor = 1.5f;

        private readonly ProgressionRuleEngine _engine;

        public int Money => _engine.Money;
        public int RacesPlayed => _engine.RacesPlayed;
        public int RacesWon => _engine.RacesWon;
        public float BestRaceTime => _engine.BestRaceTime;
        public string SaveFilePath => _engine.SaveFilePath;
        public VehicleStats BaseStats => VehicleStats.FromPure(_engine.BaseStats);
        public GameBalanceConfig BalanceConfig => _engine.BalanceConfig;
        public IReadOnlyList<string> UnlockedMapIds => _engine.UnlockedMapIds;
        public string SelectedMapId => _engine.SelectedMapId;
        public IReadOnlyList<HighScoreEntry> AllHighScores => _engine.AllHighScores;
        public ProgressionRuleEngine Engine => _engine;

        public CarUpgradeSystem()
            : this(Path.Combine(Application.persistentDataPath, "racing2d_save.json"), VehicleStats.Default)
        {
        }

        public CarUpgradeSystem(string customSavePath)
            : this(customSavePath, VehicleStats.Default)
        {
        }

        public CarUpgradeSystem(string customSavePath, VehicleStats baseStats)
            : this(new FileSaveStorageAdapter(), new UnityJsonCodec(), customSavePath, baseStats, null)
        {
        }

        public CarUpgradeSystem(
            ISaveStorage storage,
            IJsonCodec jsonCodec,
            string customSavePath,
            VehicleStats baseStats,
            GameBalanceConfig balanceConfig = null)
        {
            _engine = new ProgressionRuleEngine(
                storage,
                jsonCodec,
                customSavePath,
                baseStats.ToPure(),
                balanceConfig,
                Debug.LogWarning);
        }

        public void SetBaseStats(in VehicleStats baseStats)
        {
            _engine.SetBaseStats(baseStats.ToPure());
        }

        public void ApplyBalanceConfig(GameBalanceConfig config)
        {
            _engine.ApplyBalanceConfig(config);
        }

        public int GetLevel(UpgradeType type) => _engine.GetLevel(type);
        public bool IsMaxLevel(UpgradeType type) => _engine.IsMaxLevel(type);
        public int GetBaseCost(UpgradeType type) => _engine.GetBaseCost(type);
        public int GetUpgradeCost(UpgradeType type) => _engine.GetUpgradeCost(type);
        public bool CanAfford(UpgradeType type) => _engine.CanAfford(type);
        public bool BuyUpgrade(UpgradeType type) => _engine.BuyUpgrade(type);
        public void BuyUpgradeStrict(UpgradeType type) => _engine.BuyUpgradeStrict(type);
        public void AddMoney(int amount) => _engine.AddMoney(amount);

        public static int CalculatePrizeMoney(int finishPosition)
        {
            return finishPosition switch
            {
                1 => 500,
                2 => 320,
                3 => 200,
                4 => 120,
                _ => 75
            };
        }

        public int AwardRaceResult(int finishPosition, float raceTime)
        {
            return _engine.AwardRaceResult(finishPosition, raceTime, 1.0f);
        }

        public int AwardRaceResult(int finishPosition, float raceTime, float rewardMultiplier)
        {
            return _engine.AwardRaceResult(finishPosition, raceTime, rewardMultiplier);
        }

        public void RecordHighScore(string mapId, string racerName, float finishTime, int totalLaps, DifficultyLevel difficulty)
        {
            _engine.RecordHighScore(mapId, racerName, finishTime, totalLaps, difficulty);
        }

        public IReadOnlyList<HighScoreEntry> GetHighScores(string mapId = null)
        {
            return _engine.GetHighScores(mapId);
        }

        public VehicleStats ComputeUpgradedStats()
        {
            return VehicleStats.FromPure(_engine.ComputeUpgradedStats());
        }

        public void ApplyToCar(VehiclePhysics physics)
        {
            if (physics == null) return;
            physics.ConfigureStats(ComputeUpgradedStats());
        }

        public string GetUpgradeDisplayName(UpgradeType type) => _engine.GetUpgradeDisplayName(type);
        public string GetUpgradeEffectText(UpgradeType type) => _engine.GetUpgradeEffectText(type);
        public bool IsMapUnlocked(string mapId) => _engine.IsMapUnlocked(mapId);
        public bool UnlockMap(string mapId, bool saveImmediately = true) => _engine.UnlockMap(mapId, saveImmediately);
        public bool TryPurchaseMapUnlock(string mapId, int cost) => _engine.TryPurchaseMapUnlock(mapId, cost);
        public void SetSelectedMapId(string mapId, bool saveImmediately = true) => _engine.SetSelectedMapId(mapId, saveImmediately);

        public bool EnsureDefaultUnlockedMaps(IReadOnlyList<TrackDefinition> definitions, bool saveIfChanged = false)
        {
            if (definitions == null) return false;
            var defaultIds = new List<string>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                TrackDefinition def = definitions[i];
                if (def != null && def.UnlockedByDefault && !string.IsNullOrWhiteSpace(def.MapId))
                {
                    defaultIds.Add(def.MapId.Trim());
                }
            }
            return _engine.EnsureDefaultUnlockedMaps(defaultIds, saveIfChanged);
        }

        public void ResetMapProgress(IReadOnlyList<TrackDefinition> definitions = null, bool saveImmediately = true)
        {
            if (definitions == null)
            {
                _engine.ResetMapProgress(null, saveImmediately);
                return;
            }

            var defaultIds = new List<string>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                TrackDefinition def = definitions[i];
                if (def != null && def.UnlockedByDefault && !string.IsNullOrWhiteSpace(def.MapId))
                {
                    defaultIds.Add(def.MapId.Trim());
                }
            }

            _engine.ResetMapProgress(defaultIds, saveImmediately: false);

            for (int i = 0; i < definitions.Count; i++)
            {
                TrackDefinition def = definitions[i];
                if (def != null && !string.IsNullOrWhiteSpace(def.MapId) && IsMapUnlocked(def.MapId))
                {
                    _engine.SetSelectedMapId(def.MapId.Trim(), saveImmediately: false);
                    break;
                }
            }

            if (saveImmediately)
            {
                Save();
            }
        }

        public void Save() => _engine.Save();
        public bool Load() => _engine.Load();
        public bool LoadStrict() => _engine.LoadStrict();
        public void ResetProgress() => _engine.ResetProgress();
    }
}
