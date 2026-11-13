using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Combines VehiclePhysics (composition), an abstract Driver (polymorphism),
    /// and track-relative state.
    /// </summary>
    public class Car : MonoBehaviour
    {
        [Header("Core Physics")]
        [SerializeField] private VehiclePhysics physics = new VehiclePhysics();

        [Header("Identity & Visuals")]
        [SerializeField] private bool isPlayer = true;
        [SerializeField] private Color bikeColor = new Color(0.92f, 0.22f, 0.22f);

        private Driver _driver;
        private bool _isOnTrack = true;
        private int _currentWaypointIndex;
        private float _trackDistance;
        private float _lateralOffset;
        private float _leanAngle;
        private VehicleInput _lastInput;
        private Vector2 _previousPosition;

        public VehiclePhysics Physics => physics;
        /// <summary>Position at the start of the last physics step (used for checkpoint crossing).</summary>
        public Vector2 PreviousPosition => _previousPosition;
        public Driver Driver => _driver;
        public bool IsPlayer => isPlayer;
        public Color BikeColor => bikeColor;
        public string RacerName => _driver != null ? _driver.DriverName : gameObject.name;

        public bool IsOnTrack => _isOnTrack;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public float TrackDistance => _trackDistance;
        public float LateralOffset => _lateralOffset;
        public float LeanAngle => _leanAngle;
        public VehicleInput LastInput => _lastInput;

        public void ConfigureIdentity(bool playerFlag, Color color)
        {
            isPlayer = playerFlag;
            bikeColor = color;
        }

        public void Initialize(Driver driver, Vector2 startPos, float startHeadingRad, WaypointTrack track)
        {
            _driver = driver;
            physics.Reset(startPos, startHeadingRad);
            _previousPosition = startPos;
            _leanAngle = 0f;
            _lastInput = default;

            if (_driver != null)
            {
                _driver.OnReset();
            }

            UpdateTrackProjection(track);
            SyncTransform();
        }

        public void ResetToTrackPoint(Vector2 pos, float headingRad, WaypointTrack track)
        {
            physics.Reset(pos, headingRad);
            _previousPosition = pos;
            _leanAngle = 0f;
            _lastInput = default;
            UpdateTrackProjection(track);
            SyncTransform();
        }

        public void StepPhysics(float dt, WaypointTrack track)
        {
            if (physics == null || dt <= 0f) return;

            _previousPosition = physics.Position;
            _lastInput = _driver != null ? _driver.DecideInput(this, track) : default;

            physics.ApplyThrottle(_lastInput.throttle);
            physics.ApplyBrake(_lastInput.brake);
            physics.ApplySteering(_lastInput.steering);

            if (track != null)
            {
                TrackProjection proj = track.ProjectPosition(physics.Position);
                _isOnTrack = proj.isOnTrack;
                _currentWaypointIndex = proj.segmentIndex;
                _trackDistance = proj.distanceAlongTrack;
                _lateralOffset = proj.lateralOffset;

                if (_isOnTrack)
                {
                    physics.SetSurfaceModifier(1f, 1f, 1f);
                }
                else
                {
                    physics.SetSurfaceModifier(3.0f, 0.55f, 0.55f);
                }
            }

            physics.Update(dt);

            if (track != null)
            {
                EnforceOuterBarrier(track);
                UpdateTrackProjection(track);
            }

            float speedFactor = Mathf.Clamp01(physics.Speed / 18f);
            float targetLean = _lastInput.steering * speedFactor;
            _leanAngle = Mathf.Lerp(_leanAngle, targetLean, 1f - Mathf.Exp(-10f * dt));

            SyncTransform();
        }

        public void ApplyCollisionPush(Vector2 worldDeltaPos, Vector2 worldVelocityImpulse)
        {
            physics.TranslatePosition(worldDeltaPos);
            physics.ApplyVelocityImpulse(worldVelocityImpulse);
            physics.ScaleVelocity(0.94f);
            SyncTransform();
        }

        private void EnforceOuterBarrier(WaypointTrack track)
        {
            TrackProjection proj = track.ProjectPosition(physics.Position);
            float maxLateral = track.MaxOffRoadWidth * 0.5f;
            if (Mathf.Abs(proj.lateralOffset) > maxLateral)
            {
                float clampedLat = Mathf.Sign(proj.lateralOffset) * maxLateral;
                Vector2 right = new Vector2(proj.tangent.y, -proj.tangent.x);
                physics.SetPosition(proj.closestPoint + right * clampedLat);

                float outwardSpeed = Vector2.Dot(physics.Velocity, right) * Mathf.Sign(proj.lateralOffset);
                if (outwardSpeed > 0f)
                {
                    physics.ApplyVelocityImpulse(-right * (outwardSpeed * Mathf.Sign(proj.lateralOffset) * 1.2f));
                    physics.ScaleVelocity(0.85f);
                }
            }
        }

        private void UpdateTrackProjection(WaypointTrack track)
        {
            if (track == null) return;
            TrackProjection proj = track.ProjectPosition(physics.Position);
            _isOnTrack = proj.isOnTrack;
            _currentWaypointIndex = proj.segmentIndex;
            _trackDistance = proj.distanceAlongTrack;
            _lateralOffset = proj.lateralOffset;
        }

        private void SyncTransform()
        {
            transform.position = new Vector3(physics.Position.x, physics.Position.y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, physics.Heading * Mathf.Rad2Deg);
        }
    }
}
