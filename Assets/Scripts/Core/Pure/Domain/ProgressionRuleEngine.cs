using System;
using System.Collections.Generic;

namespace RacingGame
{
    /// <summary>
    /// Engine-independent core progression, economy, high-score, and Decorator stat computation engine.
    /// Communicates exclusively through ISaveStorage and IJsonCodec abstractions (DIP / Constructor Injection).
    /// </summary>
    public sealed class ProgressionRuleEngine
    {
        public const int DefaultMaxUpgradeLevel = 5;
        public const int MaxHighScoresStored = 15;

        private readonly ISaveStorage _storage;
        private readonly IJsonCodec _jsonCodec;
        private readonly string _saveFilePath;
        private readonly Action<string> _warningLogger;

        private PureVehicleStats _baseStats;
        private GameBalanceConfig _balanceConfig;
        private PlayerSaveData _data = new PlayerSaveData();
        private readonly List<HighScoreEntry> _highScoreScratch = new List<HighScoreEntry>();

        public int MaxUpgradeLevel => _balanceConfig.maxUpgradeLevel;
        public float CostGrowthFactor => _balanceConfig.upgradeCostGrowthFactor;

        public int Money => _data.money;
        public int RacesPlayed => _data.racesPlayed;
        public int RacesWon => _data.racesWon;
        public float BestRaceTime => _data.bestRaceTime;
        public string SaveFilePath => _saveFilePath;
        public PureVehicleStats BaseStats => _baseStats;
        public GameBalanceConfig BalanceConfig => _balanceConfig;
        public IReadOnlyList<string> UnlockedMapIds => _data.unlockedMapIds ?? (_data.unlockedMapIds = new List<string>());
        public string SelectedMapId => _data.selectedMapId ?? string.Empty;
        public IReadOnlyList<HighScoreEntry> AllHighScores => _data.highScores ?? (_data.highScores = new List<HighScoreEntry>());

        public ProgressionRuleEngine(
            ISaveStorage storage,
            IJsonCodec jsonCodec,
            string saveFilePath,
            PureVehicleStats baseStats,
            GameBalanceConfig balanceConfig = null,
            Action<string> warningLogger = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _jsonCodec = jsonCodec ?? throw new ArgumentNullException(nameof(jsonCodec));
            _saveFilePath = saveFilePath ?? string.Empty;
            _baseStats = baseStats.Sanitize();
            _balanceConfig = (balanceConfig ?? GameBalanceConfig.CreateDefault()).Sanitize();
            _warningLogger = warningLogger;
            _data = new PlayerSaveData();
        }

        public void SetBaseStats(in PureVehicleStats baseStats)
        {
            _baseStats = baseStats.Sanitize();
        }

        public void ApplyBalanceConfig(GameBalanceConfig config)
        {
            if (config == null) return;
            _balanceConfig = config.Sanitize();
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
            return _balanceConfig.GetBaseCost(type);
        }

