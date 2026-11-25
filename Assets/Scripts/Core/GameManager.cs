using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Composition Root & State Context coordinating the complete game lifecycle via the OOP State Pattern:
    /// MainMenu -> Countdown -> Racing -> Finished -> UpgradeShop.
    /// Delegates combat/collisions to IRaceCombatService, input to IInputProvider,
    /// audio to IAudioService, and persistence to ProgressionRuleEngine / MidRaceSaveService.
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

        private ISaveStorage _storage;
        private IJsonCodec _jsonCodec;
        private IInputProvider _inputProviderOverride;
        private IAudioService _audioServiceOverride;
        private IRaceCombatService _combatService;
        private CarUpgradeSystem _upgradeSystem;
        private MidRaceSaveService _midRaceSaveService;
        private GameBalanceConfig _balanceConfig;

        private RaceStateBase _currentState;
        private bool _prizeAwardedForCurrentRace;
        private int _lastPrizeEarned;
        private int _lastFinishPosition;
        private int _fallbackKickSide = 1;
        private bool _isPaused;
        private bool _skipNextGridResetOnEnter;
        private int _policeEngagementCount;

        public event Action<Car, Car, Vector2> RacerKicked;
        public event Action<Car, Car, Vector2> RacersCollided;
        public event Action<Car> PlayerRecoveredToTrack { add { } remove { } }

        public IReadOnlyList<Car> AllCars => _allCars;
        public Car PlayerCar => playerCar;
        public WaypointTrack Track => track;
        public LapCheckpointManager LapManager => lapManager;
        public CarUpgradeSystem UpgradeSystem => _upgradeSystem;
        public MidRaceSaveService MidRaceSaveService => EnsureMidRaceSaveService();
        public RaceAudioController AudioController => audioController;
        public IAudioService AudioService => _audioServiceOverride != null ? _audioServiceOverride : audioController;
        public IInputProvider InputProvider => _inputProviderOverride != null ? _inputProviderOverride : inputHandler;
        public IRaceCombatService CombatService => _combatService ?? (_combatService = new RaceCombatSystem());
        public GameBalanceConfig BalanceConfig => _balanceConfig ?? (_balanceConfig = GameBalanceConfig.CreateDefault());
        public SkinManager SkinManager => skinManager;
        public MapManager MapManager => mapManager;
        public EnvironmentDecorator EnvironmentDecorator => environmentDecorator;
        public TrackObstacleSystem ObstacleSystem => obstacleSystem;
        public PoliceChaseController PoliceController => policeController;
        public PauseMenuUI PauseMenuUi => pauseMenuUi;
        public bool IsPaused => _isPaused;
        public int PoliceEngagementCount => _policeEngagementCount;

        public bool HasSavedMidRace => EnsureMidRaceSaveService().HasSavedRace();

        public GameFlowState CurrentFlowState => _currentState != null ? _currentState.StateType : GameFlowState.MainMenu;
        public DifficultyLevel Difficulty => difficulty;
        public int TotalLaps => totalLaps;
        public int LastPrizeEarned => _lastPrizeEarned;
        public int LastFinishPosition => _lastFinishPosition;

        public float CountdownRemaining => _currentState != null ? _currentState.RemainingCountdown : 0f;

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

        /// <summary>
        /// Composition Root Dependency Injection hook allowing tests or alternative hosts to swap
        /// Storage, JSON codec, Input provider, Audio service, Combat solver, or Mid-Race persistence.
        /// </summary>
        public void ConfigureServices(
            ISaveStorage storage = null,
            IJsonCodec jsonCodec = null,
            IInputProvider inputProvider = null,
            IAudioService audioService = null,
            IRaceCombatService combatService = null,
            CarUpgradeSystem upgradeSystem = null,
            MidRaceSaveService midRaceSaveService = null,
            GameBalanceConfig balanceConfig = null)
        {
            if (storage != null) _storage = storage;
            if (jsonCodec != null) _jsonCodec = jsonCodec;
            if (inputProvider != null)
            {
                _inputProviderOverride = inputProvider;
                if (playerDriver != null) playerDriver.SetInputProvider(inputProvider);
            }
            if (audioService != null) _audioServiceOverride = audioService;
            if (combatService != null) _combatService = combatService;
            if (balanceConfig != null) _balanceConfig = balanceConfig.Sanitize();

            if (upgradeSystem != null)
            {
                _upgradeSystem = upgradeSystem;
                if (mapManager != null) mapManager.BindSaveSystem(_upgradeSystem);
            }
            if (midRaceSaveService != null)
            {
                _midRaceSaveService = midRaceSaveService;
            }
        }

        private MidRaceSaveService EnsureMidRaceSaveService()
        {
            _storage ??= new FileSaveStorageAdapter();
            _jsonCodec ??= new UnityJsonCodec();
            if (_midRaceSaveService == null)
            {
                string midPath = Path.Combine(Application.persistentDataPath, "midrace_save.json");
                _midRaceSaveService = new MidRaceSaveService(_storage, _jsonCodec, midPath, Debug.LogWarning);
            }
            return _midRaceSaveService;
        }

        private void EnsureStatesInitialized()
        {
            _storage ??= new FileSaveStorageAdapter();
            _jsonCodec ??= new UnityJsonCodec();
            _combatService ??= new RaceCombatSystem();

            if (_balanceConfig == null)
            {
                var loader = new ExternalBalanceConfigLoader(_storage, _jsonCodec);
                _balanceConfig = loader.LoadOrDefault();
            }

            if (_upgradeSystem == null)
            {
                VehicleStats baseline = defaultVehicleStats != null
                    ? defaultVehicleStats.Stats
                    : VehicleStats.FromPure(_balanceConfig.defaultVehicleStats);
                string savePath = Path.Combine(Application.persistentDataPath, "racing2d_save.json");
                _upgradeSystem = new CarUpgradeSystem(_storage, _jsonCodec, savePath, baseline, _balanceConfig);
                _upgradeSystem.Load();
            }

            EnsureMidRaceSaveService();

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
            if (skinManager != null) skinManager.ApplyToCar(playerCar);
            ApplyDifficultyToAI();

            lapManager.Initialize(track, new List<Car>(_allCars), totalLaps);
            EnsurePoliceSetup();
            ApplyActiveMapSettings();
            obstacleSystem.SpawnObstacles(track);

            BindSystemEvents();
            if (raceHud != null) raceHud.Bind(this, playerCar, lapManager, track);
            if (menuUi != null) menuUi.Bind(this);
            if (pauseMenuUi != null) pauseMenuUi.Bind(this);

            bool shouldResumeSaved = SkinManager.AutoResumeSavedRaceOnGameplayLoad;
            bool shouldStartDirectly = !startInMainMenu || SkinManager.AutoStartRaceOnGameplayLoad;
            SkinManager.AutoResumeSavedRaceOnGameplayLoad = false;
            SkinManager.AutoStartRaceOnGameplayLoad = false;

            if (shouldResumeSaved && LoadMidRaceState())
            {
                return;
            }

            TransitionTo(shouldStartDirectly ? GameFlowState.Countdown : GameFlowState.MainMenu);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            UnbindSystemEvents();

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
            if (_inputProviderOverride != null)
            {
                playerDriver.SetInputProvider(_inputProviderOverride);
            }
            else
            {
                playerDriver.SetInputHandler(inputHandler);
            }

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
                if (aiCar != null && aiCar != playerCar && !_allCars.Contains(aiCar))
                {
                    if (aiCar.IsPlayer)
                    {
                        aiCar.ConfigureIdentity(false, aiCar.BikeColor);
                    }
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

        public bool ConsumeSkipNextGridResetOnEnter()
        {
            if (!_skipNextGridResetOnEnter) return false;
            _skipNextGridResetOnEnter = false;
            return true;
        }

        public void TransitionTo(GameFlowState targetState)
        {
            EnsureStatesInitialized();
            if (!_states.TryGetValue(targetState, out RaceStateBase next)) return;

            ResumeGame();
            if (_currentState != null) _currentState.Exit(this);
            _currentState = next;
            _currentState.Enter(this);
        }

        public void TransitionToStrict(GameFlowState targetState)
        {
            RaceStateTransitionRules.ValidateTransitionOrThrow(CurrentFlowState, targetState);
            TransitionTo(targetState);
        }

        public void PauseGame()
        {
            if (!RaceStateTransitionRules.CanPause(CurrentFlowState))
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
            if (skinManager != null) skinManager.ApplyToCar(playerCar);
            ApplyDifficultyToAI();
            if (lapManager != null) lapManager.SetTotalLaps(totalLaps);
            if (raceHud != null) raceHud.RebuildMinimapTexture();
            _prizeAwardedForCurrentRace = false;
            _lastPrizeEarned = 0;
            _lastFinishPosition = 0;
            TransitionTo(GameFlowState.Countdown);
        }

        /// <summary>
        /// Captures complete mid-race state (map, lap settings, race timer, vehicle positions/velocities,
        /// and checkpoint progress) and persists it to JSON via MidRaceSaveService.
        /// </summary>
        public bool SaveMidRaceState()
        {
            EnsureStatesInitialized();
            if (track == null || _allCars.Count == 0) return false;

            var snapshot = new MidRaceSaveData
            {
                mapId = mapManager != null && !string.IsNullOrEmpty(mapManager.SelectedMapId)
                    ? mapManager.SelectedMapId
                    : (track.ActiveDefinition != null ? track.ActiveDefinition.MapId : "city_morning"),
                totalLaps = Mathf.Max(1, totalLaps),
                difficulty = (int)difficulty,
                raceTime = lapManager != null ? lapManager.RaceTime : 0f,
                finishedCount = 0
            };

            Car copCar = policeController != null ? policeController.PoliceCar : null;
            int finished = 0;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car c = _allCars[i];
                if (c == null) continue;

                RacerProgress prog = lapManager != null ? lapManager.GetProgress(c) : null;
                if (prog != null && prog.IsFinished)
                {
                    finished++;
                }

                snapshot.racers.Add(new RacerMidRaceSnapshot
                {
                    racerName = c.RacerName,
                    isPlayer = c == playerCar,
                    isPolice = copCar != null && c == copCar,
                    posX = c.Physics.Position.x,
                    posY = c.Physics.Position.y,
                    velX = c.Physics.Velocity.x,
                    velY = c.Physics.Velocity.y,
                    headingRad = c.Physics.Heading,
                    completedLaps = prog != null ? prog.CompletedLaps : 0,
                    nextCheckpointIndex = prog != null ? prog.NextCheckpointIndex : 1,
                    totalCheckpointsPassed = prog != null ? prog.TotalCheckpointsPassed : 0,
                    isFinished = prog != null && prog.IsFinished,
                    finishPosition = prog != null ? prog.FinishPosition : 0,
                    finishTime = prog != null ? prog.FinishTime : 0f,
                    lastLapTime = prog != null ? prog.LastLapTime : 0f,
                    bestLapTime = prog != null ? prog.BestLapTime : 0f
                });
            }

            snapshot.finishedCount = finished;
            _midRaceSaveService.SaveSnapshot(snapshot);
            return true;
        }

        /// <summary>
        /// Restores an active race from midrace_save.json (map, lap progress, vehicle positions, velocities,
        /// checkpoints, and race timer) and resumes directly in Racing state.
        /// </summary>
        public bool LoadMidRaceState()
        {
            EnsureStatesInitialized();
            if (!_midRaceSaveService.TryLoadSnapshot(out MidRaceSaveData snapshot))
            {
                return false;
            }

            if (track == null || _allCars.Count == 0)
            {
                SetupReferences();
                SpawnOrSyncOpponents();
            }

            if (mapManager != null && !string.IsNullOrEmpty(snapshot.mapId))
            {
                mapManager.SelectMap(snapshot.mapId, track, environmentDecorator);
            }

            totalLaps = Mathf.Clamp(snapshot.totalLaps, 1, 10);
            if (Enum.IsDefined(typeof(DifficultyLevel), snapshot.difficulty))
            {
                difficulty = (DifficultyLevel)snapshot.difficulty;
            }

            ApplyActiveMapSettings();
            ApplyPlayerUpgrades();
            if (skinManager != null) skinManager.ApplyToCar(playerCar);
            ApplyDifficultyToAI();
            EnsurePoliceSetup();

            if (lapManager != null)
            {
                lapManager.Initialize(track, new List<Car>(_allCars), totalLaps);
            }

            Car copCar = policeController != null ? policeController.PoliceCar : null;
            for (int i = 0; i < snapshot.racers.Count; i++)
            {
                RacerMidRaceSnapshot rSnap = snapshot.racers[i];
                if (rSnap == null) continue;

                Car targetCar = FindCarForSnapshot(rSnap, copCar, i);
                if (targetCar == null) continue;

                targetCar.RestoreRuntimeState(
                    new Vector2(rSnap.posX, rSnap.posY),
                    new Vector2(rSnap.velX, rSnap.velY),
                    rSnap.headingRad,
                    track);
            }

            if (lapManager != null)
            {
                lapManager.RestoreRaceSnapshot(snapshot.raceTime, snapshot.finishedCount, snapshot.racers);
            }

            if (obstacleSystem != null && track != null)
            {
                obstacleSystem.SpawnObstacles(track);
            }

            if (raceHud != null)
            {
                raceHud.RebuildMinimapTexture();
                raceHud.ClearMessage();
            }

            _prizeAwardedForCurrentRace = false;
            _lastPrizeEarned = 0;
            _lastFinishPosition = 0;

            TransitionTo(GameFlowState.Racing);
            return true;
        }

        private Car FindCarForSnapshot(RacerMidRaceSnapshot snap, Car copCar, int fallbackIndex)
        {
            if (snap.isPlayer && playerCar != null) return playerCar;
            if (snap.isPolice && copCar != null) return copCar;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car c = _allCars[i];
                if (c != null && !c.IsPlayer && c != copCar && string.Equals(c.RacerName, snap.racerName, StringComparison.Ordinal))
                {
                    return c;
                }
            }

            return fallbackIndex >= 0 && fallbackIndex < _allCars.Count ? _allCars[fallbackIndex] : null;
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
                if (raceHud != null) raceHud.RebuildMinimapTexture();
                if (AudioService != null) AudioService.PlayUpgradeResult(true);
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
                if (raceHud != null) raceHud.RebuildMinimapTexture();
                if (AudioService != null) AudioService.PlayUpgradeResult(true);
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
                if (raceHud != null) raceHud.RebuildMinimapTexture();
            }
            if (AudioService != null) AudioService.PlayUpgradeResult(unlocked);
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
            if (changed && AudioService != null)
            {
                AudioService.PlayUpgradeResult(true);
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
            if (AudioService != null)
            {
                AudioService.PlayUpgradeResult(bought);
            }
            return bought;
        }

        public void ResetSaveData()
        {
            if (_upgradeSystem != null)
            {
                _upgradeSystem.ResetProgress();
            }
            EnsureMidRaceSaveService().ClearSavedRace();
            if (mapManager != null)
            {
                mapManager.ResetProgress();
                ApplyActiveMapSettings();
                ApplyDifficultyToAI();
                if (raceHud != null) raceHud.RebuildMinimapTexture();
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
                VehicleStats baseStats = playerCar.BaseStatsAsset != null
                    ? playerCar.BaseStatsAsset.Stats
                    : (_balanceConfig != null ? VehicleStats.FromPure(_balanceConfig.defaultVehicleStats) : playerCar.BaseStats);
                _upgradeSystem.SetBaseStats(baseStats);
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
        /// Disabled by design: player vehicles must never be automatically or forcibly teleported back onto the track.
        /// </summary>
        public bool RecoverPlayerToTrack(bool automatic = false)
        {
            return false;
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
                    if (car.TryStartKick(kickSide) && car == playerCar && AudioService != null)
                    {
                        AudioService.PlayKick(false);
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

            string mapId = activeMap != null ? activeMap.MapId : "city_morning";
            if (_upgradeSystem != null && p.IsFinished && p.FinishTime > 0f)
            {
                _upgradeSystem.RecordHighScore(mapId, playerCar.RacerName, p.FinishTime, totalLaps, difficulty);
            }
            if (activeMap != null && p.IsFinished)
            {
                mapManager.RecordBestTime(activeMap.MapId, p.FinishTime);
            }
            EnsureMidRaceSaveService().ClearSavedRace();
        }

        public void SetEngineSoundActive(bool active)
        {
            if (AudioService != null)
            {
                AudioService.SetEngineActive(active);
            }
        }

        public void SetBgmActive(bool active)
        {
            if (AudioService != null)
            {
                AudioService.SetBgmActive(active);
            }
        }

        public void PlayCountdownBeep(bool isGo)
        {
            if (AudioService != null)
            {
                AudioService.PlayCountdown(isGo);
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
            IInputProvider input = InputProvider;
            if (input != null && input.WasMutePressed() && AudioService != null)
            {
                AudioService.ToggleMute();
            }

            if (_isPaused)
            {
                if (input != null && input.WasPausePressed())
                {
                    ResumeGame();
                }
                return;
            }

            if (_currentState != null)
            {
                _currentState.Update(this, Time.deltaTime);
            }

            if (AudioService != null && playerCar != null &&
                (CurrentFlowState == GameFlowState.Racing || CurrentFlowState == GameFlowState.Countdown))
            {
                float speedNorm = playerCar.Physics.Speed / Mathf.Max(1f, playerCar.Physics.MaxSpeed);
                AudioService.UpdateEnginePitch(speedNorm, playerCar.LastInput.throttle);
            }
        }

        private void FixedUpdate()
        {
            if (_isPaused) return;
            if (_currentState != null)
            {
                _currentState.FixedUpdate(this, Time.fixedDeltaTime);
            }
        }

        private void ResolveVehicleCollisions()
        {
            CombatService.ResolveVehicleCollisions(
                _allCars,
                bikeCollisionRadius,
                collisionRestitution,
                collisionFriction,
                playerCar,
                AudioService,
                (a, b, pt) => RacersCollided?.Invoke(a, b, pt));
        }

        private int ChooseKickDirection(Car attacker)
        {
            return CombatService.FindBestKickDirection(
                attacker, _allCars, kickReachForward, kickReachLateral, ref _fallbackKickSide);
        }

        private void ResolveKickAttacks()
        {
            CombatService.ResolveKickAttacks(
                _allCars,
                kickReachForward,
                kickReachLateral,
                kickLateralImpulse,
                playerCar,
                AudioService,
                (att, hit, pt) => RacerKicked?.Invoke(att, hit, pt));
        }

        public static int FindBestKickDirection(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            ref int fallbackSide)
        {
            return RaceCombatSystem.FindBestKickDirectionStatic(
                attacker, allCars, forwardReach, lateralReach, ref fallbackSide);
        }

        public static Car TryResolveKickHit(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            float lateralImpulse,
            out Vector2 hitPos)
        {
            return RaceCombatSystem.TryResolveKickHitStatic(
                attacker, allCars, forwardReach, lateralReach, lateralImpulse, out hitPos);
        }

        private void BindSystemEvents()
        {
            if (lapManager != null)
            {
                lapManager.CheckpointPassed += OnCheckpointPassedAudio;
                lapManager.LapCompleted += OnLapCompletedAudio;
                lapManager.RacerFinished += OnRacerFinishedAudio;
            }
            if (policeController != null)
            {
                policeController.PoliceEngaged += OnPoliceEngaged;
            }
            if (skinManager != null)
            {
                skinManager.SkinChanged += OnSkinChanged;
            }
        }

        private void UnbindSystemEvents()
        {
            if (lapManager != null)
            {
                lapManager.CheckpointPassed -= OnCheckpointPassedAudio;
                lapManager.LapCompleted -= OnLapCompletedAudio;
                lapManager.RacerFinished -= OnRacerFinishedAudio;
            }
            if (policeController != null)
            {
                policeController.PoliceEngaged -= OnPoliceEngaged;
            }
            if (skinManager != null)
            {
                skinManager.SkinChanged -= OnSkinChanged;
            }
        }

        private void OnCheckpointPassedAudio(RacerProgress progress, Checkpoint cp)
        {
            if (progress.Car == playerCar && !cp.IsFinishLine && AudioService != null)
            {
                AudioService.PlayCheckpoint();
            }
        }

        private void OnLapCompletedAudio(RacerProgress progress)
        {
            if (progress.Car == playerCar && !progress.IsFinished && AudioService != null)
            {
                AudioService.PlayLapCompleted();
            }
        }

        private void OnRacerFinishedAudio(RacerProgress progress)
        {
            if (progress.Car == playerCar && AudioService != null)
            {
                AudioService.PlayRaceFinished();
            }
        }

        private void OnPoliceEngaged(Car copCar)
        {
            if (copCar != null)
            {
                _policeEngagementCount++;
            }
        }

        private void OnSkinChanged(BikeSkinData newSkin)
        {
            if (playerCar != null && newSkin != null)
            {
                playerCar.ApplySkin(newSkin);
            }
        }
    }
}
