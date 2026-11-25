using System;
using System.IO;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Loads and validates external gameplay balancing config from StreamingAssets/Config/game_balance.json.
    /// </summary>
    public sealed class ExternalBalanceConfigLoader
    {
        private readonly ISaveStorage _storage;
        private readonly IJsonCodec _jsonCodec;

        public static string DefaultConfigPath => Path.Combine(Application.streamingAssetsPath, "Config", "game_balance.json");

        public ExternalBalanceConfigLoader()
            : this(new FileSaveStorageAdapter(), new UnityJsonCodec())
        {
        }

        public ExternalBalanceConfigLoader(ISaveStorage storage, IJsonCodec jsonCodec)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _jsonCodec = jsonCodec ?? throw new ArgumentNullException(nameof(jsonCodec));
        }

        public GameBalanceConfig LoadStrict(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !_storage.Exists(filePath))
            {
                throw new InvalidTrackConfigurationException(filePath, $"Balance config file not found: '{filePath}'.");
            }

            try
            {
                string json = _storage.ReadAllText(filePath);
                GameBalanceConfig cfg = _jsonCodec.Deserialize<GameBalanceConfig>(json);
                if (cfg == null)
                {
                    throw new InvalidTrackConfigurationException(filePath, "Balance config JSON deserialized to null.");
                }
                cfg.ValidateOrThrow(filePath);
                return cfg.Sanitize();
            }
            catch (SaveDataCorruptedException ex)
            {
                throw new InvalidTrackConfigurationException(filePath, $"Balance config JSON is corrupted: {ex.Message}", ex);
            }
        }

        public GameBalanceConfig LoadOrDefault(string filePath = null)
        {
            string targetPath = string.IsNullOrWhiteSpace(filePath) ? DefaultConfigPath : filePath;
            if (!_storage.Exists(targetPath))
            {
                return GameBalanceConfig.CreateDefault();
            }

            try
            {
                return LoadStrict(targetPath);
            }
            catch (GameDomainException ex)
            {
                Debug.LogWarning($"[ExternalBalanceConfigLoader] Falling back to defaults due to invalid config '{targetPath}': {ex.Message}");
                return GameBalanceConfig.CreateDefault();
            }
        }
    }
}
