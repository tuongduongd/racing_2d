using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Serializable DTO persisted to JSON for player money, upgrade levels, race records, and unlocked maps.
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public int money = 200;
        public int engineLevel;
        public int brakesLevel;
        public int gripLevel;
        public int weightLevel;
        public int racesPlayed;
        public int racesWon;
        public float bestRaceTime;
        public List<string> unlockedMapIds = new List<string>();
        public string selectedMapId = string.Empty;
    }

    /// <summary>
    /// Manages prize money, progressive vehicle upgrades (exponential pricing),
    /// applying stats to VehiclePhysics, and saving/loading JSON progress.
    /// Encapsulates save data and exposes controlled transactions.
    /// </summary>
    public class CarUpgradeSystem
    {
        public const int MaxUpgradeLevel = 5;
        public const float CostGrowthFactor = 1.5f;

        private readonly string _saveFilePath;
        private VehicleStats _baseStats;
        private PlayerSaveData _data = new PlayerSaveData();

        public int Money => _data.money;
        public int RacesPlayed => _data.racesPlayed;
        public int RacesWon => _data.racesWon;
        public float BestRaceTime => _data.bestRaceTime;
        public string SaveFilePath => _saveFilePath;
        public VehicleStats BaseStats => _baseStats;
        public IReadOnlyList<string> UnlockedMapIds => _data.unlockedMapIds ?? (_data.unlockedMapIds = new List<string>());
        public string SelectedMapId => _data.selectedMapId ?? string.Empty;

        public CarUpgradeSystem() : this(Path.Combine(Application.persistentDataPath, "racing2d_save.json"), VehicleStats.Default)
        {
        }

        public CarUpgradeSystem(string customSavePath) : this(customSavePath, VehicleStats.Default)
        {
        }

        public CarUpgradeSystem(string customSavePath, VehicleStats baseStats)
        {
            _saveFilePath = customSavePath;
            _baseStats = baseStats.Sanitize();
            _data = new PlayerSaveData();
        }

        public void SetBaseStats(in VehicleStats baseStats)
        {
            _baseStats = baseStats.Sanitize();
        }

        public int GetLevel(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Engine => _data.engineLevel,
                UpgradeType.Brakes => _data.brakesLevel,
                UpgradeType.Grip => _data.gripLevel,
                UpgradeType.WeightReduction => _data.weightLevel,
                _ => 0
            };
        }

        public bool IsMaxLevel(UpgradeType type)
        {
            return GetLevel(type) >= MaxUpgradeLevel;
        }

        public int GetBaseCost(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Engine => 150,
                UpgradeType.Brakes => 120,
                UpgradeType.Grip => 130,
                UpgradeType.WeightReduction => 140,
                _ => 150
            };
        }

        /// <summary>
        /// Exponential cost per level: baseCost * 1.5^level.
        /// Returns 0 if already at MaxUpgradeLevel.
        /// </summary>
        public int GetUpgradeCost(UpgradeType type)
        {
            int level = GetLevel(type);
            if (level >= MaxUpgradeLevel) return 0;
            return Mathf.RoundToInt(GetBaseCost(type) * Mathf.Pow(CostGrowthFactor, level));
        }

        public bool CanAfford(UpgradeType type)
        {
            return !IsMaxLevel(type) && _data.money >= GetUpgradeCost(type);
        }

        public bool BuyUpgrade(UpgradeType type)
        {
            if (IsMaxLevel(type)) return false;

            int cost = GetUpgradeCost(type);
            if (_data.money < cost) return false;

            _data.money -= cost;
            SetLevel(type, GetLevel(type) + 1);
            Save();
            return true;
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            _data.money += amount;
        }

        /// <summary>
        /// Computes prize reward based on finishing position (1-based).
        /// </summary>
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

        /// <summary>
        /// Awards race prize money and updates player statistics, then persists to JSON.
        /// </summary>
        public int AwardRaceResult(int finishPosition, float raceTime)
        {
            return AwardRaceResult(finishPosition, raceTime, 1.0f);
        }

        /// <summary>
        /// Awards race prize money scaled by the map's reward multiplier, then persists to JSON.
        /// </summary>
        public int AwardRaceResult(int finishPosition, float raceTime, float rewardMultiplier)
        {
            float safeMult = Mathf.Clamp(rewardMultiplier, 1.0f, 3.0f);
            int prize = Mathf.RoundToInt(CalculatePrizeMoney(finishPosition) * safeMult);
            _data.money += prize;
            _data.racesPlayed++;
            if (finishPosition == 1)
            {
                _data.racesWon++;
            }
            if (raceTime > 0f && (_data.bestRaceTime <= 0f || raceTime < _data.bestRaceTime))
            {
                _data.bestRaceTime = raceTime;
            }
            Save();
            return prize;
        }

        /// <summary>
        /// Computes vehicle stats from baseline VehicleStats and current upgrade levels.
        /// </summary>
        public VehicleStats ComputeUpgradedStats()
        {
            int engineLv = GetLevel(UpgradeType.Engine);
            int brakesLv = GetLevel(UpgradeType.Brakes);
            int gripLv = GetLevel(UpgradeType.Grip);
            int weightLv = GetLevel(UpgradeType.WeightReduction);

            VehicleStats upgraded = _baseStats;
            upgraded.mass = _baseStats.mass - weightLv * 14f;
            upgraded.enginePower = _baseStats.enginePower + engineLv * 750f;
            upgraded.brakePower = _baseStats.brakePower + brakesLv * 1400f;
            upgraded.grip = _baseStats.grip + gripLv * 1.3f;
            upgraded.maxSpeed = _baseStats.maxSpeed + engineLv * 3.5f;
            return upgraded.Sanitize();
        }

        /// <summary>
        /// Applies all current upgrade levels idempotently onto VehiclePhysics from baseline stats.
        /// </summary>
        public void ApplyToCar(VehiclePhysics physics)
        {
            if (physics == null) return;
            physics.ConfigureStats(ComputeUpgradedStats());
        }

        public string GetUpgradeDisplayName(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Engine => "Động cơ (Engine)",
                UpgradeType.Brakes => "Phanh (Brakes)",
                UpgradeType.Grip => "Độ bám (Grip)",
                UpgradeType.WeightReduction => "Nhẹ xe (Weight)",
                _ => type.ToString()
            };
        }

        public string GetUpgradeEffectText(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Engine => "+750 Power, +3.5 MaxSpeed/cấp",
                UpgradeType.Brakes => "+1400 Lực phanh/cấp",
                UpgradeType.Grip => "+1.3 Độ bám đường/cấp",
                UpgradeType.WeightReduction => "-14 kg Trọng lượng/cấp",
                _ => string.Empty
            };
        }

        public bool IsMapUnlocked(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId) || _data.unlockedMapIds == null) return false;
            return _data.unlockedMapIds.Contains(mapId.Trim());
        }

        public bool UnlockMap(string mapId, bool saveImmediately = true)
        {
            if (string.IsNullOrWhiteSpace(mapId)) return false;

            string cleanId = mapId.Trim();
            _data.unlockedMapIds ??= new List<string>();
            if (_data.unlockedMapIds.Contains(cleanId)) return false;

            _data.unlockedMapIds.Add(cleanId);
            if (saveImmediately)
            {
                Save();
            }
            return true;
        }

        public bool TryPurchaseMapUnlock(string mapId, int cost)
        {
            if (string.IsNullOrWhiteSpace(mapId) || IsMapUnlocked(mapId)) return false;

            int safeCost = Mathf.Max(0, cost);
            if (_data.money < safeCost) return false;

            _data.money -= safeCost;
            _data.unlockedMapIds ??= new List<string>();
            _data.unlockedMapIds.Add(mapId.Trim());
            Save();
            return true;
        }

        public void SetSelectedMapId(string mapId, bool saveImmediately = true)
        {
            _data.selectedMapId = string.IsNullOrWhiteSpace(mapId) ? string.Empty : mapId.Trim();
            if (saveImmediately)
            {
                Save();
            }
        }

        public bool EnsureDefaultUnlockedMaps(IReadOnlyList<TrackDefinition> definitions, bool saveIfChanged = false)
        {
            if (definitions == null) return false;

            _data.unlockedMapIds ??= new List<string>();
            bool changed = false;
            for (int i = 0; i < definitions.Count; i++)
            {
                TrackDefinition def = definitions[i];
                if (def != null && def.UnlockedByDefault && !string.IsNullOrWhiteSpace(def.MapId))
                {
                    string cleanId = def.MapId.Trim();
                    if (!_data.unlockedMapIds.Contains(cleanId))
                    {
                        _data.unlockedMapIds.Add(cleanId);
                        changed = true;
                    }
                }
            }

            if (changed && saveIfChanged)
            {
                Save();
            }
            return changed;
        }

        public void ResetMapProgress(IReadOnlyList<TrackDefinition> definitions = null, bool saveImmediately = true)
        {
            _data.unlockedMapIds = new List<string>();
            _data.selectedMapId = string.Empty;

            if (definitions != null)
            {
                EnsureDefaultUnlockedMaps(definitions, saveIfChanged: false);
                for (int i = 0; i < definitions.Count; i++)
                {
                    TrackDefinition def = definitions[i];
                    if (def != null && !string.IsNullOrWhiteSpace(def.MapId) && IsMapUnlocked(def.MapId))
                    {
                        _data.selectedMapId = def.MapId.Trim();
                        break;
                    }
                }
            }

            if (saveImmediately)
            {
                Save();
            }
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_saveFilePath)) return;

            string dir = Path.GetDirectoryName(_saveFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(_saveFilePath, json);
        }

        public bool Load()
        {
            if (string.IsNullOrEmpty(_saveFilePath) || !File.Exists(_saveFilePath))
            {
                _data = new PlayerSaveData();
                return false;
            }

            try
            {
                string json = File.ReadAllText(_saveFilePath);
                PlayerSaveData loaded = JsonUtility.FromJson<PlayerSaveData>(json);
                if (loaded != null)
                {
                    _data = loaded;
                    ClampLoadedLevels();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CarUpgradeSystem] Failed to load save file: {ex.Message}");
            }

            _data = new PlayerSaveData();
            return false;
        }

        public void ResetProgress()
        {
            _data = new PlayerSaveData();
            Save();
        }

        private void SetLevel(UpgradeType type, int level)
        {
            int clamped = Mathf.Clamp(level, 0, MaxUpgradeLevel);
            switch (type)
            {
                case UpgradeType.Engine: _data.engineLevel = clamped; break;
                case UpgradeType.Brakes: _data.brakesLevel = clamped; break;
                case UpgradeType.Grip: _data.gripLevel = clamped; break;
                case UpgradeType.WeightReduction: _data.weightLevel = clamped; break;
            }
        }

        private void ClampLoadedLevels()
        {
            _data.money = Mathf.Max(0, _data.money);
            _data.engineLevel = Mathf.Clamp(_data.engineLevel, 0, MaxUpgradeLevel);
            _data.brakesLevel = Mathf.Clamp(_data.brakesLevel, 0, MaxUpgradeLevel);
            _data.gripLevel = Mathf.Clamp(_data.gripLevel, 0, MaxUpgradeLevel);
            _data.weightLevel = Mathf.Clamp(_data.weightLevel, 0, MaxUpgradeLevel);
            _data.racesPlayed = Mathf.Max(0, _data.racesPlayed);
            _data.racesWon = Mathf.Max(0, _data.racesWon);
            _data.bestRaceTime = Mathf.Max(0f, _data.bestRaceTime);

            if (_data.unlockedMapIds == null)
            {
                _data.unlockedMapIds = new List<string>();
            }
            else
            {
                var cleaned = new List<string>(_data.unlockedMapIds.Count);
                for (int i = 0; i < _data.unlockedMapIds.Count; i++)
                {
                    string id = _data.unlockedMapIds[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    string trimmed = id.Trim();
                    if (!cleaned.Contains(trimmed))
                    {
                        cleaned.Add(trimmed);
                    }
                }
                _data.unlockedMapIds = cleaned;
            }

            _data.selectedMapId = string.IsNullOrWhiteSpace(_data.selectedMapId)
                ? string.Empty
                : _data.selectedMapId.Trim();
        }
    }
}
