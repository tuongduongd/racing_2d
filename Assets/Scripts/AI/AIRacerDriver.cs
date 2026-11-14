using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Autonomous AI racer controller inheriting from abstract Driver.
    /// Follows WaypointTrack closed loops, modulates speed for upcoming corners,
    /// varies racing line and behavior via skillLevel, avoids nearby bikes, and recovers if stuck.
    /// </summary>
    public class AIRacerDriver : Driver
    {
        [Header("AI Navigation")]
        [SerializeField] private int targetWaypoint;
        [SerializeField] private float waypointReachRadius = 14f;
        [SerializeField] private float maxSteerAngleDeg = 42f;

        [Header("AI Personality & Skill")]
        [SerializeField] private AIProfile profile;
        [Range(-0.15f, 0.15f)]
        [SerializeField] private float skillOffset = 0f;
        [Range(0.4f, 1.0f)]
        [SerializeField] private float skillLevel = 0.85f;
        [SerializeField] private float preferredLateralOffset = 0f;
        [SerializeField] private float minCornerSpeed = 22f;
        [SerializeField] private float noiseAmplitude = 0.08f;
        [SerializeField] private float noiseFrequency = 0.7f;

        [Header("Avoidance & Recovery")]
        [SerializeField] private float avoidanceLookAhead = 20f;
        [SerializeField] private float avoidanceLateralRange = 3.5f;
        [SerializeField] private float stuckSpeedThreshold = 2.5f;
        [SerializeField] private float stuckTimeout = 2.5f;

        [Header("Determinism")]
        [SerializeField] private bool useCustomSeed;
        [SerializeField] private int seed;

        private IReadOnlyList<Car> _allCars;
        private float _noiseSeed;
        private float _stuckTimer;
        private float _dynamicAvoidOffset;
        private float _simulationTime;
        private bool _hasSeed;
        private int _seed;

        public int TargetWaypointIndex => targetWaypoint;
        public AIProfile Profile => profile;
        public float SkillOffset => skillOffset;
        public float SkillLevel => profile != null
            ? Mathf.Clamp(profile.SkillLevel + skillOffset, 0.4f, 1.0f)
            : skillLevel;
        public float PreferredLateralOffset => preferredLateralOffset;
        public bool HasSeed => _hasSeed;
        public int Seed => _seed;

        private void Awake()
        {
            if (useCustomSeed)
            {
                SetSeed(seed);
            }
            else
            {
                _noiseSeed = Random.Range(0f, 100f);
            }
        }

        public void SetSeed(int randomSeed)
        {
            useCustomSeed = true;
            seed = randomSeed;
            _hasSeed = true;
            _seed = randomSeed;
            _noiseSeed = ComputeNoiseSeedFromSeed(randomSeed);
        }

        public void Configure(string aiName, float skill, float lateralOffset)
        {
            SetDriverName(aiName);
            profile = null;
            skillOffset = 0f;
            skillLevel = Mathf.Clamp(skill, 0.4f, 1.0f);
            preferredLateralOffset = lateralOffset;
            _noiseSeed = _hasSeed ? ComputeNoiseSeedFromSeed(_seed) : ComputeNoiseSeed(aiName);
        }

        public void Configure(string aiName, float skill, float lateralOffset, int randomSeed)
        {
            SetSeed(randomSeed);
            Configure(aiName, skill, lateralOffset);
        }

        public void Configure(string aiName, AIProfile aiProfile, float lateralOffset, float racerSkillOffset = 0f)
        {
            SetDriverName(aiName);
            preferredLateralOffset = lateralOffset;
            ApplyProfile(aiProfile, racerSkillOffset);
            _noiseSeed = _hasSeed ? ComputeNoiseSeedFromSeed(_seed) : ComputeNoiseSeed(aiName);
        }

        public void Configure(string aiName, AIProfile aiProfile, float lateralOffset, float racerSkillOffset, int randomSeed)
        {
            SetSeed(randomSeed);
            Configure(aiName, aiProfile, lateralOffset, racerSkillOffset);
        }

        public void ApplyProfile(AIProfile aiProfile, float racerSkillOffset = 0f)
        {
            profile = aiProfile;
            skillOffset = Mathf.Clamp(racerSkillOffset, -0.15f, 0.15f);
            if (profile != null)
            {
                skillLevel = Mathf.Clamp(profile.SkillLevel + skillOffset, 0.4f, 1.0f);
                minCornerSpeed = profile.MinCornerSpeed;
                noiseAmplitude = profile.NoiseAmplitude;
                noiseFrequency = profile.NoiseFrequency;
            }
        }

        private static float ComputeNoiseSeed(string aiName)
        {
            if (string.IsNullOrEmpty(aiName)) return 0f;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < aiName.Length; i++)
                {
                    hash ^= aiName[i];
                    hash *= 16777619u;
                }
                return (hash % 1000u) * 0.13f;
            }
        }

        private static float ComputeNoiseSeedFromSeed(int randomSeed)
        {
            var rng = new System.Random(randomSeed);
            return (float)(rng.NextDouble() * 100.0);
        }

        public void SetAllCars(IReadOnlyList<Car> cars)
        {
            _allCars = cars;
        }

        public override void OnReset()
        {
            targetWaypoint = 1;
            _stuckTimer = 0f;
            _dynamicAvoidOffset = 0f;
            _simulationTime = 0f;
            if (_hasSeed)
            {
                _noiseSeed = ComputeNoiseSeedFromSeed(_seed);
            }
            else if (useCustomSeed)
            {
                SetSeed(seed);
            }
        }

        public override VehicleInput DecideInput(Car car, WaypointTrack track)
        {
            if (car == null || track == null || track.WaypointCount < 2)
            {
                return default;
            }

            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            float decisionTime = _simulationTime;
            _simulationTime += dt;
            int wpCount = track.WaypointCount;

            // 1. Stuck detection & automatic recovery
            if (car.Physics.Speed < stuckSpeedThreshold)
            {
                _stuckTimer += dt;
                if (_stuckTimer >= stuckTimeout)
                {
                    int recoverIdx = car.CurrentWaypointIndex;
                    Vector2 recoverPos = track.GetWaypoint(recoverIdx);
                    Vector2 recoverDir = track.GetTrackDirection(recoverIdx);
                    float recoverHeading = Mathf.Atan2(recoverDir.y, recoverDir.x);
                    car.ResetToTrackPoint(recoverPos, recoverHeading, track);
                    targetWaypoint = (recoverIdx + 1) % wpCount;
                    _stuckTimer = 0f;
                    return new VehicleInput(1f, 0f, 0f);
                }
            }
            else
            {
                _stuckTimer = 0f;
            }

            // 2. Advance waypoint when reached (or if already past segment)
            targetWaypoint = ((targetWaypoint % wpCount) + wpCount) % wpCount;
            Vector2 wpPos = track.GetWaypoint(targetWaypoint);
            float distToWp = Vector2.Distance(car.Physics.Position, wpPos);
            if (distToWp <= waypointReachRadius)
            {
                targetWaypoint = (targetWaypoint + 1) % wpCount;
                wpPos = track.GetWaypoint(targetWaypoint);
            }

            // 3. Calculate lateral avoidance if another bike is directly ahead
            float targetAvoid = ComputeAvoidanceOffset(car, track);
            _dynamicAvoidOffset = Mathf.Lerp(_dynamicAvoidOffset, targetAvoid, 1f - Mathf.Exp(-4f * dt));

            // 4. Compute look-ahead target point with racing line offset
            Vector2 nextWpPos = track.GetWaypoint(targetWaypoint + 1);
            float lookBlend = Mathf.Clamp01(1f - distToWp / 35f) * 0.45f;
            Vector2 baseTarget = Vector2.Lerp(wpPos, nextWpPos, lookBlend);

            Vector2 segDir = track.GetTrackDirection(targetWaypoint);
            Vector2 segRight = new Vector2(segDir.y, -segDir.x);
            float maxOffset = track.TrackWidth * 0.36f;
            float clampedOffset = Mathf.Clamp(preferredLateralOffset + _dynamicAvoidOffset, -maxOffset, maxOffset);
            Vector2 aimPoint = baseTarget + segRight * clampedOffset;

            ResolveDrivingTuning(
                out float effectiveSkill,
                out float topSpeedFactor,
                out float cornerSpeedFloor,
                out float brakeAggression,
                out float throttleGain,
                out float activeNoiseAmp,
                out float activeNoiseFreq);

            // 5. Steering towards aimPoint (+ subtle skill-based human variation)
            Vector2 toTarget = (aimPoint - car.Physics.Position).normalized;
            float angleErrorRad = SignedAngleRad(car.Physics.Forward, toTarget);
            float angleErrorDeg = angleErrorRad * Mathf.Rad2Deg;

            // Note: negative angleError means target is to the right -> positive steering turns right
            float rawSteer = -angleErrorDeg / Mathf.Max(5f, maxSteerAngleDeg);
            float imperfection = (1.05f - effectiveSkill) * activeNoiseAmp;
            float wobble = Mathf.Sin(decisionTime * activeNoiseFreq + _noiseSeed) * imperfection;
            float steer = Mathf.Clamp(rawSteer + wobble, -1f, 1f);

            // 6. Corner speed prediction & throttle/brake modulation
            Vector2 dirHere = track.GetTrackDirection(targetWaypoint - 1);
            Vector2 dirNext = track.GetTrackDirection(targetWaypoint);
            Vector2 dirAfter = track.GetTrackDirection(targetWaypoint + 1);

            float turnAngle1 = Vector2.Angle(dirHere, dirNext);
            float turnAngle2 = Vector2.Angle(dirNext, dirAfter);
            float upcomingTurn = Mathf.Max(turnAngle1, turnAngle2 * 0.85f);

            float sharpness = Mathf.Clamp01(upcomingTurn / 75f);
            float effectiveTopSpeed = car.Physics.MaxSpeed * topSpeedFactor;
            float cornerTargetSpeed = Mathf.Lerp(effectiveTopSpeed, cornerSpeedFloor, sharpness * brakeAggression);

            float currentSpeed = car.Physics.ForwardSpeed;
            float throttle = 0f;
            float brake = 0f;

            if (currentSpeed > cornerTargetSpeed + 5f)
            {
                brake = Mathf.Clamp01((currentSpeed - cornerTargetSpeed) / 14f);
            }
            else if (currentSpeed > cornerTargetSpeed)
            {
                throttle = 0.25f * effectiveSkill;
            }
            else
            {
                // Ease off throttle slightly if steering angle is very large
                float steerThrottlePenalty = Mathf.Lerp(1f, 0.65f, Mathf.Abs(steer) * 0.5f);
                throttle = throttleGain * steerThrottlePenalty;
            }

            return new VehicleInput(throttle, brake, steer);
        }

        private void ResolveDrivingTuning(
            out float effectiveSkill,
            out float topSpeedFactor,
            out float cornerSpeedFloor,
            out float brakeAggression,
            out float throttleGain,
            out float activeNoiseAmp,
            out float activeNoiseFreq)
        {
            if (profile != null)
            {
                effectiveSkill = Mathf.Clamp(profile.SkillLevel + skillOffset, 0.4f, 1.0f);
                topSpeedFactor = Mathf.Clamp(profile.TopSpeedFactor + skillOffset * 0.25f, 0.5f, 1.05f);
                cornerSpeedFloor = Mathf.Max(5f, profile.MinCornerSpeed + skillOffset * 12f);
                brakeAggression = Mathf.Clamp(profile.CornerBrakeAggression - skillOffset * 0.4f, 0.5f, 1.4f);
                throttleGain = Mathf.Clamp(profile.ThrottleScale + skillOffset * 0.25f, 0.5f, 1.05f);
                activeNoiseAmp = profile.NoiseAmplitude;
                activeNoiseFreq = profile.NoiseFrequency;
                return;
            }

            effectiveSkill = Mathf.Clamp(skillLevel, 0.4f, 1.0f);
            topSpeedFactor = Mathf.Lerp(0.76f, 0.98f, effectiveSkill);
            cornerSpeedFloor = minCornerSpeed;
            brakeAggression = Mathf.Lerp(1.1f, 0.78f, effectiveSkill);
            throttleGain = Mathf.Clamp01(effectiveSkill * 1.05f);
            activeNoiseAmp = noiseAmplitude;
            activeNoiseFreq = noiseFrequency;
        }

        private float ComputeAvoidanceOffset(Car self, WaypointTrack track)
        {
            if (_allCars == null || _allCars.Count <= 1) return 0f;

            float totalLen = track.TotalLength;
            if (totalLen <= 1f) return 0f;

            float bestDz = avoidanceLookAhead;
            float avoidSign = 0f;

            for (int i = 0; i < _allCars.Count; i++)
            {
                Car other = _allCars[i];
                if (other == null || other == self) continue;

                float dz = other.TrackDistance - self.TrackDistance;
                if (dz < -totalLen * 0.5f) dz += totalLen;
                if (dz > totalLen * 0.5f) dz -= totalLen;

                if (dz > 1f && dz < bestDz)
                {
                    float dLat = other.LateralOffset - self.LateralOffset;
                    if (Mathf.Abs(dLat) < avoidanceLateralRange)
                    {
                        bestDz = dz;
                        // Steer away from the blocking bike toward the side with more road space
                        if (Mathf.Abs(other.LateralOffset) > track.TrackWidth * 0.2f)
                        {
                            avoidSign = -Mathf.Sign(other.LateralOffset);
                        }
                        else
                        {
                            avoidSign = dLat >= 0f ? -1f : 1f;
                        }
                    }
                }
            }

            return avoidSign * 4.2f;
        }

        private static float SignedAngleRad(Vector2 from, Vector2 to)
        {
            float cross = from.x * to.y - from.y * to.x;
            float dot = Vector2.Dot(from, to);
            return Mathf.Atan2(cross, dot);
        }
    }
}
