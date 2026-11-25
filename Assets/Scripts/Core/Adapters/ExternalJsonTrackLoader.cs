using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Loads and validates external TrackJsonDto files from StreamingAssets/Maps/*.json.
    /// </summary>
    public sealed class ExternalJsonTrackLoader : ITrackDataProvider
    {
        private readonly ISaveStorage _storage;
        private readonly IJsonCodec _jsonCodec;

        public static string DefaultMapsDirectory => Path.Combine(Application.streamingAssetsPath, "Maps");

        public ExternalJsonTrackLoader()
            : this(new FileSaveStorageAdapter(), new UnityJsonCodec())
        {
        }

        public ExternalJsonTrackLoader(ISaveStorage storage, IJsonCodec jsonCodec)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _jsonCodec = jsonCodec ?? throw new ArgumentNullException(nameof(jsonCodec));
        }

        public TrackJsonDto LoadTrackFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !_storage.Exists(filePath))
            {
                throw new InvalidTrackConfigurationException(filePath, $"Track JSON file not found: '{filePath}'.");
            }

            try
            {
                string json = _storage.ReadAllText(filePath);
                TrackJsonDto dto = _jsonCodec.Deserialize<TrackJsonDto>(json);
                if (dto == null)
                {
                    throw new InvalidTrackConfigurationException(filePath, $"Track JSON '{filePath}' deserialized to null.");
                }
                dto.ValidateOrThrow(filePath);
                return dto;
            }
            catch (SaveDataCorruptedException ex)
            {
                throw new InvalidTrackConfigurationException(filePath, $"Track JSON '{filePath}' is corrupted: {ex.Message}", ex);
            }
        }

        public IReadOnlyList<TrackJsonDto> LoadTracks(string directoryPath)
        {
            var results = new List<TrackJsonDto>();
            string targetDir = string.IsNullOrWhiteSpace(directoryPath) ? DefaultMapsDirectory : directoryPath;
            if (!Directory.Exists(targetDir))
            {
                return results;
            }

            string[] files = Directory.GetFiles(targetDir, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    TrackJsonDto dto = LoadTrackFile(files[i]);
                    results.Add(dto);
                }
                catch (GameDomainException ex)
                {
                    Debug.LogWarning($"[ExternalJsonTrackLoader] Skipped invalid map JSON '{files[i]}': {ex.Message}");
                }
            }

            return results;
        }
    }
}
