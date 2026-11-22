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

        public VehicleStatsAsset BaseStatsAsset => baseStatsAsset;
        public VehicleStats BaseStats => baseStatsAsset != null ? baseStatsAsset.Stats : VehicleStats.Default;
        public VehiclePhysics Physics => physics;
        /// <summary>Position at the start of the last physics step (used for checkpoint crossing).</summary>
        public Vector2 PreviousPosition => _previousPosition;
        public float PreviousTrackDistance => _previousTrackDistance;
        public float PreviousLateralOffset => _previousLateralOffset;
        public Driver Driver => _driver;
        public bool IsPlayer => isPlayer;
        public bool IsRoadRashArcadeActive => enableRoadRashPlayerHandling && isPlayer && _driver is PlayerDriver;
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
            SyncPreviousInterpolationState();
            SyncTransform();
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
            _leanAngle = Mathf.Clamp((kickDir >= 0 ? 1f : -1f) * 0.9f, -1f, 1f);
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
        }

        public void StepPhysics(float dt, WaypointTrack track)
        {
            if (physics == null || dt <= 0f) return;

            if (_kickTimer > 0f) _kickTimer = Mathf.Max(0f, _kickTimer - dt);
            if (_kickCooldownTimer > 0f) _kickCooldownTimer = Mathf.Max(0f, _kickCooldownTimer - dt);
            if (_hitStaggerTimer > 0f) _hitStaggerTimer = Mathf.Max(0f, _hitStaggerTimer - dt);
            if (_collisionKnockbackTimer > 0f) _collisionKnockbackTimer = Mathf.Max(0f, _collisionKnockbackTimer - dt);

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
                physics.ApplySteering(_lastInput.steering);

                if (track != null)
                {
                    TrackProjection proj = track.ProjectPosition(physics.Position);
                    _isOnTrack = proj.isOnTrack;
                    _currentWaypointIndex = proj.segmentIndex;
                    _trackDistance = proj.distanceAlongTrack;
                    _lateralOffset = proj.lateralOffset;

                    if (_collisionKnockbackTimer > 0f)
                    {
                        // Low sliding lateral tire grip so struck bikes physically skid sideways across lanes
                        physics.SetSurfaceModifier(_isOnTrack ? 1.08f : 1.25f, 0.22f, 1.0f);
                    }
                    else if (_isOnTrack)
                    {
                        physics.SetSurfaceModifier(1f, 1f, 1f);
                    }
                    else
                    {
                        // Mild speed cap off-road while keeping throttle punchy and steering grip responsive for quick recovery
                        physics.SetSurfaceModifier(1.30f, 0.95f, 0.58f);

                        // Assist turning back onto the track when throttling and steering inward from the shoulder
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
                                    // Positive offset turns toward -right (left inward from right shoulder) and vice versa
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
                }

                physics.Update(dt);

                if (Mathf.Abs(_angularVelocity) > 0.001f)
                {
                    physics.SetHeading(physics.Heading + _angularVelocity * dt);
                    _angularVelocity *= Mathf.Exp(-5.5f * dt);
                    if (Mathf.Abs(_angularVelocity) < 0.005f) _angularVelocity = 0f;
                }
            }

            if (track != null)
            {
                EnforceOuterBarrier(track, dt);
                UpdateTrackProjection(track);
            }

            float speedFactor = Mathf.Clamp01(physics.Speed / 18f);
            float targetLean = _lastInput.steering * speedFactor;
            _leanAngle = Mathf.Lerp(_leanAngle, targetLean, 1f - Mathf.Exp(-10f * dt));

            SyncTransform();
        }

        private void StepRoadRashArcadePhysics(float dt, WaypointTrack track, float throttle)
        {
            TrackProjection proj = track.ProjectPosition(physics.Position);
            _isOnTrack = proj.isOnTrack;
            _currentWaypointIndex = proj.segmentIndex;
            _trackDistance = proj.distanceAlongTrack;
            _lateralOffset = proj.lateralOffset;

            TrackSample sample = track.SampleAtDistance(_trackDistance);
            Vector2 roadTangent = sample.tangent.sqrMagnitude > 0.0001f ? sample.tangent.normalized : proj.tangent;
            Vector2 roadRight = new Vector2(roadTangent.y, -roadTangent.x);
            float roadHeading = Mathf.Atan2(roadTangent.y, roadTangent.x);

            float roadForwardSpeed = Vector2.Dot(physics.Velocity, roadTangent);
            float roadLateralSpeed = Vector2.Dot(physics.Velocity, roadRight);

            if (_isOnTrack)
            {
                physics.SetSurfaceModifier(1f, 1.15f, 1f);
            }
            else
            {
                // Forgiving Road Rash off-road: keep riding smoothly almost just like on asphalt
                physics.SetSurfaceModifier(1.06f, 1.05f, 0.88f);
            }

            float prevHeading = physics.Heading;
            physics.SetHeading(roadHeading);
            physics.SetVelocity(roadTangent * roadForwardSpeed);
            physics.ApplySteering(0f);
            physics.Update(dt);
            float newForwardSpeed = Vector2.Dot(physics.Velocity, roadTangent);

            float nextLatVel;
            if (_collisionKnockbackTimer > 0f)
            {
                // Real Coulomb kinetic tire friction opposing lateral skid: F = mu * m * g => a = mu * g
                float muKinetic = _isOnTrack ? 0.72f : 0.48f;
                float frictionDecel = muKinetic * Gravity;
                float recoveryProgress = _collisionKnockbackDuration > 0.01f
                    ? 1f - Mathf.Clamp01(_collisionKnockbackTimer / _collisionKnockbackDuration)
                    : 1f;
                nextLatVel = Mathf.MoveTowards(roadLateralSpeed, 0f, frictionDecel * dt)
                             + (_lastInput.steering * 5.0f * recoveryProgress * dt);
            }
            else
            {
                float gripFactor = Mathf.Clamp(physics.Grip / 10f, 0.85f, 1.35f);
                float steerSpeedScale = (Mathf.Abs(newForwardSpeed) > 0.25f || throttle > 0.05f)
                    ? Mathf.Clamp01(Mathf.Abs(newForwardSpeed) / 8f + 0.30f)
                    : 0f;
                float steerLateralVel = _lastInput.steering * (14.5f * gripFactor) * steerSpeedScale;
                if (_hitStaggerTimer > 0f) steerLateralVel *= 0.45f;

                float speedNorm = Mathf.Clamp01(Mathf.Max(0f, newForwardSpeed) / Mathf.Max(1f, physics.MaxSpeed));
                // Positive curvature = right turn -> centrifugal pull pushes gently left (-roadRight)
                float centrifugalLatVel = Mathf.Clamp(-sample.curvature * speedNorm * speedNorm * (140f / gripFactor), -4.5f, 4.5f);

                float targetLatVel = steerLateralVel + centrifugalLatVel;
                float latBlendRate = _hitStaggerTimer > 0f ? 3.5f : 12.0f;
                nextLatVel = Mathf.Lerp(roadLateralSpeed, targetLatVel, 1f - Mathf.Exp(-latBlendRate * dt));
            }

            physics.TranslatePosition(roadRight * (nextLatVel * dt));
            physics.SetVelocity(roadTangent * newForwardSpeed + roadRight * nextLatVel);

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
            physics.AlignHeadingToward(roadHeading + headingTilt + _collisionYawOffset, (_collisionKnockbackTimer > 0f ? 16f : 10f) * dt);
        }

        public void ApplyPhysicalCollisionImpact(
            Vector2 worldDeltaPos,
            Vector2 worldVelocityDelta,
            float deltaAngularVelocity,
            float verticalLaunchVelocity)
        {
            physics.TranslatePosition(worldDeltaPos);
            physics.ApplyVelocityImpulse(worldVelocityDelta);

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

                if (IsRoadRashArcadeActive)
                {
                    float outwardSpeed = Vector2.Dot(physics.Velocity, right) * sideSign;
                    if (outwardSpeed > 0f)
                    {
                        physics.ApplyVelocityImpulse(-right * (outwardSpeed * sideSign));
                        physics.ScaleVelocity(0.995f);
                    }
                    float trackHeading = Mathf.Atan2(proj.tangent.y, proj.tangent.x);
                    physics.AlignHeadingToward(trackHeading, 8f * dt);
                    return;
                }

                float nonArcadeOutwardSpeed = Vector2.Dot(physics.Velocity, right) * sideSign;
                if (nonArcadeOutwardSpeed > 0f)
                {
                    physics.ApplyVelocityImpulse(-right * (nonArcadeOutwardSpeed * sideSign * 1.12f));
                    physics.ScaleVelocity(0.985f);
                }

                // Wall-slide: smoothly align bike heading off the barrier so steep impacts don't stall at zero speed
                bool steeringInward = Mathf.Abs(_lastInput.steering) > 0.1f && Mathf.Sign(_lastInput.steering) == -sideSign;
                float outwardFacing = Vector2.Dot(physics.Forward, right) * sideSign;
                if (outwardFacing > 0.04f || steeringInward)
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
