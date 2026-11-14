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
        [SerializeField] private VehicleStatsAsset baseStatsAsset;
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

        private float _kickTimer;
        private float _kickDuration = 0.34f;
        private float _kickCooldownTimer;
        private int _kickDirection = 1;
        private bool _kickHitLanded;
        private float _hitStaggerTimer;

        public VehicleStatsAsset BaseStatsAsset => baseStatsAsset;
        public VehicleStats BaseStats => baseStatsAsset != null ? baseStatsAsset.Stats : VehicleStats.Default;
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

        public bool IsKicking => _kickTimer > 0f;
        public int KickDirection => _kickDirection;
        public float KickProgress => _kickDuration > 0f && _kickTimer > 0f
            ? 1f - (_kickTimer / _kickDuration)
            : 0f;
        public float KickCooldownRemaining => _kickCooldownTimer;
        public bool CanKick => _kickTimer <= 0f && _kickCooldownTimer <= 0f && _hitStaggerTimer <= 0f;
        public bool HasKickHitThisSwing => _kickHitLanded;
        public bool IsStaggered => _hitStaggerTimer > 0f;
        public float HitStaggerRemaining => _hitStaggerTimer;

        private void Awake()
        {
            if (baseStatsAsset != null)
            {
                physics.ConfigureStats(baseStatsAsset.Stats);
            }
        }

        public void SetBaseStatsAsset(VehicleStatsAsset statsAsset)
        {
            baseStatsAsset = statsAsset;
            if (baseStatsAsset != null)
            {
                physics.ConfigureStats(baseStatsAsset.Stats);
            }
        }

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
            ResetCombatState();

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
            ResetCombatState();
            UpdateTrackProjection(track);
            SyncTransform();
        }

        public bool TryStartKick(int direction, float duration = 0.34f, float cooldown = 0.58f)
        {
            if (!CanKick) return false;
            _kickDuration = Mathf.Max(0.1f, duration);
            _kickTimer = _kickDuration;
            _kickCooldownTimer = Mathf.Max(_kickDuration, cooldown);
            _kickDirection = direction >= 0 ? 1 : -1;
            _kickHitLanded = false;
            return true;
        }

        public void MarkKickHit()
        {
            _kickHitLanded = true;
        }

        public void ReceiveKick(int kickDir, Vector2 lateralImpulse, float staggerDuration = 0.45f)
        {
            _kickTimer = 0f;
            _hitStaggerTimer = Mathf.Max(_hitStaggerTimer, staggerDuration);
            physics.ApplyVelocityImpulse(lateralImpulse);
            physics.ScaleVelocity(0.84f);
            _leanAngle = Mathf.Clamp((kickDir >= 0 ? 1f : -1f) * 0.9f, -1f, 1f);
        }

        private void ResetCombatState()
        {
            _kickTimer = 0f;
            _kickCooldownTimer = 0f;
            _kickHitLanded = false;
            _hitStaggerTimer = 0f;
        }

        public void StepPhysics(float dt, WaypointTrack track)
        {
            if (physics == null || dt <= 0f) return;

            if (_kickTimer > 0f) _kickTimer = Mathf.Max(0f, _kickTimer - dt);
            if (_kickCooldownTimer > 0f) _kickCooldownTimer = Mathf.Max(0f, _kickCooldownTimer - dt);
            if (_hitStaggerTimer > 0f) _hitStaggerTimer = Mathf.Max(0f, _hitStaggerTimer - dt);

            _previousPosition = physics.Position;
            _lastInput = _driver != null ? _driver.DecideInput(this, track) : default;

            float throttle = _hitStaggerTimer > 0f ? _lastInput.throttle * 0.55f : _lastInput.throttle;
            physics.ApplyThrottle(throttle);
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
