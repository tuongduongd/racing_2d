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
        [Header("Scene References")]
        [SerializeField] private WaypointTrack track;
        [SerializeField] private Car playerCar;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private PlayerDriver playerDriver;
        [SerializeField] private RoadRashRenderer2D roadRenderer;
        [SerializeField] private LapCheckpointManager lapManager;
        [SerializeField] private RaceHUD raceHud;
        [SerializeField] private GameMenuUI menuUi;
        [SerializeField] private RaceAudioController audioController;

        [Header("Race Settings")]
        [SerializeField] private int aiOpponentCount = 4;
        [SerializeField] private int totalLaps = 3;
        [SerializeField] private float countdownDuration = 3f;
        [SerializeField] private float bikeCollisionRadius = 1.3f;
        [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Normal;
        [SerializeField] private bool startInMainMenu = true;

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

        private CarUpgradeSystem _upgradeSystem;
        private RaceStateBase _currentState;
        private bool _prizeAwardedForCurrentRace;
        private int _lastPrizeEarned;
        private int _lastFinishPosition;
        private int _fallbackKickSide = 1;

        public event Action<Car, Car, Vector2> RacerKicked;
        public event Action<Car, Car, Vector2> RacersCollided;

        public IReadOnlyList<Car> AllCars => _allCars;
        public Car PlayerCar => playerCar;
        public WaypointTrack Track => track;
        public LapCheckpointManager LapManager => lapManager;
        public CarUpgradeSystem UpgradeSystem => _upgradeSystem;
        public RaceAudioController AudioController => audioController;

        public GameFlowState CurrentFlowState => _currentState != null ? _currentState.StateType : GameFlowState.MainMenu;
        public DifficultyLevel Difficulty => difficulty;
        public int TotalLaps => totalLaps;
        public int LastPrizeEarned => _lastPrizeEarned;
        public int LastFinishPosition => _lastFinishPosition;

        public float CountdownRemaining =>
            _currentState is CountdownState cs ? cs.RemainingSeconds : 0f;

        private static readonly (string name, float skill, float latOffset, Color color)[] DefaultAiProfiles =
        {
            ("Viper",  0.94f, -3.5f, new Color(0.20f, 0.85f, 0.35f)),
            ("Blaze",  0.88f,  3.5f, new Color(1.00f, 0.55f, 0.12f)),
            ("Shadow", 0.80f, -1.5f, new Color(0.72f, 0.32f, 0.96f)),
            ("Nova",   0.72f,  1.8f, new Color(0.20f, 0.78f, 0.96f)),
            ("Titan",  0.65f,  0.0f, new Color(0.95f, 0.85f, 0.22f)),
        };

        private void Awake()
        {
            _upgradeSystem = new CarUpgradeSystem();
            _upgradeSystem.Load();

            _states[GameFlowState.MainMenu] = new MainMenuState();
            _states[GameFlowState.Countdown] = new CountdownState(countdownDuration);
            _states[GameFlowState.Racing] = new RacingState();
            _states[GameFlowState.Finished] = new FinishedState();
            _states[GameFlowState.UpgradeShop] = new UpgradeShopState();
        }

        private void Start()
        {
            SetupReferences();
            SpawnOrSyncOpponents();
            ApplyPlayerUpgrades();
            ApplyDifficultyToAI();

            lapManager.Initialize(track, _allCars, totalLaps);
            BindAudioEvents();
            raceHud.Bind(this, playerCar, lapManager, track);
            menuUi.Bind(this);

            TransitionTo(startInMainMenu ? GameFlowState.MainMenu : GameFlowState.Countdown);
        }

        private void OnDestroy()
        {
            UnbindAudioEvents();
        }

        private void SetupReferences()
        {
            track = FindOrCreate(track, "WaypointTrack");
            inputHandler = GetOrAdd(inputHandler);
            playerDriver = GetOrAdd(playerDriver);
            lapManager = GetOrAdd(lapManager);
            raceHud = GetOrAdd(raceHud);
            menuUi = GetOrAdd(menuUi);
            audioController = GetOrAdd(audioController);

            playerDriver.SetDriverName("Player");
            playerDriver.SetInputHandler(inputHandler);

            playerCar = FindOrCreate(playerCar, "PlayerCar");
            playerCar.ConfigureIdentity(true, RoadRashVisualTheme.PlayerJacketBlue);

            roadRenderer = FindOrCreate(roadRenderer, "RoadRashRenderer2D");
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

            for (int i = _aiDrivers.Count; i < aiOpponentCount; i++)
            {
                var profile = DefaultAiProfiles[i % DefaultAiProfiles.Length];
                GameObject aiObj = new GameObject($"AICar_{profile.name}");
                Car aiCar = aiObj.AddComponent<Car>();
                aiCar.ConfigureIdentity(false, profile.color);

                AIRacerDriver aiDriver = aiObj.AddComponent<AIRacerDriver>();
                aiDriver.Configure(profile.name, profile.skill, profile.latOffset);

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
            if (!_states.TryGetValue(targetState, out RaceStateBase next)) return;

            _currentState?.Exit(this);
            _currentState = next;
            _currentState.Enter(this);
        }

        public void StartRace()
        {
            ApplyPlayerUpgrades();
            ApplyDifficultyToAI();
            lapManager.SetTotalLaps(totalLaps);
            _prizeAwardedForCurrentRace = false;
            _lastPrizeEarned = 0;
            _lastFinishPosition = 0;
            TransitionTo(GameFlowState.Countdown);
        }

        public void OpenGarage()
        {
            TransitionTo(GameFlowState.UpgradeShop);
        }

        public void OpenMenu()
        {
            TransitionTo(GameFlowState.MainMenu);
        }

        public void SetDifficulty(DifficultyLevel newDifficulty)
        {
            difficulty = newDifficulty;
            ApplyDifficultyToAI();
        }

        public void SetTotalLaps(int laps)
        {
            totalLaps = Mathf.Clamp(laps, 1, 10);
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
            if (_upgradeSystem == null) return;
            _upgradeSystem.ResetProgress();
            ApplyPlayerUpgrades();
        }

        public void ApplyPlayerUpgrades()
        {
            if (_upgradeSystem != null && playerCar != null)
            {
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

            int slot = 0;
            for (int i = 0; i < _aiDrivers.Count; i++)
            {
                Car aiCar = _aiDrivers[i].GetComponent<Car>();
                if (aiCar == null) continue;
                aiCar.Initialize(_aiDrivers[i], GridSlotPosition(slot++, origin, dir, right), heading, track);
            }

            if (playerCar != null)
            {
                playerCar.Initialize(playerDriver, GridSlotPosition(slot, origin, dir, right), heading, track);
            }

            if (lapManager != null) lapManager.ResetRace();
            if (raceHud != null) raceHud.ClearMessage();
        }

        public void StepRaceSimulation(float dt)
        {
            if (track == null) return;

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
            _lastPrizeEarned = _upgradeSystem != null
                ? _upgradeSystem.AwardRaceResult(_lastFinishPosition, p.FinishTime)
                : 0;
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

        private void ApplyDifficultyToAI()
        {
            float offset = difficulty switch
            {
                DifficultyLevel.Easy => -0.14f,
                DifficultyLevel.Hard => 0.06f,
                _ => 0f
            };

            for (int i = 0; i < _aiDrivers.Count; i++)
            {
                var profile = DefaultAiProfiles[i % DefaultAiProfiles.Length];
                float tunedSkill = Mathf.Clamp(profile.skill + offset, 0.45f, 0.99f);
                _aiDrivers[i].Configure(profile.name, tunedSkill, profile.latOffset);
            }
        }

        private Vector2 GridSlotPosition(int slot, Vector2 origin, Vector2 dir, Vector2 right)
        {
            int row = slot / 2;
            float side = (slot % 2 == 0) ? -gridLaneOffset : gridLaneOffset;
            return origin - dir * (gridFirstRowOffset + row * gridRowSpacing) + right * side;
        }

        private void Update()
        {
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
            _currentState?.FixedUpdate(this, Time.fixedDeltaTime);
        }

        private void ResolveVehicleCollisions()
        {
            float minDist = bikeCollisionRadius * 2f;
            float minDistSq = minDist * minDist;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car a = _allCars[i];
                if (a == null) continue;

                for (int j = i + 1; j < _allCars.Count; j++)
                {
                    Car b = _allCars[j];
                    if (b == null) continue;

                    Vector2 delta = b.Physics.Position - a.Physics.Position;
                    float distSq = delta.sqrMagnitude;

                    if (distSq < minDistSq)
                    {
                        float dist = Mathf.Sqrt(distSq);
                        Vector2 normal = dist > 0.001f ? delta / dist : Vector2.right;
                        float overlap = (minDist - dist) * 0.5f;

                        Vector2 relVel = b.Physics.Velocity - a.Physics.Velocity;
                        float closingSpeed = Vector2.Dot(relVel, normal);
                        Vector2 impulse = closingSpeed < 0f ? normal * (-closingSpeed * 0.45f) : Vector2.zero;

                        a.ApplyCollisionPush(-normal * overlap, -impulse);
                        b.ApplyCollisionPush(normal * overlap, impulse);

                        Vector2 mid = (a.Physics.Position + b.Physics.Position) * 0.5f;
                        RacersCollided?.Invoke(a, b, mid);

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