        public int GetUpgradeCost(UpgradeType type)
        {
            int level = GetLevel(type);
            if (level >= MaxUpgradeLevel) return 0;
            return CoreMath.RoundToInt(GetBaseCost(type) * CoreMath.Pow(CostGrowthFactor, level));
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

        public void BuyUpgradeStrict(UpgradeType type)
        {
            if (IsMaxLevel(type))
            {
                throw new InvalidUpgradeTransactionException(
                    type,
                    $"Upgrade '{type}' is already at max level {MaxUpgradeLevel}.");
            }

            int cost = GetUpgradeCost(type);
            if (_data.money < cost)
            {
                throw new InvalidUpgradeTransactionException(
                    type,
                    $"Insufficient funds for '{type}': required {cost}, available {_data.money}.");
            }

            _data.money -= cost;
            SetLevel(type, GetLevel(type) + 1);
            Save();
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            _data.money += amount;
        }

        public int CalculatePrizeMoney(int finishPosition)
        {
            return _balanceConfig.GetPrizeForPosition(finishPosition);
        }

        public int AwardRaceResult(int finishPosition, float raceTime, float rewardMultiplier = 1.0f)
        {
            float safeMult = CoreMath.Clamp(rewardMultiplier, 1.0f, 3.0f);
            int prize = CoreMath.RoundToInt(CalculatePrizeMoney(finishPosition) * safeMult);
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

        public void RecordHighScore(string mapId, string racerName, float finishTime, int totalLaps, DifficultyLevel difficulty)
        {
            if (finishTime <= 0f || string.IsNullOrWhiteSpace(mapId)) return;

            _data.highScores ??= new List<HighScoreEntry>();
            _data.highScores.Add(new HighScoreEntry
            {
                mapId = mapId.Trim(),
                racerName = string.IsNullOrWhiteSpace(racerName) ? "Player" : racerName.Trim(),
                finishTime = finishTime,
                totalLaps = CoreMath.Max(1, totalLaps),
                difficulty = difficulty.ToString(),
                recordedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });

            _data.highScores.Sort((a, b) => a.finishTime.CompareTo(b.finishTime));
            if (_data.highScores.Count > MaxHighScoresStored)
            {
                _data.highScores.RemoveRange(MaxHighScoresStored, _data.highScores.Count - MaxHighScoresStored);
            }
            Save();
        }

        public IReadOnlyList<HighScoreEntry> GetHighScores(string mapId = null)
        {
            _highScoreScratch.Clear();
            if (_data.highScores == null) return _highScoreScratch;

            string filter = string.IsNullOrWhiteSpace(mapId) ? null : mapId.Trim();
            for (int i = 0; i < _data.highScores.Count; i++)
            {
                HighScoreEntry entry = _data.highScores[i];
                if (entry == null || entry.finishTime <= 0f) continue;
                if (filter != null && !string.Equals(entry.mapId, filter, StringComparison.OrdinalIgnoreCase)) continue;
                _highScoreScratch.Add(entry);
            }
            return _highScoreScratch;
        }

        /// <summary>
        /// Builds a Structural Decorator chain over baseline vehicle stats from current upgrade levels.
        /// </summary>
        public IVehicleStatsProvider BuildStatsProviderChain()
        {
            IVehicleStatsProvider provider = new BaseVehicleStatsProvider(_baseStats);
            provider = new EngineUpgradeDecorator(provider, GetLevel(UpgradeType.Engine), _balanceConfig.enginePowerPerLevel, _balanceConfig.maxSpeedPerLevel);
            provider = new BrakesUpgradeDecorator(provider, GetLevel(UpgradeType.Brakes), _balanceConfig.brakePowerPerLevel);
            provider = new GripUpgradeDecorator(provider, GetLevel(UpgradeType.Grip), _balanceConfig.gripPerLevel);
            provider = new WeightReductionDecorator(provider, GetLevel(UpgradeType.WeightReduction), _balanceConfig.weightReductionPerLevel);
            return provider;
        }

        public PureVehicleStats ComputeUpgradedStats()
        {
            return BuildStatsProviderChain().GetStats();
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
                UpgradeType.Engine => $"+{_balanceConfig.enginePowerPerLevel:0} Power, +{_balanceConfig.maxSpeedPerLevel:0.#} MaxSpeed/cấp",
                UpgradeType.Brakes => $"+{_balanceConfig.brakePowerPerLevel:0} Lực phanh/cấp",
                UpgradeType.Grip => $"+{_balanceConfig.gripPerLevel:0.#} Độ bám đường/cấp",
                UpgradeType.WeightReduction => $"-{_balanceConfig.weightReductionPerLevel:0} kg Trọng lượng/cấp",
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

            int safeCost = CoreMath.Max(0, cost);
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

        public bool EnsureDefaultUnlockedMaps(IReadOnlyList<string> defaultUnlockedMapIds, bool saveIfChanged = false)
        {
            if (defaultUnlockedMapIds == null) return false;

            _data.unlockedMapIds ??= new List<string>();
            bool changed = false;
            for (int i = 0; i < defaultUnlockedMapIds.Count; i++)
            {
                string id = defaultUnlockedMapIds[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                string cleanId = id.Trim();
                if (!_data.unlockedMapIds.Contains(cleanId))
                {
                    _data.unlockedMapIds.Add(cleanId);
                    changed = true;
                }
            }

            if (changed && saveIfChanged)
            {
                Save();
            }
            return changed;
        }

        public void ResetMapProgress(IReadOnlyList<string> defaultUnlockedMapIds = null, bool saveImmediately = true)
        {
            _data.unlockedMapIds = new List<string>();
            _data.selectedMapId = string.Empty;

            if (defaultUnlockedMapIds != null)
            {
                EnsureDefaultUnlockedMaps(defaultUnlockedMapIds, saveIfChanged: false);
                for (int i = 0; i < defaultUnlockedMapIds.Count; i++)
                {
                    string id = defaultUnlockedMapIds[i];
                    if (!string.IsNullOrWhiteSpace(id) && IsMapUnlocked(id))
                    {
                        _data.selectedMapId = id.Trim();
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
            string json = _jsonCodec.Serialize(_data, true);
            _storage.WriteAllText(_saveFilePath, json);
        }

        public bool Load()
        {
            if (string.IsNullOrEmpty(_saveFilePath) || !_storage.Exists(_saveFilePath))
            {
                _data = new PlayerSaveData();
                return false;
            }

            try
            {
                return LoadStrict();
            }
            catch (SaveDataCorruptedException ex)
            {
                _warningLogger?.Invoke($"[CarUpgradeSystem] Corrupted save file handled safely: {ex.Message}");
            }
            catch (Exception ex)
            {
                _warningLogger?.Invoke($"[CarUpgradeSystem] Failed to load save file: {ex.Message}");
            }

            _data = new PlayerSaveData();
            return false;
        }

        public bool LoadStrict()
        {
            if (string.IsNullOrEmpty(_saveFilePath) || !_storage.Exists(_saveFilePath))
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Save file path is empty or file does not exist.");
            }

            string json = _storage.ReadAllText(_saveFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Save file is empty.");
            }

            PlayerSaveData loaded = _jsonCodec.Deserialize<PlayerSaveData>(json);
            if (loaded == null)
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Deserialized save payload was null.");
            }

            _data = loaded;
            ClampLoadedLevels();
            return true;
        }

        public void ResetProgress()
        {
            _data = new PlayerSaveData();
            Save();
        }

        private void SetLevel(UpgradeType type, int level)
        {
            int clamped = CoreMath.Clamp(level, 0, MaxUpgradeLevel);
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
            _data.money = CoreMath.Max(0, _data.money);
            _data.engineLevel = CoreMath.Clamp(_data.engineLevel, 0, MaxUpgradeLevel);
            _data.brakesLevel = CoreMath.Clamp(_data.brakesLevel, 0, MaxUpgradeLevel);
            _data.gripLevel = CoreMath.Clamp(_data.gripLevel, 0, MaxUpgradeLevel);
            _data.weightLevel = CoreMath.Clamp(_data.weightLevel, 0, MaxUpgradeLevel);
            _data.racesPlayed = CoreMath.Max(0, _data.racesPlayed);
            _data.racesWon = CoreMath.Max(0, _data.racesWon);
            _data.bestRaceTime = CoreMath.Max(0f, _data.bestRaceTime);

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

            if (_data.highScores == null)
            {
                _data.highScores = new List<HighScoreEntry>();
            }
            else
            {
                for (int i = _data.highScores.Count - 1; i >= 0; i--)
                {
                    HighScoreEntry entry = _data.highScores[i];
                    if (entry == null || entry.finishTime <= 0f || string.IsNullOrWhiteSpace(entry.mapId))
                    {
                        _data.highScores.RemoveAt(i);
                    }
                }
            }

            _data.selectedMapId = string.IsNullOrWhiteSpace(_data.selectedMapId)
                ? string.Empty
                : _data.selectedMapId.Trim();
        }
    }
}
