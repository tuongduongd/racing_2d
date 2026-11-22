using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Quản lý danh sách các Map (TrackDefinition), trạng thái mở khóa theo PlayerSaveData / TrackDefinition,
    /// lưu Map đang chọn & kỷ lục thời gian từng Map, và áp dụng Map lên Track/Environment.
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        public const string SelectedMapPrefKey = "RacingGame_SelectedMap_v1";
        private const string BestTimePrefPrefix = "RacingGame_BestTime_";

        [Header("Danh Sách Map Đua (Kéo thả TrackDefinition Assets vào đây)")]
        [SerializeField] private TrackDefinition[] mapAssets = new TrackDefinition[0];

        [Header("Mở Khóa & Khởi Tạo")]
        [SerializeField] private bool forceUnlockAllMaps;

        private readonly List<TrackDefinition> _maps = new List<TrackDefinition>();
        private readonly List<Object> _runtimeCreatedAssets = new List<Object>();
        private readonly HashSet<string> _sessionUnlockedIds = new HashSet<string>();
        private CarUpgradeSystem _saveSystem;
        private int _selectedIndex;
        private bool _initialized;

        public CarUpgradeSystem SaveSystem => _saveSystem;

        public IReadOnlyList<TrackDefinition> Maps
        {
            get
            {
                EnsureInitialized();
                return _maps;
            }
        }

        public int MapCount => Maps.Count;

        public int SelectedIndex
        {
            get
            {
                EnsureInitialized();
                return _maps.Count > 0 ? Mathf.Clamp(_selectedIndex, 0, _maps.Count - 1) : 0;
            }
        }

        public TrackDefinition SelectedMap
        {
            get
            {
                EnsureInitialized();
                return _maps.Count > 0 ? _maps[SelectedIndex] : null;
            }
        }

        public string SelectedMapId => SelectedMap != null ? SelectedMap.MapId : string.Empty;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeCreatedAssets.Count; i++)
            {
                if (_runtimeCreatedAssets[i] != null)
                {
                    DestroyImmediate(_runtimeCreatedAssets[i]);
                }
            }
            _runtimeCreatedAssets.Clear();
        }

        public void EnsureInitialized()
        {
            if (_initialized && _maps.Count > 0) return;

            _maps.Clear();
            if (mapAssets != null)
            {
                for (int i = 0; i < mapAssets.Length; i++)
                {
                    if (mapAssets[i] != null && !_maps.Contains(mapAssets[i]))
                    {
                        _maps.Add(mapAssets[i]);
                    }
                }
            }

            if (_maps.Count == 0)
            {
                MapTheme cityMorningTheme = MapTheme.CreateCityMorningPreset();
                MapTheme coastalTheme = MapTheme.CreateCoastalHighwayPreset();
                MapTheme neonTheme = MapTheme.CreateNeonNightExpressPreset();
                MapTheme cityDistrictTheme = MapTheme.CreateCityDistrictPreset();

                TrackDefinition cityMorningMap = TrackDefinition.CreateCityMorningPreset(cityMorningTheme);
                TrackDefinition coastalMap = TrackDefinition.CreateCoastalHighwayPreset(coastalTheme);
                TrackDefinition neonMap = TrackDefinition.CreateNeonNightExpressPreset(neonTheme);
                TrackDefinition cityDistrictMap = TrackDefinition.CreateCityDistrictPreset(cityDistrictTheme);

                _runtimeCreatedAssets.Add(cityMorningTheme);
                _runtimeCreatedAssets.Add(coastalTheme);
                _runtimeCreatedAssets.Add(neonTheme);
                _runtimeCreatedAssets.Add(cityDistrictTheme);
                _runtimeCreatedAssets.Add(cityMorningMap);
                _runtimeCreatedAssets.Add(coastalMap);
                _runtimeCreatedAssets.Add(neonMap);
                _runtimeCreatedAssets.Add(cityDistrictMap);

                _maps.Add(cityMorningMap);
                _maps.Add(coastalMap);
                _maps.Add(neonMap);
                _maps.Add(cityDistrictMap);
            }

            _initialized = true;
            SyncWithSaveSystem();
        }

        public void BindSaveSystem(CarUpgradeSystem saveSystem)
        {
            _saveSystem = saveSystem;
            if (!_initialized || _maps.Count == 0)
            {
                EnsureInitialized();
            }
            else
            {
                SyncWithSaveSystem();
            }
        }

        public void SetMapAssets(TrackDefinition[] definitions)
        {
            mapAssets = definitions;
            _initialized = false;
            EnsureInitialized();
        }

        public bool IsMapUnlocked(string mapId)
        {
            EnsureInitialized();
            TrackDefinition def = GetMap(mapId);
            if (def == null) return false;
            if (forceUnlockAllMaps) return true;

            if (_saveSystem != null)
            {
                return _saveSystem.IsMapUnlocked(def.MapId);
            }

            return def.UnlockedByDefault || _sessionUnlockedIds.Contains(def.MapId);
        }

        public int GetMapUnlockCost(string mapId)
        {
            TrackDefinition def = GetMap(mapId);
            if (def == null || def.UnlockedByDefault) return 0;
            return Mathf.Max(1, def.DifficultyStars) * 250;
        }

        public bool CanAffordMapUnlock(string mapId)
        {
            EnsureInitialized();
            if (IsMapUnlocked(mapId)) return true;
            if (_saveSystem == null) return false;
            return _saveSystem.Money >= GetMapUnlockCost(mapId);
        }

        public bool UnlockMap(string mapId, bool saveImmediately = true)
        {
            EnsureInitialized();
            TrackDefinition def = GetMap(mapId);
            if (def == null) return false;

            _sessionUnlockedIds.Add(def.MapId);
            if (_saveSystem != null)
            {
                return _saveSystem.UnlockMap(def.MapId, saveImmediately);
            }
            return true;
        }

        public bool TryPurchaseMapUnlock(string mapId)
        {
            EnsureInitialized();
            TrackDefinition def = GetMap(mapId);
            if (def == null || IsMapUnlocked(def.MapId) || _saveSystem == null) return false;

            int cost = GetMapUnlockCost(def.MapId);
            if (!_saveSystem.TryPurchaseMapUnlock(def.MapId, cost)) return false;

            _sessionUnlockedIds.Add(def.MapId);
            return true;
        }

        public TrackDefinition GetMap(string mapId)
        {
            EnsureInitialized();
            int idx = FindIndexById(mapId);
            return idx >= 0 ? _maps[idx] : null;
        }

        public TrackDefinition GetMapAt(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= _maps.Count) return null;
            return _maps[index];
        }

        public int GetMapIndex(string mapId)
        {
            EnsureInitialized();
            return FindIndexById(mapId);
        }

        public bool SelectMap(string mapId, WaypointTrack track = null, EnvironmentDecorator envDecorator = null)
        {
            EnsureInitialized();
            int idx = FindIndexById(mapId);
            if (idx < 0) return false;
            if (!IsMapUnlocked(_maps[idx].MapId)) return false;

            _selectedIndex = idx;
            PlayerPrefs.SetString(SelectedMapPrefKey, _maps[idx].MapId);
            PlayerPrefs.Save();
            _saveSystem?.SetSelectedMapId(_maps[idx].MapId, saveImmediately: true);

            ApplySelectedMapToTrack(track, envDecorator);
            return true;
        }

        public bool SelectMapByIndex(int index, WaypointTrack track = null, EnvironmentDecorator envDecorator = null)
        {
            EnsureInitialized();
            if (_maps.Count == 0) return false;

            int safe = ((index % _maps.Count) + _maps.Count) % _maps.Count;
            return SelectMap(_maps[safe].MapId, track, envDecorator);
        }

        public TrackDefinition StepMapSelection(int delta, WaypointTrack track = null, EnvironmentDecorator envDecorator = null)
        {
            EnsureInitialized();
            if (_maps.Count == 0) return null;
            if (delta == 0) return SelectedMap;

            int count = _maps.Count;
            int startIdx = Mathf.Clamp(_selectedIndex, 0, count - 1);
            int dir = delta > 0 ? 1 : -1;
            int offset = Mathf.Abs(delta);
            int candidate = ((startIdx + dir * offset) % count + count) % count;

            for (int attempt = 0; attempt < count; attempt++)
            {
                int idx = ((candidate + dir * attempt) % count + count) % count;
                if (IsMapUnlocked(_maps[idx].MapId))
                {
                    SelectMapByIndex(idx, track, envDecorator);
                    return SelectedMap;
                }
            }

            return SelectedMap;
        }

        public void ApplySelectedMapToTrack(WaypointTrack track, EnvironmentDecorator envDecorator = null)
        {
            TrackDefinition active = SelectedMap;
            if (active == null) return;

            if (track != null)
            {
                track.ApplyTrackDefinition(active);
            }

            if (envDecorator != null && track != null)
            {
                envDecorator.RebuildEnvironment(track, active.Theme);
            }
        }

        public float GetBestTime(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return 0f;
            return PlayerPrefs.GetFloat(BestTimePrefPrefix + mapId, 0f);
        }

        public bool RecordBestTime(string mapId, float raceTime)
        {
            if (string.IsNullOrEmpty(mapId) || raceTime <= 0f) return false;

            float prev = GetBestTime(mapId);
            if (prev <= 0f || raceTime < prev)
            {
                PlayerPrefs.SetFloat(BestTimePrefPrefix + mapId, raceTime);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        public void ResetProgress()
        {
            EnsureInitialized();
            for (int i = 0; i < _maps.Count; i++)
            {
                PlayerPrefs.DeleteKey(BestTimePrefPrefix + _maps[i].MapId);
            }

            _sessionUnlockedIds.Clear();
            SeedDefaultUnlockedIds();
            _saveSystem?.ResetMapProgress(_maps, saveImmediately: true);

            _selectedIndex = FindFirstUnlockedIndex();
            if (_maps.Count > 0)
            {
                string defaultId = _maps[_selectedIndex].MapId;
                PlayerPrefs.SetString(SelectedMapPrefKey, defaultId);
                _saveSystem?.SetSelectedMapId(defaultId, saveImmediately: true);
            }
            else
            {
                PlayerPrefs.DeleteKey(SelectedMapPrefKey);
            }
            PlayerPrefs.Save();
        }

        private void SyncWithSaveSystem()
        {
            SeedDefaultUnlockedIds();

            if (_saveSystem != null)
            {
                _saveSystem.EnsureDefaultUnlockedMaps(_maps, saveIfChanged: false);
                IReadOnlyList<string> savedUnlocked = _saveSystem.UnlockedMapIds;
                for (int i = 0; i < savedUnlocked.Count; i++)
                {
                    if (!string.IsNullOrEmpty(savedUnlocked[i]))
                    {
                        _sessionUnlockedIds.Add(savedUnlocked[i]);
                    }
                }
            }

            int restoredIdx = -1;
            if (_saveSystem != null && !string.IsNullOrEmpty(_saveSystem.SelectedMapId))
            {
                int saveIdx = FindIndexById(_saveSystem.SelectedMapId);
                if (saveIdx >= 0 && IsMapUnlocked(_maps[saveIdx].MapId))
                {
                    restoredIdx = saveIdx;
                }
            }

            if (restoredIdx < 0)
            {
                string prefId = PlayerPrefs.GetString(SelectedMapPrefKey, string.Empty);
                int prefIdx = FindIndexById(prefId);
                if (prefIdx >= 0 && IsMapUnlocked(_maps[prefIdx].MapId))
                {
                    restoredIdx = prefIdx;
                    _saveSystem?.SetSelectedMapId(_maps[prefIdx].MapId, saveImmediately: false);
                }
            }

            if (restoredIdx < 0)
            {
                restoredIdx = FindFirstUnlockedIndex();
            }

            _selectedIndex = restoredIdx;
        }

        private void SeedDefaultUnlockedIds()
        {
            for (int i = 0; i < _maps.Count; i++)
            {
                TrackDefinition def = _maps[i];
                if (def != null && def.UnlockedByDefault && !string.IsNullOrEmpty(def.MapId))
                {
                    _sessionUnlockedIds.Add(def.MapId);
                }
            }
        }

        private int FindFirstUnlockedIndex()
        {
            for (int i = 0; i < _maps.Count; i++)
            {
                if (_maps[i] != null && IsMapUnlocked(_maps[i].MapId))
                {
                    return i;
                }
            }
            return 0;
        }

        private int FindIndexById(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return -1;
            for (int i = 0; i < _maps.Count; i++)
            {
                if (_maps[i] != null && _maps[i].MapId == mapId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
