using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Coordinates the complete game lifecycle via the OOP State Pattern:
    /// MainMenu -> Countdown -> Racing -> Finished -> UpgradeShop.
    /// Owns WaypointTrack, Cars, Drivers, LapCheckpointManager, CarUpgradeSystem,
    /// RaceAudioController, RaceHUD, and GameMenuUI.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string SelectedLapsPrefKey = "RacingGame_SelectedLaps_v1";
        public const string SelectedDifficultyPrefKey = "RacingGame_SelectedDifficulty_v1";

        [Header("Scene References")]
        [SerializeField] private WaypointTrack track;
        [SerializeField] private Car playerCar;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private PlayerDriver playerDriver;
        [SerializeField] private RoadRashRenderer2D roadRenderer;
        [SerializeField] private LapCheckpointManager lapManager;
        [SerializeField] private RaceHUD raceHud;
        [SerializeField] private GameMenuUI menuUi;
        [SerializeField] private PauseMenuUI pauseMenuUi;
        [SerializeField] private RaceAudioController audioController;
        [SerializeField] private SkinManager skinManager;
        [SerializeField] private MapManager mapManager;
        [SerializeField] private EnvironmentDecorator environmentDecorator;
        [SerializeField] private TrackObstacleSystem obstacleSystem;
        [SerializeField] private PoliceChaseController policeController;

        [Header("Race Settings")]
        [SerializeField] private int aiOpponentCount = 4;
        [SerializeField] private int totalLaps = 3;
        [SerializeField] private float countdownDuration = 3f;
        [SerializeField] private float bikeCollisionRadius = 1.3f;
        [SerializeField, Range(0f, 1f)] private float collisionRestitution = 0.68f;
        [SerializeField, Range(0f, 1f)] private float collisionFriction = 0.28f;
        [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Normal;
        [SerializeField] private bool startInMainMenu = true;

        [Header("Data Profiles")]
        [SerializeField] private VehicleStatsAsset defaultVehicleStats;
        [SerializeField] private AIProfile easyProfile;
        [SerializeField] private AIProfile normalProfile;
        [SerializeField] private AIProfile hardProfile;

        [Header("Starting Grid")]
        [SerializeField] private float gridFirstRowOffset = 4f;
        [SerializeField] private float gridRowSpacing = 6.5f;
        [SerializeField] private float gridLaneOffset = 2.8f;

        [Header("Combat (Road Rash Kick)")]
        [SerializeField] private float kickReachForward = 3.5f;
        [SerializeField] private float kickReachLateral = 3.6f;
        [SerializeField] private float kickLateralImpulse = 14.5f;

        private readonly List<Car> _allCars = new List<Car>();
        private readonly List<AIRacerDriver> _aiDrivers = new List<AIRacerDriver>();
        private readonly Dictionary<GameFlowState, RaceStateBase> _states = new Dictionary<GameFlowState, RaceStateBase>();
        private readonly Dictionary<DifficultyLevel, AIProfile> _fallbackProfiles = new Dictionary<DifficultyLevel, AIProfile>();

        private CarUpgradeSystem _upgradeSystem;
        private RaceStateBase _currentState;
        private bool _prizeAwardedForCurrentRace;
        private int _lastPrizeEarned;
        private int _lastFinishPosition;
        private int _fallbackKickSide = 1;

        public event Action<Car, Car, Vector2> RacerKicked;
        public event Action<Car, Car, Vector2> RacersCollided;
        public event Action<Car> PlayerRecoveredToTrack;

        public IReadOnlyList<Car> AllCars => _allCars;
        public Car PlayerCar => playerCar;
        public WaypointTrack Track => track;
        public LapCheckpointManager LapManager => lapManager;
        private bool _isPaused;

        public CarUpgradeSystem UpgradeSystem => _upgradeSystem;
        public RaceAudioController AudioController => audioController;
        public SkinManager SkinManager => skinManager;
        public MapManager MapManager => mapManager;
        public EnvironmentDecorator EnvironmentDecorator => environmentDecorator;
        public TrackObstacleSystem ObstacleSystem => obstacleSystem;
        public PoliceChaseController PoliceController => policeController;
        public PauseMenuUI PauseMenuUi => pauseMenuUi;
        public bool IsPaused => _isPaused;

        public GameFlowState CurrentFlowState => _currentState != null ? _currentState.StateType : GameFlowState.MainMenu;
        public DifficultyLevel Difficulty => difficulty;
        public int TotalLaps => totalLaps;
        public int LastPrizeEarned => _lastPrizeEarned;
        public int LastFinishPosition => _lastFinishPosition;

        public float CountdownRemaining =>
            _currentState is CountdownState cs ? cs.RemainingSeconds : 0f;

        private static readonly (string name, float skillOffset, float latOffset, Color color)[] DefaultAiRacers =
        {
            ("Viper",   0.04f, -3.5f, new Color(0.20f, 0.85f, 0.35f)),
            ("Blaze",   0.01f,  3.5f, new Color(1.00f, 0.55f, 0.12f)),
            ("Shadow", -0.02f, -1.5f, new Color(0.72f, 0.32f, 0.96f)),
            ("Nova",   -0.05f,  1.8f, new Color(0.20f, 0.78f, 0.96f)),
            ("Titan",  -0.08f,  0.0f, new Color(0.95f, 0.85f, 0.22f)),
        };

        private void Awake()
        {
            EnsureStatesInitialized();
        }

        private void EnsureStatesInitialized()
        {
            if (_upgradeSystem == null)
            {
                _upgradeSystem = new CarUpgradeSystem();
                _upgradeSystem.Load();
            }

            if (_states.Count == 0)
            {
                _states[GameFlowState.MainMenu] = new MainMenuState();
                _states[GameFlowState.Countdown] = new CountdownState(countdownDuration);
                _states[GameFlowState.Racing] = new RacingState();
                _states[GameFlowState.Finished] = new FinishedState();
                _states[GameFlowState.UpgradeShop] = new UpgradeShopState();
            }
        }

        private void Start()
        {
            SetupReferences();
            totalLaps = LoadSavedTotalLaps(totalLaps);
            difficulty = LoadSavedDifficulty(difficulty);
            SpawnOrSyncOpponents();
            ApplyPlayerUpgrades();
            skinManager.ApplyToCar(playerCar);
            ApplyDifficultyToAI();

            lapManager.Initialize(track, new List<Car>(_allCars), totalLaps);
            EnsurePoliceSetup();
            ApplyActiveMapSettings();
            obstacleSystem.SpawnObstacles(track);

            BindAudioEvents();
            raceHud.Bind(this, playerCar, lapManager, track);
            menuUi.Bind(this);
            pauseMenuUi.Bind(this);

            bool shouldStartDirectly = !startInMainMenu || SkinManager.AutoStartRaceOnGameplayLoad;
            SkinManager.AutoStartRaceOnGameplayLoad = false;
            TransitionTo(shouldStartDirectly ? GameFlowState.Countdown : GameFlowState.MainMenu);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            UnbindAudioEvents();

            foreach (AIProfile fallback in _fallbackProfiles.Values)
            {
                if (fallback != null)
                {
                    Destroy(fallback);
                }
            }
            _fallbackProfiles.Clear();
        }

        private void SetupReferences()
        {
            EnsureStatesInitialized();
            track = FindOrCreate(track, "WaypointTrack");
            inputHandler = GetOrAdd(inputHandler);
            playerDriver = GetOrAdd(playerDriver);
            lapManager = GetOrAdd(lapManager);
            raceHud = GetOrAdd(raceHud);
            menuUi = GetOrAdd(menuUi);
            pauseMenuUi = GetOrAdd(pauseMenuUi);
            audioController = GetOrAdd(audioController);
            skinManager = GetOrAdd(skinManager);
            skinManager.EnsureInitialized();
            mapManager = GetOrAdd(mapManager);
            mapManager.BindSaveSystem(_upgradeSystem);
            mapManager.EnsureInitialized();
            environmentDecorator = GetOrAdd(environmentDecorator);
            obstacleSystem = GetOrAdd(obstacleSystem);
            policeController = GetOrAdd(policeController);
            ApplyActiveMapSettings();

            playerDriver.SetDriverName("Player");
            playerDriver.SetInputHandler(inputHandler);

            playerCar = FindOrCreate(playerCar, "PlayerCar");
            playerCar.ConfigureIdentity(true, RoadRashVisualTheme.PlayerJacketBlue);
            skinManager.ApplyToCar(playerCar);

            roadRenderer = FindOrCreate(roadRenderer, "RoadRashRenderer2D");
        }

        private void EnsurePoliceSetup()
        {
            if (policeController == null) policeController = GetOrAdd(policeController);
            Car copCar = policeController.EnsurePoliceUnit(playerCar, track, defaultVehicleStats);
            if (copCar != null && !_allCars.Contains(copCar))
            {
                _allCars.Add(copCar);
                if (roadRenderer != null)
                {
                    roadRenderer.SetSceneReferences(Camera.main, track, playerCar, _allCars);
                }
            }
        }

        private static T FindOrCreate<T>(T current, string objectName) where T : Component
        {
            if (current != null) return current;
            T found = FindAnyObjectByType<T>();
            if (found != null) return found;
            return new GameObject(objectName).AddComponent<T>();
        }

        private T GetOrAdd<T>(T current) where T : Component
        {
            if (current != null) return current;
            T found = FindAnyObjectByType<T>();
            return found != null ? found : gameObject.AddComponent<T>();
        }

        private void SpawnOrSyncOpponents()
        {
            _allCars.Clear();
            _aiDrivers.Clear();

            if (playerCar != null)
            {
                _allCars.Add(playerCar);
            }

            AIRacerDriver[] existingAis = FindObjectsByType<AIRacerDriver>();
            for (int i = 0; i < existingAis.Length; i++)
            {
                Car aiCar = existingAis[i].GetComponent<Car>();
                if (aiCar != null && !_allCars.Contains(aiCar))
                {
                    _allCars.Add(aiCar);
                    _aiDrivers.Add(existingAis[i]);
                }
            }

            AIProfile activeProfile = GetProfileForDifficulty(difficulty);
            for (int i = _aiDrivers.Count; i < aiOpponentCount; i++)
            {
                var racer = DefaultAiRacers[i % DefaultAiRacers.Length];
                GameObject aiObj = new GameObject($"AICar_{racer.name}");
                Car aiCar = aiObj.AddComponent<Car>();
                if (defaultVehicleStats != null)
                {
                    aiCar.SetBaseStatsAsset(defaultVehicleStats);
                }
                aiCar.ConfigureIdentity(false, racer.color);

                AIRacerDriver aiDriver = aiObj.AddComponent<AIRacerDriver>();
                aiDriver.Configure(racer.name, activeProfile, GetAiPreferredLateralOffset(i), racer.skillOffset);

                _allCars.Add(aiCar);
                _aiDrivers.Add(aiDriver);
            }

            for (int i = 0; i < _aiDrivers.Count; i++)
            {
                _aiDrivers[i].SetAllCars(_allCars);
            }

            if (roadRenderer != null)
            {
                roadRenderer.SetSceneReferences(Camera.main, track, playerCar, _allCars);
            }
        }

        public void TransitionTo(GameFlowState targetState)
        {
            EnsureStatesInitialized();
            if (!_states.TryGetValue(targetState, out RaceStateBase next)) return;

            ResumeGame();
            _currentState?.Exit(this);
            _currentState = next;
            _currentState.Enter(this);
        }

        public void PauseGame()
        {
            if (CurrentFlowState != GameFlowState.Racing && CurrentFlowState != GameFlowState.Countdown)
            {
                return;
            }

            _isPaused = true;
            Time.timeScale = 0f;
            if (pauseMenuUi == null) pauseMenuUi = GetComponent<PauseMenuUI>();
            if (pauseMenuUi != null) pauseMenuUi.RefreshState();
        }

        public void ResumeGame()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            if (pauseMenuUi == null) pauseMenuUi = GetComponent<PauseMenuUI>();
            if (pauseMenuUi != null) pauseMenuUi.RefreshState();
        }

        public void TogglePause()
        {
            if (_isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void StartRace()
        {
            ResumeGame();
            ApplyActiveMapSettings();
            ApplyPlayerUpgrades();
            skinManager?.ApplyToCar(playerCar);
            ApplyDifficultyToAI();
            lapManager.SetTotalLaps(totalLaps);
            raceHud?.RebuildMinimapTexture();
            _prizeAwardedForCurrentRace = false;
            _lastPrizeEarned = 0;
            _lastFinishPosition = 0;
            TransitionTo(GameFlowState.Countdown);
        }

        public bool SelectPlayerMap(string mapId)
        {
            EnsureStatesInitialized();
            if (mapManager == null)
            {
                mapManager = GetOrAdd(mapManager);
            }
            if (mapManager.SaveSystem != _upgradeSystem)
            {
                mapManager.BindSaveSystem(_upgradeSystem);
            }
            bool changed = mapManager.SelectMap(mapId, track, environmentDecorator);
            if (changed)
            {
                ApplyActiveMapSettings();
                ApplyDifficultyToAI();
                raceHud?.RebuildMinimapTexture();
                if (audioController != null) audioController.PlayUpgradeResult(true);
            }
            return changed;
        }

        public TrackDefinition StepPlayerMap(int delta)
        {
            EnsureStatesInitialized();
            if (mapManager == null)
            {
                mapManager = GetOrAdd(mapManager);
            }
            if (mapManager.SaveSystem != _upgradeSystem)
            {
                mapManager.BindSaveSystem(_upgradeSystem);
            }
            TrackDefinition def = mapManager.StepMapSelection(delta, track, environmentDecorator);
            if (def != null)
            {
                ApplyActiveMapSettings();
                ApplyDifficultyToAI();
                raceHud?.RebuildMinimapTexture();
                if (audioController != null) audioController.PlayUpgradeResult(true);
            }
            return def;
        }

        public bool TryUnlockPlayerMap(string mapId)
        {
            EnsureStatesInitialized();
            if (mapManager == null)
            {
                mapManager = GetOrAdd(mapManager);
            }
            if (mapManager.SaveSystem != _upgradeSystem)
            {
                mapManager.BindSaveSystem(_upgradeSystem);
            }

            bool unlocked = mapManager.TryPurchaseMapUnlock(mapId);
            if (unlocked)
            {
                mapManager.SelectMap(mapId, track, environmentDecorator);
                ApplyActiveMapSettings();
                ApplyDifficultyToAI();
                raceHud?.RebuildMinimapTexture();
            }
            if (audioController != null) audioController.PlayUpgradeResult(unlocked);
            return unlocked;
        }

        private void ApplyActiveMapSettings()
        {
            TrackDefinition activeMap = mapManager != null ? mapManager.SelectedMap : null;
            if (activeMap == null) return;

            if (track != null) track.ApplyTrackDefinition(activeMap);
            if (obstacleSystem != null) obstacleSystem.ApplyMapSettings(activeMap);
            if (policeController != null) policeController.ApplyMapSettings(activeMap);
            if (environmentDecorator != null && track != null)
            {
                environmentDecorator.RebuildEnvironment(track, activeMap.Theme);
            }
        }

        public bool SelectPlayerSkin(DriverBrandSkin brand)
        {
            if (skinManager == null)
            {
                skinManager = GetOrAdd(skinManager);
            }
            bool changed = skinManager.SelectSkin(brand, playerCar);
            if (changed && audioController != null)
            {
                audioController.PlayUpgradeResult(true);
            }
            return changed;
        }

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void OpenGarage()
        {
            TransitionTo(GameFlowState.UpgradeShop);
        }

        public void OpenMenu()
        {
            TransitionTo(GameFlowState.MainMenu);
        }

        public static int LoadSavedTotalLaps(int fallback = 3)
        {
            int fallbackClamped = Mathf.Clamp(fallback, 1, 10);
            return Mathf.Clamp(PlayerPrefs.GetInt(SelectedLapsPrefKey, fallbackClamped), 1, 10);
        }

        public static void SaveTotalLapsPreference(int laps)
        {
            PlayerPrefs.SetInt(SelectedLapsPrefKey, Mathf.Clamp(laps, 1, 10));
            PlayerPrefs.Save();
        }

        public static DifficultyLevel LoadSavedDifficulty(DifficultyLevel fallback = DifficultyLevel.Normal)
        {
            int raw = PlayerPrefs.GetInt(SelectedDifficultyPrefKey, (int)fallback);
            return Enum.IsDefined(typeof(DifficultyLevel), raw) ? (DifficultyLevel)raw : fallback;
        }

        public static void SaveDifficultyPreference(DifficultyLevel level)
        {
            PlayerPrefs.SetInt(SelectedDifficultyPrefKey, (int)level);
            PlayerPrefs.Save();
        }

        public void SetDifficulty(DifficultyLevel newDifficulty)
        {
            difficulty = newDifficulty;
            SaveDifficultyPreference(difficulty);
            ApplyDifficultyToAI();
        }

        public void SetTotalLaps(int laps)
        {
            totalLaps = Mathf.Clamp(laps, 1, 10);
            SaveTotalLapsPreference(totalLaps);
            if (lapManager != null)
            {
                lapManager.SetTotalLaps(totalLaps);
            }
        }

        public bool TryBuyUpgrade(UpgradeType type)
        {
            if (_upgradeSystem == null) return false;
            bool bought = _upgradeSystem.BuyUpgrade(type);
            if (bought)
            {
                ApplyPlayerUpgrades();
            }
            if (audioController != null)
            {
                audioController.PlayUpgradeResult(bought);
            }
            return bought;
        }

        public void ResetSaveData()
        {
            if (_upgradeSystem != null)
            {
                _upgradeSystem.ResetProgress();
            }
            if (mapManager != null)
            {
                mapManager.ResetProgress();
                ApplyActiveMapSettings();
                ApplyDifficultyToAI();
                raceHud?.RebuildMinimapTexture();
            }
            ApplyPlayerUpgrades();
        }

        public void ApplyPlayerUpgrades()
        {
            if (_upgradeSystem != null && playerCar != null)
            {
                if (playerCar.BaseStatsAsset == null && defaultVehicleStats != null)
                {
                    playerCar.SetBaseStatsAsset(defaultVehicleStats);
                }
                _upgradeSystem.SetBaseStats(playerCar.BaseStats);
                _upgradeSystem.ApplyToCar(playerCar.Physics);
            }
        }

        public void ResetRaceGrid()
        {
            if (track == null || track.WaypointCount < 2) return;

            Vector2 origin = track.GetWaypoint(0);
            Vector2 dir = track.GetTrackDirection(0);
            Vector2 right = new Vector2(dir.y, -dir.x);
            float heading = Mathf.Atan2(dir.y, dir.x);

            int aiCount = 0;
            for (int i = 0; i < _aiDrivers.Count; i++)
            {
                if (_aiDrivers[i] != null && _aiDrivers[i].GetComponent<Car>() != null)
                {
                    aiCount++;
                }
            }

            int aiIdx = 0;
            for (int i = 0; i < _aiDrivers.Count; i++)
            {
                Car aiCar = _aiDrivers[i] != null ? _aiDrivers[i].GetComponent<Car>() : null;
                if (aiCar == null) continue;
                float lat = GetAbreastGridLateralOffset(track, aiIdx++, aiCount);
                Vector2 aiPos = origin - dir * gridFirstRowOffset + right * lat;
                aiCar.Initialize(_aiDrivers[i], aiPos, heading, track);
            }

            if (playerCar != null)
            {
                Vector2 playerPos = origin - dir * (gridFirstRowOffset + gridRowSpacing);
                playerCar.Initialize(playerDriver, playerPos, heading, track);
            }

            EnsurePoliceSetup();
            if (policeController != null)
            {
                policeController.ResetPoliceOnGrid(playerCar, track);
            }

            if (environmentDecorator != null)
            {
                environmentDecorator.RebuildEnvironment(track);
            }

            if (obstacleSystem != null)
            {
                obstacleSystem.SpawnObstacles(track);
            }

            if (lapManager != null) lapManager.ResetRace();
            if (raceHud != null) raceHud.ClearMessage();
        }

        /// <summary>
        /// Immediately repositions the player back onto the nearest road center when requested manually ([F] / [Backspace]).
        /// </summary>
        public bool RecoverPlayerToTrack(bool automatic = false)
        {
            if (playerCar == null || track == null || track.WaypointCount < 2)
            {
                return false;
            }

            TrackProjection proj = track.ProjectPosition(playerCar.Physics.Position);
            float trackHeading = Mathf.Atan2(proj.tangent.y, proj.tangent.x);
            playerCar.ResetToTrackPoint(proj.closestPoint, trackHeading, track);
            playerCar.Physics.ApplyVelocityImpulse(proj.tangent * 14f);

            PlayerRecoveredToTrack?.Invoke(playerCar);
            return true;
        }

        public void StepRaceSimulation(float dt)
        {
            if (track == null || _isPaused) return;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car car = _allCars[i];
                if (car == null) continue;

                car.StepPhysics(dt, track);
                if (car.LastInput.kick && car.CanKick)
                {
                    int kickSide = ChooseKickDirection(car);
                    if (car.TryStartKick(kickSide) && car == playerCar && audioController != null)
                    {
                        audioController.PlayKick(false);
                    }
                }
            }

            ResolveKickAttacks();
            ResolveVehicleCollisions();

            if (obstacleSystem != null)
            {
                obstacleSystem.StepObstacles(track, _allCars, dt);
            }

            if (policeController != null)
            {
                policeController.TickPursuit(dt);
            }

            if (lapManager != null)
            {
                lapManager.Tick(dt);
            }
        }

        public bool IsPlayerFinished()
        {
            if (lapManager == null || playerCar == null) return false;
            RacerProgress p = lapManager.GetProgress(playerCar);
            return p != null && p.IsFinished;
        }

        public void RecordPlayerRaceFinish()
        {
            if (_prizeAwardedForCurrentRace || lapManager == null || playerCar == null) return;

            RacerProgress p = lapManager.GetProgress(playerCar);
            if (p == null) return;

            _prizeAwardedForCurrentRace = true;
            _lastFinishPosition = p.IsFinished ? p.FinishPosition : lapManager.GetPosition(playerCar);
            TrackDefinition activeMap = mapManager != null ? mapManager.SelectedMap : null;
            float rewardMult = activeMap != null ? activeMap.RewardMultiplier : 1.0f;
            _lastPrizeEarned = _upgradeSystem != null
                ? _upgradeSystem.AwardRaceResult(_lastFinishPosition, p.FinishTime, rewardMult)
                : 0;
            if (activeMap != null && p.IsFinished)
            {
                mapManager.RecordBestTime(activeMap.MapId, p.FinishTime);
            }
        }

        public void SetEngineSoundActive(bool active)
        {
            if (audioController != null)
            {
                audioController.SetEngineActive(active);
            }
        }

        public void PlayCountdownBeep(bool isGo)
        {
            if (audioController != null)
            {
                audioController.PlayCountdown(isGo);
            }
        }

        private AIProfile GetProfileForDifficulty(DifficultyLevel level)
        {
            AIProfile assigned = level switch
            {
                DifficultyLevel.Easy => easyProfile,
                DifficultyLevel.Hard => hardProfile,
                _ => normalProfile
            };

            if (assigned != null)
            {
                return assigned;
            }

            if (!_fallbackProfiles.TryGetValue(level, out AIProfile fallback) || fallback == null)
            {
                fallback = AIProfile.CreateDefault(level);
                _fallbackProfiles[level] = fallback;
            }

            return fallback;
        }

        private void ApplyDifficultyToAI()
        {
            AIProfile activeProfile = GetProfileForDifficulty(difficulty);
            TrackDefinition activeMap = mapManager != null ? mapManager.SelectedMap : null;
            float cornerMult = activeMap != null ? activeMap.AiCornerSpeedMultiplier : 1f;

            for (int i = 0; i < _aiDrivers.Count; i++)
            {
                var racer = DefaultAiRacers[i % DefaultAiRacers.Length];
                float laneOffset = GetAiPreferredLateralOffset(i);
                _aiDrivers[i].Configure(racer.name, activeProfile, laneOffset, racer.skillOffset);
                _aiDrivers[i].SetMapCornerSpeedMultiplier(cornerMult);
            }
        }

        public float GetAiPreferredLateralOffset(int racerIndex)
        {
            return GetAiPreferredLateralOffset(track, racerIndex);
        }

        public static float GetAiPreferredLateralOffset(WaypointTrack targetTrack, int racerIndex)
        {
            int safeIndex = Mathf.Max(0, racerIndex);
            if (targetTrack == null)
            {
                return DefaultAiRacers[safeIndex % DefaultAiRacers.Length].latOffset;
            }

            int lanes = targetTrack.LaneCount;
            int halfPairs = (lanes + 1) / 2;
            int pairStep = (safeIndex / 2) % halfPairs;
            int laneIdx = (safeIndex % 2 == 0)
                ? pairStep
                : (lanes - 1) - pairStep;
            return targetTrack.GetLaneCenterOffset(laneIdx);
        }

        public float GetGridSlotLateralOffset(int slot)
        {
            return GetGridSlotLateralOffset(track, slot, gridLaneOffset);
        }

        public static float GetGridSlotLateralOffset(WaypointTrack targetTrack, int slot, float fallbackLaneOffset = 2.8f)
        {
            if (targetTrack == null)
            {
                return (slot % 2 == 0) ? -fallbackLaneOffset : fallbackLaneOffset;
            }

            int lanes = targetTrack.LaneCount;
            int leftLane;
            int rightLane;
            if (lanes % 2 == 0)
            {
                leftLane = (lanes / 2) - 1;
                rightLane = lanes / 2;
            }
            else
            {
                int center = lanes / 2;
                leftLane = Mathf.Max(0, center - 1);
                rightLane = Mathf.Min(lanes - 1, center + 1);
            }

            int laneIdx = (slot % 2 == 0) ? leftLane : rightLane;
            return targetTrack.GetLaneCenterOffset(laneIdx);
        }

        public static float GetAbreastGridLateralOffset(WaypointTrack targetTrack, int opponentIndex, int totalOpponents)
        {
            if (totalOpponents <= 1)
            {
                return 0f;
            }

            int safeIdx = Mathf.Clamp(opponentIndex, 0, totalOpponents - 1);
            if (targetTrack == null)
            {
                float span = Mathf.Min(18f, (totalOpponents - 1) * 4.5f);
                float tNull = safeIdx / (float)(totalOpponents - 1);
                return Mathf.Lerp(-span * 0.5f, span * 0.5f, tNull);
            }

            if (totalOpponents <= targetTrack.LaneCount)
            {
                return GetAiPreferredLateralOffset(targetTrack, safeIdx);
            }

            float usableHalf = Mathf.Max(
                targetTrack.MaxLaneCenterOffset,
                Mathf.Max(0.5f, targetTrack.TrackWidth * 0.5f - 0.65f));
            float t = safeIdx / (float)(totalOpponents - 1);
            return Mathf.Lerp(-usableHalf, usableHalf, t);
        }

        private void Update()
        {
            if (CurrentFlowState == GameFlowState.Countdown)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
                {
                    TogglePause();
                    return;
                }
            }

            if (_isPaused)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
                {
                    ResumeGame();
                }
                return;
            }

            _currentState?.Update(this, Time.deltaTime);

            if (audioController != null && playerCar != null &&
                (CurrentFlowState == GameFlowState.Racing || CurrentFlowState == GameFlowState.Countdown))
            {
                float speedNorm = playerCar.Physics.Speed / Mathf.Max(1f, playerCar.Physics.MaxSpeed);
                audioController.UpdateEnginePitch(speedNorm, playerCar.LastInput.throttle);
            }
        }

        private void FixedUpdate()
        {
            if (_isPaused) return;
            _currentState?.FixedUpdate(this, Time.fixedDeltaTime);
        }

        private void ResolveVehicleCollisions()
        {
            for (int i = 0; i < _allCars.Count; i++)
            {
                Car a = _allCars[i];
                if (a == null) continue;

                for (int j = i + 1; j < _allCars.Count; j++)
                {
                    Car b = _allCars[j];
                    if (b == null) continue;

                    VehicleCollisionResult res = VehicleCollisionPhysics.Resolve(
                        a.Physics.Position, a.Physics.Velocity, a.Physics.Mass, bikeCollisionRadius,
                        b.Physics.Position, b.Physics.Velocity, b.Physics.Mass, bikeCollisionRadius,
                        collisionRestitution, collisionFriction);

                    if (!res.Collided) continue;

                    a.ApplyPhysicalCollisionImpact(
                        res.PositionDeltaA, res.VelocityDeltaA, res.AngularVelocityDeltaA, res.VerticalLaunchVelA);
                    b.ApplyPhysicalCollisionImpact(
                        res.PositionDeltaB, res.VelocityDeltaB, res.AngularVelocityDeltaB, res.VerticalLaunchVelB);

                    if (res.NormalImpulse > 120f)
                    {
                        RacersCollided?.Invoke(a, b, res.ContactPoint);

                        if ((a == playerCar || b == playerCar) && audioController != null)
                        {
                            audioController.PlayCollision();
                        }
                    }
                }
            }
        }

        private int ChooseKickDirection(Car attacker)
        {
            return FindBestKickDirection(attacker, _allCars, kickReachForward, kickReachLateral, ref _fallbackKickSide);
        }

        private void ResolveKickAttacks()
        {
            for (int i = 0; i < _allCars.Count; i++)
            {
                Car attacker = _allCars[i];
                Car hitTarget = TryResolveKickHit(attacker, _allCars, kickReachForward, kickReachLateral, kickLateralImpulse, out Vector2 hitPos);
                if (hitTarget != null)
                {
                    RacerKicked?.Invoke(attacker, hitTarget, hitPos);

                    if ((attacker == playerCar || hitTarget == playerCar) && audioController != null)
                    {
                        audioController.PlayKick(true);
                    }
                }
            }
        }

        public static int FindBestKickDirection(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            ref int fallbackSide)
        {
            Vector2 fwd = attacker.Physics.Forward;
            Vector2 right = attacker.Physics.Right;
            float bestDistSq = float.MaxValue;
            int bestSide = 0;

            for (int i = 0; i < allCars.Count; i++)
            {
                Car target = allCars[i];
                if (target == null || target == attacker) continue;

                Vector2 rel = target.Physics.Position - attacker.Physics.Position;
                float localFwd = Vector2.Dot(rel, fwd);
                float localLat = Vector2.Dot(rel, right);

                if (Mathf.Abs(localFwd) <= forwardReach + 0.8f && Mathf.Abs(localLat) <= lateralReach + 1.0f)
                {
                    float d2 = rel.sqrMagnitude;
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        bestSide = localLat >= 0f ? 1 : -1;
                    }
                }
            }

            if (bestSide != 0) return bestSide;
            if (Mathf.Abs(attacker.LastInput.steering) > 0.15f)
            {
                return attacker.LastInput.steering > 0f ? 1 : -1;
            }

            fallbackSide = -fallbackSide;
            return fallbackSide;
        }

        public static Car TryResolveKickHit(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            float lateralImpulse,
            out Vector2 hitPos)
        {
            hitPos = Vector2.zero;
            if (attacker == null || !attacker.IsKicking || attacker.HasKickHitThisSwing) return null;
            if (attacker.KickProgress < 0.16f || attacker.KickProgress > 0.88f) return null;

            Vector2 fwd = attacker.Physics.Forward;
            Vector2 right = attacker.Physics.Right;
            Car bestTarget = null;
            float bestDistSq = float.MaxValue;

            for (int j = 0; j < allCars.Count; j++)
            {
                Car target = allCars[j];
                if (target == null || target == attacker) continue;

                Vector2 rel = target.Physics.Position - attacker.Physics.Position;
                float localFwd = Vector2.Dot(rel, fwd);
                float sideLat = Vector2.Dot(rel, right) * attacker.KickDirection;

                if (Mathf.Abs(localFwd) <= forwardReach && sideLat >= -0.45f && sideLat <= lateralReach)
                {
                    float d2 = rel.sqrMagnitude;
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        bestTarget = target;
                    }
                }
            }

            if (bestTarget == null) return null;

            Vector2 impulse = right * (attacker.KickDirection * lateralImpulse);
            bestTarget.ReceiveKick(attacker.KickDirection, impulse, 0.45f);
            attacker.ApplyCollisionPush(Vector2.zero, -right * (attacker.KickDirection * 2.4f));
            attacker.MarkKickHit();

            hitPos = (attacker.Physics.Position + bestTarget.Physics.Position) * 0.5f;
            return bestTarget;
        }

        private void BindAudioEvents()
        {
            if (lapManager == null) return;
            lapManager.CheckpointPassed += OnCheckpointPassedAudio;
            lapManager.LapCompleted += OnLapCompletedAudio;
            lapManager.RacerFinished += OnRacerFinishedAudio;
        }

        private void UnbindAudioEvents()
        {
            if (lapManager == null) return;
            lapManager.CheckpointPassed -= OnCheckpointPassedAudio;
            lapManager.LapCompleted -= OnLapCompletedAudio;
            lapManager.RacerFinished -= OnRacerFinishedAudio;
        }

        private void OnCheckpointPassedAudio(RacerProgress progress, Checkpoint cp)
        {
            if (progress.Car == playerCar && !cp.IsFinishLine && audioController != null)
            {
                audioController.PlayCheckpoint();
            }
        }

        private void OnLapCompletedAudio(RacerProgress progress)
        {
            if (progress.Car == playerCar && !progress.IsFinished && audioController != null)
            {
                audioController.PlayLapCompleted();
            }
        }

        private void OnRacerFinishedAudio(RacerProgress progress)
        {
            if (progress.Car == playerCar && audioController != null)
            {
                audioController.PlayRaceFinished();
            }
        }
    }
}
