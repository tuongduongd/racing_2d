using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Coordinates the race: WaypointTrack, player (PlayerDriver + InputHandler),
    /// AI opponents (AIRacerDriver), polymorphic physics stepping, vehicle collisions,
    /// LapCheckpointManager (laps/ranking) and RaceHUD.
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

        [Header("Race Settings")]
        [SerializeField] private int aiOpponentCount = 4;
        [SerializeField] private int totalLaps = 3;
        [SerializeField] private float bikeCollisionRadius = 1.3f;

        [Header("Starting Grid")]
        [SerializeField] private float gridFirstRowOffset = 4f;
        [SerializeField] private float gridRowSpacing = 6.5f;
        [SerializeField] private float gridLaneOffset = 2.8f;

        private readonly List<Car> _allCars = new List<Car>();
        private readonly List<AIRacerDriver> _aiDrivers = new List<AIRacerDriver>();

        public IReadOnlyList<Car> AllCars => _allCars;
        public LapCheckpointManager LapManager => lapManager;

        private static readonly (string name, float skill, float latOffset, Color color)[] DefaultAiProfiles =
        {
            ("Viper",  0.94f, -3.5f, new Color(0.20f, 0.85f, 0.35f)),
            ("Blaze",  0.88f,  3.5f, new Color(1.00f, 0.55f, 0.12f)),
            ("Shadow", 0.80f, -1.5f, new Color(0.72f, 0.32f, 0.96f)),
            ("Nova",   0.72f,  1.8f, new Color(0.20f, 0.78f, 0.96f)),
            ("Titan",  0.65f,  0.0f, new Color(0.95f, 0.85f, 0.22f)),
        };

        private void Start()
        {
            SetupReferences();
            SpawnOrSyncOpponents();
            lapManager.Initialize(track, _allCars, totalLaps);
            raceHud.Bind(playerCar, lapManager, track);
            ResetRaceGrid();
        }

        private void SetupReferences()
        {
            track = FindOrCreate(track, "WaypointTrack");
            inputHandler = GetOrAdd(inputHandler);
            playerDriver = GetOrAdd(playerDriver);
            lapManager = GetOrAdd(lapManager);
            raceHud = GetOrAdd(raceHud);

            playerDriver.SetDriverName("Player");
            playerDriver.SetInputHandler(inputHandler);

            playerCar = FindOrCreate(playerCar, "PlayerCar");
            playerCar.ConfigureIdentity(true, new Color(0.95f, 0.24f, 0.24f));

            roadRenderer = FindOrCreate(roadRenderer, "RoadRashRenderer2D");
        }

        private static T FindOrCreate<T>(T current, string objectName) where T : Component
        {
            if (current != null) return current;
            T found = FindFirstObjectByType<T>();
            if (found != null) return found;
            return new GameObject(objectName).AddComponent<T>();
        }

        private T GetOrAdd<T>(T current) where T : Component
        {
            if (current != null) return current;
            T found = FindFirstObjectByType<T>();
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

            AIRacerDriver[] existingAis = FindObjectsByType<AIRacerDriver>(FindObjectsSortMode.None);
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

        /// <summary>
        /// Places every racer on a 2-lane grid behind the finish line (player starts last,
        /// Road Rash style) and resets lap progress.
        /// </summary>
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

        private Vector2 GridSlotPosition(int slot, Vector2 origin, Vector2 dir, Vector2 right)
        {
            int row = slot / 2;
            float side = (slot % 2 == 0) ? -gridLaneOffset : gridLaneOffset;
            return origin - dir * (gridFirstRowOffset + row * gridRowSpacing) + right * side;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetRaceGrid();
            }
        }

        private void FixedUpdate()
        {
            if (track == null) return;

            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < _allCars.Count; i++)
            {
                if (_allCars[i] != null)
                {
                    _allCars[i].StepPhysics(dt, track);
                }
            }

            ResolveVehicleCollisions();

            if (lapManager != null)
            {
                lapManager.Tick(dt);
            }
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
                    }
                }
            }
        }
    }
}
