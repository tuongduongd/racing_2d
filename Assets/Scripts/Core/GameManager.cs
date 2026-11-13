using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Phase 1 & 2 GameManager: initializes the WaypointTrack, player Car, InputHandler,
    /// and RoadRashRenderer2D, runs fixed-timestep physics, and displays a basic test HUD.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene References")]
        public WaypointTrack track;
        public Car playerCar;
        public InputHandler inputHandler;
        public RoadRashRenderer2D roadRenderer;

        private GUIStyle _speedStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _warnStyle;

        private void Start()
        {
            SetupReferences();
            ResetPlayerToStart();
        }

        private void SetupReferences()
        {
            if (track == null)
            {
                track = FindFirstObjectByType<WaypointTrack>();
                if (track == null)
                {
                    GameObject trackObj = new GameObject("WaypointTrack");
                    track = trackObj.AddComponent<WaypointTrack>();
                }
            }

            if (inputHandler == null)
            {
                inputHandler = FindFirstObjectByType<InputHandler>();
                if (inputHandler == null)
                {
                    inputHandler = gameObject.AddComponent<InputHandler>();
                }
            }

            if (playerCar == null)
            {
                playerCar = FindFirstObjectByType<Car>();
                if (playerCar == null)
                {
                    GameObject carObj = new GameObject("PlayerCar");
                    playerCar = carObj.AddComponent<Car>();
                }
            }

            if (roadRenderer == null)
            {
                roadRenderer = FindFirstObjectByType<RoadRashRenderer2D>();
                if (roadRenderer == null)
                {
                    GameObject renderObj = new GameObject("RoadRashRenderer2D");
                    roadRenderer = renderObj.AddComponent<RoadRashRenderer2D>();
                }
            }

            roadRenderer.track = track;
            roadRenderer.playerCar = playerCar;
            if (roadRenderer.targetCamera == null)
            {
                roadRenderer.targetCamera = Camera.main;
            }
        }

        public void ResetPlayerToStart()
        {
            if (track == null || playerCar == null) return;

            Vector2 startPos = track.waypoints != null && track.waypoints.Length > 0
                ? track.waypoints[0]
                : Vector2.zero;
            Vector2 startDir = track.GetTrackDirection(0);
            float startHeading = Mathf.Atan2(startDir.y, startDir.x);

            playerCar.isPlayer = true;
            playerCar.Initialize(inputHandler, startPos, startHeading, track);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetPlayerToStart();
            }
        }

        private void FixedUpdate()
        {
            if (playerCar != null && track != null)
            {
                playerCar.StepPhysics(Time.fixedDeltaTime, track);
            }
        }

        private void OnGUI()
        {
            if (playerCar == null) return;

            if (_speedStyle == null)
            {
                _speedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold
                };
                _speedStyle.normal.textColor = Color.white;

                _infoStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                _infoStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f);

                _warnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                _warnStyle.normal.textColor = new Color(1f, 0.35f, 0.25f);
            }

            GUI.Box(new Rect(14, 14, 290, 118), GUIContent.none);
            int kmh = Mathf.RoundToInt(Mathf.Abs(playerCar.physics.SpeedKmh));
            GUI.Label(new Rect(26, 20, 260, 40), $"{kmh} km/h", _speedStyle);

            string trackState = playerCar.isOnTrack ? "ON TRACK" : "OFF-ROAD (SLOW)";
            GUIStyle stateStyle = playerCar.isOnTrack ? _infoStyle : _warnStyle;
            GUI.Label(new Rect(26, 60, 260, 26), trackState, stateStyle);

            int wpCount = track != null ? track.WaypointCount : 0;
            GUI.Label(new Rect(26, 86, 260, 24),
                $"Waypoint: {playerCar.currentWaypointIndex + 1}/{wpCount}  |  Dist: {playerCar.trackDistance:F0}m",
                _infoStyle);

            GUI.Label(new Rect(26, 108, 260, 22),
                "W/Up: Ga | S/Down: Phanh | A/D: Lai | R: Reset",
                _infoStyle);
        }
    }
}
