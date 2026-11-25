using System;

namespace RacingGame
{
    /// <summary>
    /// Pure C# mid-race state save/load service persisting racer positions, velocities, laps, and checkpoints.
    /// </summary>
    public sealed class MidRaceSaveService
    {
        private readonly ISaveStorage _storage;
        private readonly IJsonCodec _jsonCodec;
        private readonly string _saveFilePath;
        private readonly Action<string> _warningLogger;

        public string SaveFilePath => _saveFilePath;

        public MidRaceSaveService(
            ISaveStorage storage,
            IJsonCodec jsonCodec,
            string saveFilePath,
            Action<string> warningLogger = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _jsonCodec = jsonCodec ?? throw new ArgumentNullException(nameof(jsonCodec));
            _saveFilePath = saveFilePath ?? string.Empty;
            _warningLogger = warningLogger;
        }

        public bool HasSavedRace()
        {
            return !string.IsNullOrEmpty(_saveFilePath) && _storage.Exists(_saveFilePath);
        }

        public void SaveSnapshot(MidRaceSaveData snapshot)
        {
            if (snapshot == null)
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Cannot persist null MidRaceSaveData snapshot.");
            }
            if (string.IsNullOrEmpty(_saveFilePath)) return;

            snapshot.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string json = _jsonCodec.Serialize(snapshot, true);
            _storage.WriteAllText(_saveFilePath, json);
        }

        public MidRaceSaveData LoadSnapshotStrict()
        {
            if (string.IsNullOrEmpty(_saveFilePath) || !_storage.Exists(_saveFilePath))
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Mid-race save file does not exist.");
            }

            string json = _storage.ReadAllText(_saveFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Mid-race save file is empty.");
            }

            MidRaceSaveData snapshot = _jsonCodec.Deserialize<MidRaceSaveData>(json);
            if (snapshot == null || snapshot.racers == null || snapshot.racers.Count == 0 || snapshot.totalLaps < 1)
            {
                throw new SaveDataCorruptedException(_saveFilePath, "Mid-race snapshot is missing racers or valid lap metadata.");
            }

            snapshot.raceTime = CoreMath.Max(0f, snapshot.raceTime);
            snapshot.finishedCount = CoreMath.Max(0, snapshot.finishedCount);
            snapshot.totalLaps = CoreMath.Clamp(snapshot.totalLaps, 1, 10);
            return snapshot;
        }

        public bool TryLoadSnapshot(out MidRaceSaveData snapshot)
        {
            try
            {
                snapshot = LoadSnapshotStrict();
                return true;
            }
            catch (SaveDataCorruptedException ex)
            {
                _warningLogger?.Invoke($"[MidRaceSaveService] Corrupted mid-race snapshot ignored: {ex.Message}");
            }
            catch (Exception ex)
            {
                _warningLogger?.Invoke($"[MidRaceSaveService] Failed to load mid-race snapshot: {ex.Message}");
            }

            snapshot = null;
            return false;
        }

        public void ClearSavedRace()
        {
            if (!string.IsNullOrEmpty(_saveFilePath))
            {
                _storage.Delete(_saveFilePath);
            }
        }
    }
}
