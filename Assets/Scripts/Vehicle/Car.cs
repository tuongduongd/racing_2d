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
        [SerializeField] private BikeSkinData activeSkin;

        [Header("Road Rash Arcade Feel")]
        [SerializeField] private bool enableRoadRashPlayerHandling = true;

        private Driver _driver;
        private bool _isOnTrack = true;
        private int _currentWaypointIndex;
        private float _trackDistance;
        private float _lateralOffset;
        private float _leanAngle;
        private VehicleInput _lastInput;
        private Vector2 _previousPosition;
        private float _previousTrackDistance;
        private float _previousLateralOffset;
        private float _previousHeading;
        private float _previousLeanAngle;

        private const float Gravity = 9.81f;

        private float _kickTimer;
        private float _kickDuration = 0.34f;
        private float _kickCooldownTimer;
        private int _kickDirection = 1;
        private bool _kickHitLanded;
        private float _hitStaggerTimer;

        private float _collisionKnockbackTimer;
        private float _collisionKnockbackDuration;
        private float _angularVelocity;
        private float _collisionYawOffset;
        private float _airHeight;
        private float _previousAirHeight;
        private float _verticalVelocity;
        private bool _hasArcadeFrenetState;
        private float _arcadeLateralVel;
        private Vector2 _lastArcadeTangent = Vector2.right;
        private float _hazardSlowTimer;
        private float _hazardSpeedCapMultiplier = 1f;
        private float _obstacleImmunityTimer;
        private bool _obstacleImmunityTickedThisStep;

        public VehicleStatsAsset BaseStatsAsset => baseStatsAsset;
        public VehicleStats BaseStats => baseStatsAsset != null ? baseStatsAsset.Stats : VehicleStats.Default;
        public VehiclePhysics Physics => physics;
        /// <summary>Position at the start of the last physics step (used for checkpoint crossing).</summary>
        public Vector2 PreviousPosition => _previousPosition;
        public float PreviousTrackDistance => _previousTrackDistance;
        public float PreviousLateralOffset => _previousLateralOffset;
        public Driver Driver => _driver;
        public bool IsPlayer => isPlayer;
        public bool IsRoadRashArcadeActive => enableRoadRashPlayerHandling && isPlayer && _driver != null && _driver.UsesArcadeAssist;
        public Color BikeColor => bikeColor;
        public BikeSkinData ActiveSkin => activeSkin;
        public string RacerName => _driver != null ? _driver.DriverName : gameObject.name;

        public bool IsOnTrack => _isOnTrack;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public float TrackDistance => _trackDistance;
        public float LateralOffset => _lateralOffset;
        public float LeanAngle => _leanAngle;
        public VehicleInput LastInput => _lastInput;

        public float GetInterpolatedTrackDistance(float alpha, WaypointTrack track)
        {
            if (track == null || track.TotalLength <= 0.01f) return _trackDistance;
            float t = Mathf.Clamp01(alpha);
            float totalLen = track.TotalLength;
            float delta = _trackDistance - _previousTrackDistance;
            if (delta < -totalLen * 0.5f) delta += totalLen;
            else if (delta > totalLen * 0.5f) delta -= totalLen;
            return track.WrapDistance(_previousTrackDistance + delta * t);
        }

        public float GetInterpolatedLateralOffset(float alpha)
        {
            return Mathf.Lerp(_previousLateralOffset, _lateralOffset, Mathf.Clamp01(alpha));
        }

        public float GetInterpolatedHeading(float alpha)
        {
            float t = Mathf.Clamp01(alpha);
            return Mathf.LerpAngle(_previousHeading * Mathf.Rad2Deg, physics.Heading * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
        }

        public float GetInterpolatedLeanAngle(float alpha)
        {
            return Mathf.Lerp(_previousLeanAngle, _leanAngle, Mathf.Clamp01(alpha));
        }

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
        public bool IsCollisionKnockback => _collisionKnockbackTimer > 0f;
        public float CollisionKnockbackRemaining => _collisionKnockbackTimer;
        public float AngularVelocity => _angularVelocity;
        public float AirHeight => _airHeight;
        public bool IsObstacleImmune => _obstacleImmunityTimer > 0f;
        public float ObstacleImmunityRemaining => _obstacleImmunityTimer;
        public bool IsHazardSlowed => _hazardSlowTimer > 0f;

        public float GetInterpolatedAirHeight(float alpha)
        {
            return Mathf.Lerp(_previousAirHeight, _airHeight, Mathf.Clamp01(alpha));
        }

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

        public void ApplySkin(BikeSkinData skin)
        {
            activeSkin = skin;
            if (skin != null)
            {
                bikeColor = skin.PrimaryColor;
            }
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
            SyncArcadeFrenetFromTrack(track, physics.Velocity);
            SyncPreviousInterpolationState();
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
            SyncArcadeFrenetFromTrack(track, physics.Velocity);
            SyncPreviousInterpolationState();
            SyncTransform();
        }

        public void RestoreRuntimeState(Vector2 pos, Vector2 velocity, float headingRad, WaypointTrack track)
        {
            physics.Reset(pos, headingRad);
            physics.SetVelocity(velocity);
            _previousPosition = pos;
            _leanAngle = 0f;
            _lastInput = default;
            ResetCombatState();
            UpdateTrackProjection(track);
            SyncArcadeFrenetFromTrack(track, velocity);
            SyncPreviousInterpolationState();
            SyncTransform();
        }

        private void SyncArcadeFrenetFromTrack(WaypointTrack track, Vector2 velocity)
        {
            if (track == null || track.WaypointCount < 2)
            {
                _hasArcadeFrenetState = false;
                _arcadeLateralVel = 0f;
                return;
            }

            Vector2 tan = track.SampleAtDistance(_trackDistance).tangent;
            if (tan.sqrMagnitude < 0.0001f)
            {
                tan = track.GetTrackDirection(_currentWaypointIndex);
            }
            _lastArcadeTangent = tan.sqrMagnitude > 0.0001f ? tan.normalized : Vector2.right;
            Vector2 right = new Vector2(_lastArcadeTangent.y, -_lastArcadeTangent.x);
            _arcadeLateralVel = Vector2.Dot(velocity, right);
            _hasArcadeFrenetState = true;
        }

        public void ConsumeOrTickObstacleImmunity(float dt)
        {
            if (_obstacleImmunityTickedThisStep)
            {
                _obstacleImmunityTickedThisStep = false;
                return;
            }
            if (_obstacleImmunityTimer > 0f && dt > 0f)
            {
                _obstacleImmunityTimer = Mathf.Max(0f, _obstacleImmunityTimer - dt);
            }
        }

        private void SyncPreviousInterpolationState()
        {
            _previousTrackDistance = _trackDistance;
            _previousLateralOffset = _lateralOffset;
            _previousHeading = physics.Heading;
            _previousLeanAngle = _leanAngle;
            _previousAirHeight = _airHeight;
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
            if (_hasArcadeFrenetState)
            {
                Vector2 lastRight = new Vector2(_lastArcadeTangent.y, -_lastArcadeTangent.x);
                _arcadeLateralVel = Vector2.Dot(physics.Velocity, lastRight);
            }
            _leanAngle = Mathf.Clamp((kickDir >= 0 ? 1f : -1f) * 0.9f, -1f, 1f);
        }

        /// <summary>
        /// Applies temporary speed loss from surface hazards (potholes, oil slicks)
        /// without flinging, staggering, teleporting, or pushing the bike backward.
        /// </summary>
        public void ApplySurfaceHazardSlowdown(float speedMultiplier, float duration = 0.45f)
        {
            if (physics == null) return;
            float clampedMult = Mathf.Clamp(speedMultiplier, 0.35f, 0.90f);
            physics.ScaleVelocity(clampedMult);
            _hazardSlowTimer = Mathf.Max(_hazardSlowTimer, Mathf.Max(0.1f, duration));
            _hazardSpeedCapMultiplier = Mathf.Min(_hazardSpeedCapMultiplier, clampedMult);
        }

        /// <summary>
        /// Applies lateral knockback + stagger + backward track rebound from solid obstacles
        /// (barriers, barrels, cones, parked buses) with short immunity cooldown to prevent sticky loops.
        /// </summary>
        public void ApplySolidObstacleKnockback(
            int pushSide,
            WaypointTrack track,
            float speedMultiplierOnHit,
            float lateralKnockImpulse,
            float backwardMeters,
            float backwardVelocityImpulse,
            float immunityDuration = 0.55f)
        {
            if (physics == null) return;

            int side = pushSide >= 0 ? 1 : -1;
            float preSpeed = physics.Speed;
            Vector2 roadTangent = physics.Forward;
            float currentDist = _trackDistance;
            float currentLat = _lateralOffset;

            if (track != null && track.WaypointCount >= 2)
            {
                if (!_hasArcadeFrenetState)
                {
                    TrackProjection proj = track.ProjectPosition(physics.Position);
                    currentDist = proj.distanceAlongTrack;
                    currentLat = proj.lateralOffset;
                }

                TrackSample sample = track.SampleAtDistance(currentDist);
                if (sample.tangent.sqrMagnitude > 0.0001f)
                {
                    roadTangent = sample.tangent.normalized;
                }
                else
                {
                    roadTangent = track.GetTrackDirection(track.GetSegmentIndexAtDistance(currentDist));
                }
            }

            if (roadTangent.sqrMagnitude < 0.0001f)
            {
                roadTangent = Vector2.right;
            }
            Vector2 roadRight = new Vector2(roadTangent.y, -roadTangent.x);

            float backDist = Mathf.Max(0.5f, backwardMeters);
            float latShift = side * 0.65f;
            if (track != null && track.WaypointCount >= 2)
            {
                float maxHalfOffRoad = track.MaxOffRoadWidth * 0.5f;
                float newDist = track.WrapDistance(currentDist - backDist);
                float newLat = Mathf.Clamp(currentLat + latShift, -maxHalfOffRoad, maxHalfOffRoad);
                Vector2 newPos = track.EvaluateWorldPosition(newDist, newLat, out _currentWaypointIndex, out Vector2 updatedTangent);
                physics.SetPosition(newPos);
                _trackDistance = newDist;
                _lateralOffset = newLat;
                _isOnTrack = Mathf.Abs(newLat) <= track.TrackWidth * 0.5f;
                _lastArcadeTangent = updatedTangent;
                roadTangent = updatedTangent;
                roadRight = new Vector2(roadTangent.y, -roadTangent.x);
            }
            else
            {
                physics.TranslatePosition(-roadTangent * backDist + roadRight * latShift);
            }

            float clampedMult = Mathf.Clamp(speedMultiplierOnHit, 0.40f, 0.82f);
            float desiredTotalSpeed = preSpeed >= 2.0f
                ? Mathf.Clamp(preSpeed * clampedMult, 1.5f, Mathf.Max(1.5f, preSpeed * 0.85f))
                : Mathf.Max(6.0f, backwardVelocityImpulse * 0.55f);

            float rawBack = Mathf.Max(2.5f, backwardVelocityImpulse * 0.55f);
            float rawLat = Mathf.Max(2.0f, lateralKnockImpulse * 0.55f);
            Vector2 impulseDir = (-roadTangent * rawBack + roadRight * (side * rawLat)).normalized;
            Vector2 reboundVel = impulseDir * desiredTotalSpeed;

            physics.SetVelocity(reboundVel);
            _arcadeLateralVel = Vector2.Dot(reboundVel, roadRight);

            _kickTimer = 0f;
            _obstacleImmunityTimer = Mathf.Max(_obstacleImmunityTimer, Mathf.Max(0.2f, immunityDuration));
            _obstacleImmunityTickedThisStep = false;
            _hitStaggerTimer = Mathf.Max(_hitStaggerTimer, 0.42f);
            _collisionKnockbackTimer = Mathf.Max(_collisionKnockbackTimer, 0.38f);
            _collisionKnockbackDuration = Mathf.Max(_collisionKnockbackDuration, 0.38f);
            _angularVelocity = Mathf.Clamp(_angularVelocity + side * 4.5f, -7.5f, 7.5f);
            _leanAngle = Mathf.Clamp(side * 0.95f, -1f, 1f);
            SyncTransform();
        }

        private void ResetCombatState()
        {
            _kickTimer = 0f;
            _kickCooldownTimer = 0f;
            _kickHitLanded = false;
            _hitStaggerTimer = 0f;
            _collisionKnockbackTimer = 0f;
            _collisionKnockbackDuration = 0f;
            _angularVelocity = 0f;
            _collisionYawOffset = 0f;
            _airHeight = 0f;
            _previousAirHeight = 0f;
            _verticalVelocity = 0f;
            _arcadeLateralVel = 0f;
            _hazardSlowTimer = 0f;
            _hazardSpeedCapMultiplier = 1f;
            _obstacleImmunityTimer = 0f;
            _obstacleImmunityTickedThisStep = false;
        }

        public void StepPhysics(float dt, WaypointTrack track)
        {
            if (physics == null || dt <= 0f) return;

            if (_kickTimer > 0f) _kickTimer = Mathf.Max(0f, _kickTimer - dt);
            if (_kickCooldownTimer > 0f) _kickCooldownTimer = Mathf.Max(0f, _kickCooldownTimer - dt);
            if (_hitStaggerTimer > 0f) _hitStaggerTimer = Mathf.Max(0f, _hitStaggerTimer - dt);
            if (_collisionKnockbackTimer > 0f) _collisionKnockbackTimer = Mathf.Max(0f, _collisionKnockbackTimer - dt);
            if (_hazardSlowTimer > 0f)
            {
                _hazardSlowTimer = Mathf.Max(0f, _hazardSlowTimer - dt);
                if (_hazardSlowTimer <= 0f) _hazardSpeedCapMultiplier = 1f;
            }
            if (_obstacleImmunityTimer > 0f) _obstacleImmunityTimer = Mathf.Max(0f, _obstacleImmunityTimer - dt);
            _obstacleImmunityTickedThisStep = true;

            _previousPosition = physics.Position;
            SyncPreviousInterpolationState();

            if (_airHeight > 0f || _verticalVelocity > 0f)
            {
                _verticalVelocity -= Gravity * dt;
                _airHeight += _verticalVelocity * dt;
                if (_airHeight <= 0f)
                {
                    _airHeight = 0f;
                    _verticalVelocity = _verticalVelocity < -0.65f ? -_verticalVelocity * 0.28f : 0f;
                }
            }

            _lastInput = _driver != null ? _driver.DecideInput(this, track) : default;

            float throttle = _hitStaggerTimer > 0f ? _lastInput.throttle * 0.55f : _lastInput.throttle;
            physics.ApplyThrottle(throttle);
            physics.ApplyBrake(_lastInput.brake);

            if (track != null && IsRoadRashArcadeActive)
            {
                StepRoadRashArcadePhysics(dt, track, throttle);
            }
            else
            {
                _hasArcadeFrenetState = false;
                physics.ApplySteering(_lastInput.steering);

                if (track != null)
                {
                    TrackProjection proj = track.ProjectPosition(physics.Position);
                    _isOnTrack = proj.isOnTrack;
                    _currentWaypointIndex = proj.segmentIndex;
                    _trackDistance = proj.distanceAlongTrack;
                    _lateralOffset = proj.lateralOffset;

                    float hazardCap = _hazardSlowTimer > 0f ? _hazardSpeedCapMultiplier : 1f;
                    float hazardDrag = _hazardSlowTimer > 0f ? 1.30f : 1f;

                    if (_collisionKnockbackTimer > 0f)
                    {
                        // Low sliding lateral tire grip so struck bikes physically skid sideways across lanes
                        physics.SetSurfaceModifier((_isOnTrack ? 1.08f : 1.25f) * hazardDrag, 0.22f, _isOnTrack ? hazardCap : Mathf.Min(0.58f, hazardCap));
                    }
                    else if (_isOnTrack)
                    {
                        physics.SetSurfaceModifier(hazardDrag, 1f, hazardCap);
                    }
                    else
                    {
                        bool aiRecovery = _driver != null && _driver.UsesAiOffRoadRecovery;
                        if (aiRecovery)
                        {
                            physics.SetSurfaceModifier(1.30f * hazardDrag, 1.0f, Mathf.Min(0.58f, hazardCap));

                            if (throttle > 0.1f && Mathf.Abs(_lastInput.steering) > 0.1f && Mathf.Abs(_lateralOffset) > 0.1f)
                            {
                                float sideSign = Mathf.Sign(_lateralOffset);
                                float inwardSign = -sideSign;
                                if (Mathf.Sign(_lastInput.steering) == inwardSign)
                                {
                                    Vector2 segRight = new Vector2(proj.tangent.y, -proj.tangent.x);
                                    physics.ApplyVelocityImpulse(segRight * (inwardSign * 16f * dt));

                                    float outwardFacing = Vector2.Dot(physics.Forward, segRight) * sideSign;
                                    float forwardAlign = Vector2.Dot(physics.Forward, proj.tangent);
                                    if (physics.Speed < 5.5f || outwardFacing > 0.05f || forwardAlign < 0.35f)
                                    {
                                        float trackHeading = Mathf.Atan2(proj.tangent.y, proj.tangent.x);
                                        float recoveryHeading = trackHeading + sideSign * 0.42f;
                                        physics.AlignHeadingToward(recoveryHeading, 4.5f * dt);
                                        if (physics.ForwardSpeed < 8f)
                                        {
                                            physics.ApplyVelocityImpulse(proj.tangent * (10f * dt));
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Cap top speed to 58% off-road while preserving steering feel and throttle movement (zero inward auto-pull)
                            physics.SetSurfaceModifier(1.15f * hazardDrag, 0.62f, Mathf.Min(0.58f, hazardCap));
                        }
                    }
                }

                physics.Update(dt);

                if (Mathf.Abs(_angularVelocity) > 0.001f)
                {
                    physics.SetHeading(physics.Heading + _angularVelocity * dt);
                    _angularVelocity *= Mathf.Exp(-5.5f * dt);
                    if (Mathf.Abs(_angularVelocity) < 0.005f) _angularVelocity = 0f;
                }

                if (track != null)
                {
                    EnforceOuterBarrier(track, dt);
                    UpdateTrackProjection(track);
                }
            }

            float speedFactor = Mathf.Clamp01(physics.Speed / 18f);
            float targetLean = _lastInput.steering * speedFactor;
            _leanAngle = Mathf.Lerp(_leanAngle, targetLean, 1f - Mathf.Exp(-10f * dt));

            SyncTransform();
        }

        private void StepRoadRashArcadePhysics(float dt, WaypointTrack track, float throttle)
        {
            if (!_hasArcadeFrenetState)
            {
                UpdateTrackProjection(track);
                SyncArcadeFrenetFromTrack(track, physics.Velocity);
            }

            float halfRoadWidth = track.TrackWidth * 0.5f;
            _isOnTrack = Mathf.Abs(_lateralOffset) <= halfRoadWidth;

            TrackSample sample = track.SampleAtDistance(_trackDistance);
            Vector2 roadTangent = sample.tangent.sqrMagnitude > 0.0001f
                ? sample.tangent.normalized
                : track.GetTrackDirection(_currentWaypointIndex);
            float roadHeading = Mathf.Atan2(roadTangent.y, roadTangent.x);

            float roadForwardSpeed = Vector2.Dot(physics.Velocity, _lastArcadeTangent);

            float hazardCap = _hazardSlowTimer > 0f ? _hazardSpeedCapMultiplier : 1f;
            float hazardDrag = _hazardSlowTimer > 0f ? 1.30f : 1f;
            if (_isOnTrack)
            {
                physics.SetSurfaceModifier(hazardDrag, 1.15f, hazardCap);
            }
            else
            {
                // Cap off-road speed at <= 60% MaxSpeed (0.58x) without ever pulling or teleporting toward centerline
                physics.SetSurfaceModifier(1.15f * hazardDrag, 0.85f, Mathf.Min(0.58f, hazardCap));
            }

            float prevHeading = physics.Heading;
            physics.SetHeading(roadHeading);
            physics.SetVelocity(roadTangent * roadForwardSpeed);
            physics.ApplySteering(0f);
            physics.Update(dt);
            float newForwardSpeed = Vector2.Dot(physics.Velocity, roadTangent);

            if (!_isOnTrack && throttle > 0.05f && _lastInput.brake < 0.05f &&
                _collisionKnockbackTimer <= 0f && newForwardSpeed < 2.5f)
            {
                newForwardSpeed = Mathf.Max(newForwardSpeed, Mathf.Max(0.5f, roadForwardSpeed) + 8.0f * dt);
            }

            _trackDistance = track.WrapDistance(_trackDistance + newForwardSpeed * dt);

            float nextLatVel;
            if (_collisionKnockbackTimer > 0f)
            {
                // Real Coulomb kinetic tire friction opposing lateral skid: F = mu * m * g => a = mu * g
                float muKinetic = _isOnTrack ? 0.72f : 0.48f;
                float frictionDecel = muKinetic * Gravity;
                float recoveryProgress = _collisionKnockbackDuration > 0.01f
                    ? 1f - Mathf.Clamp01(_collisionKnockbackTimer / _collisionKnockbackDuration)
                    : 1f;
                nextLatVel = Mathf.MoveTowards(_arcadeLateralVel, 0f, frictionDecel * dt)
                             + (_lastInput.steering * 5.0f * recoveryProgress * dt);
            }
            else
            {
                float gripFactor = Mathf.Clamp(physics.Grip / 10f, 0.85f, 1.35f);
                float steerSpeedScale = (Mathf.Abs(newForwardSpeed) > 0.25f || throttle > 0.05f)
                    ? Mathf.Clamp01(Mathf.Abs(newForwardSpeed) / 8f + 0.30f)
                    : 0f;
                float steerLateralVel = _lastInput.steering * (14.5f * gripFactor) * (_isOnTrack ? 1.0f : 0.85f) * steerSpeedScale;
                if (_hitStaggerTimer > 0f) steerLateralVel *= 0.45f;

                float latBlendRate = _hitStaggerTimer > 0f ? 3.5f : 12.0f;
                nextLatVel = Mathf.Lerp(_arcadeLateralVel, steerLateralVel, 1f - Mathf.Exp(-latBlendRate * dt));
                if (Mathf.Abs(_lastInput.steering) < 0.001f && Mathf.Abs(nextLatVel) < 0.005f)
                {
                    nextLatVel = 0f;
                }
            }

            _arcadeLateralVel = nextLatVel;
            _lateralOffset += _arcadeLateralVel * dt;

            // Soft outer boundary clamp only (prevents exiting map void; zero inward pull toward centerline)
            float maxLateral = track.MaxOffRoadWidth * 0.5f;
            if (Mathf.Abs(_lateralOffset) > maxLateral)
            {
                float sideSign = Mathf.Sign(_lateralOffset);
                _lateralOffset = sideSign * maxLateral;
                if (_arcadeLateralVel * sideSign > 0f)
                {
                    _arcadeLateralVel = 0f;
                }
            }

            Vector2 worldPos = track.EvaluateWorldPosition(_trackDistance, _lateralOffset, out _currentWaypointIndex, out Vector2 updatedTangent);
            Vector2 updatedRight = new Vector2(updatedTangent.y, -updatedTangent.x);
            float updatedRoadHeading = Mathf.Atan2(updatedTangent.y, updatedTangent.x);

            _isOnTrack = Mathf.Abs(_lateralOffset) <= halfRoadWidth;
            _lastArcadeTangent = updatedTangent;

            physics.SetPosition(worldPos);
            physics.SetVelocity(updatedTangent * newForwardSpeed + updatedRight * _arcadeLateralVel);

            if (_collisionKnockbackTimer > 0f || Mathf.Abs(_angularVelocity) > 0.01f || Mathf.Abs(_collisionYawOffset) > 0.002f)
            {
                const float springStiffness = 28f;
                const float yawDamping = 7.2f;
                float angAccel = -springStiffness * _collisionYawOffset - yawDamping * _angularVelocity;
                _angularVelocity += angAccel * dt;
                _collisionYawOffset = Mathf.Clamp(_collisionYawOffset + _angularVelocity * dt, -0.42f, 0.42f);
            }
            else
            {
                _angularVelocity = 0f;
                _collisionYawOffset = 0f;
            }

            physics.SetHeading(prevHeading);
            float headingTilt = -_lastInput.steering * 0.12f * Mathf.Clamp01(Mathf.Abs(newForwardSpeed) / 10f);
            physics.AlignHeadingToward(updatedRoadHeading + headingTilt + _collisionYawOffset, (_collisionKnockbackTimer > 0f ? 16f : 10f) * dt);
        }

        public void ApplyPhysicalCollisionImpact(
            Vector2 worldDeltaPos,
            Vector2 worldVelocityDelta,
            float deltaAngularVelocity,
            float verticalLaunchVelocity)
        {
            physics.TranslatePosition(worldDeltaPos);
            physics.ApplyVelocityImpulse(worldVelocityDelta);

            if (_hasArcadeFrenetState)
            {
                Vector2 lastRight = new Vector2(_lastArcadeTangent.y, -_lastArcadeTangent.x);
                _trackDistance += Vector2.Dot(worldDeltaPos, _lastArcadeTangent);
                _lateralOffset += Vector2.Dot(worldDeltaPos, lastRight);
                _arcadeLateralVel = Vector2.Dot(physics.Velocity, lastRight);
            }

            float impactDeltaSpeed = worldVelocityDelta.magnitude;
            if (impactDeltaSpeed >= 2.2f)
            {
                _angularVelocity = Mathf.Clamp(_angularVelocity + deltaAngularVelocity, -7.5f, 7.5f);
                float knockDur = Mathf.Clamp((impactDeltaSpeed - 1.5f) * 0.068f, 0.22f, 0.92f);
                _collisionKnockbackTimer = Mathf.Max(_collisionKnockbackTimer, knockDur);
                _collisionKnockbackDuration = Mathf.Max(_collisionKnockbackDuration, knockDur);
                _hitStaggerTimer = Mathf.Max(_hitStaggerTimer, knockDur * 0.72f);

                float latImpulse = Vector2.Dot(worldVelocityDelta, physics.Right);
                _leanAngle = Mathf.Clamp(_leanAngle + Mathf.Clamp(latImpulse / 7.5f, -1f, 1f), -1f, 1f);
            }

            if (verticalLaunchVelocity > 0.2f)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity, verticalLaunchVelocity);
            }

            SyncTransform();
        }

        public void ApplyCollisionPush(Vector2 worldDeltaPos, Vector2 worldVelocityImpulse)
        {
            physics.TranslatePosition(worldDeltaPos);
            physics.ApplyVelocityImpulse(worldVelocityImpulse);
            physics.ScaleVelocity(0.94f);
            if (_hasArcadeFrenetState)
            {
                Vector2 lastRight = new Vector2(_lastArcadeTangent.y, -_lastArcadeTangent.x);
                _trackDistance += Vector2.Dot(worldDeltaPos, _lastArcadeTangent);
                _lateralOffset += Vector2.Dot(worldDeltaPos, lastRight);
                _arcadeLateralVel = Vector2.Dot(physics.Velocity, lastRight);
            }
            SyncTransform();
        }

        private void EnforceOuterBarrier(WaypointTrack track, float dt)
        {
            TrackProjection proj = track.ProjectPosition(physics.Position);
            float maxLateral = track.MaxOffRoadWidth * 0.5f;
            if (Mathf.Abs(proj.lateralOffset) > maxLateral)
            {
                float sideSign = Mathf.Sign(proj.lateralOffset);
                float clampedLat = sideSign * maxLateral;
                Vector2 right = new Vector2(proj.tangent.y, -proj.tangent.x);
                physics.SetPosition(proj.closestPoint + right * clampedLat);

                bool aiRecovery = _driver != null && _driver.UsesAiOffRoadRecovery;
                float outwardSpeed = Vector2.Dot(physics.Velocity, right) * sideSign;
                if (outwardSpeed > 0f)
                {
                    physics.ApplyVelocityImpulse(-right * (outwardSpeed * sideSign * (aiRecovery ? 1.12f : 1.0f)));
                    physics.ScaleVelocity(aiRecovery ? 0.985f : 0.99f);
                }

                if (aiRecovery)
                {
                    bool steeringInward = Mathf.Abs(_lastInput.steering) > 0.1f && Mathf.Sign(_lastInput.steering) == -sideSign;
                    float outwardFacingAi = Vector2.Dot(physics.Forward, right) * sideSign;
                    if (outwardFacingAi > 0.04f || steeringInward)
                    {
                        float trackHeading = Mathf.Atan2(proj.tangent.y, proj.tangent.x);
                        float targetHeading = trackHeading + sideSign * (steeringInward ? 0.42f : 0.08f);
                        physics.AlignHeadingToward(targetHeading, (steeringInward ? 7.0f : 4.5f) * dt);
                    }

                    if (_lastInput.throttle > 0.1f)
                    {
                        float tangentSpeed = Vector2.Dot(physics.Velocity, proj.tangent);
                        if (tangentSpeed < 10f)
                        {
                            physics.ApplyVelocityImpulse(proj.tangent * (14f * dt));
                        }
                    }
                    return;
                }

                // Wall-slide strictly parallel to outer barrier (never turn or pull inward toward track centerline)
                float outwardFacing = Vector2.Dot(physics.Forward, right) * sideSign;
                if (outwardFacing > 0.01f)
                {
                    float trackHeading = Mathf.Atan2(proj.tangent.y, proj.tangent.x);
                    physics.AlignHeadingToward(trackHeading, 6.0f * dt);
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
