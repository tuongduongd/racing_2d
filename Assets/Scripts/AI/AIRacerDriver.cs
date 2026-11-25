using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public enum AINavigationMode
    {
        LegacyFixed = 0,
        SpeedAdaptiveWaypoint = 1,
        SmoothedSplinePath = 2
    }

    /// <summary>
    /// Autonomous AI racer controller inheriting from abstract Driver.
    /// Follows WaypointTrack closed loops, modulates speed for upcoming corners,
    /// varies racing line and behavior via skillLevel, avoids nearby bikes, and recovers if stuck.
    /// </summary>
    public class AIRacerDriver : Driver
    {
        [Header("AI Navigation")]
        [SerializeField] private AINavigationMode navigationMode = AINavigationMode.SmoothedSplinePath;
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

        private float _mapCornerSpeedMultiplier = 1f;

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

        public override bool UsesAiOffRoadRecovery => true;
        public AINavigationMode NavigationMode => navigationMode;
        public int TargetWaypointIndex => targetWaypoint;
        public AIProfile Profile => profile;
        public float SkillOffset => skillOffset;
        public float SkillLevel => profile != null
            ? Mathf.Clamp(profile.SkillLevel + skillOffset, 0.4f, 1.0f)
            : skillLevel;
        public float PreferredLateralOffset => preferredLateralOffset;
        public bool HasSeed => _hasSeed;
        public int Seed => _seed;

        public void SetNavigationMode(AINavigationMode mode)
        {
            navigationMode = mode;
        }

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

        public void SetMapCornerSpeedMultiplier(float multiplier)
        {
            _mapCornerSpeedMultiplier = Mathf.Clamp(multiplier, 0.75f, 1.35f);
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

            // 2. Calculate lateral avoidance if another bike is directly ahead
            float targetAvoid = ComputeAvoidanceOffset(car, track);
            _dynamicAvoidOffset = Mathf.Lerp(_dynamicAvoidOffset, targetAvoid, 1f - Mathf.Exp(-4f * dt));

            float maxOffset = Mathf.Max(track.LaneWidth * 0.35f, track.MaxLaneCenterOffset);
            float clampedOffset = Mathf.Clamp(preferredLateralOffset + _dynamicAvoidOffset, -maxOffset, maxOffset);

            ResolveDrivingTuning(
                out float effectiveSkill,
                out float topSpeedFactor,
                out float cornerSpeedFloor,
                out float brakeAggression,
                out float throttleGain,
                out float activeNoiseAmp,
                out float activeNoiseFreq);

            Vector2 aimPoint;
            float cornerTargetSpeed;
            float effectiveTopSpeed = car.Physics.MaxSpeed * topSpeedFactor;
            float adjustedCornerFloor = cornerSpeedFloor * _mapCornerSpeedMultiplier;

            if (navigationMode == AINavigationMode.SpeedAdaptiveWaypoint)
            {
                ComputeSpeedAdaptiveWaypointGuidance(
                    car,
                    track,
                    wpCount,
                    clampedOffset,
                    effectiveTopSpeed,
                    adjustedCornerFloor,
                    brakeAggression,
                    out aimPoint,
                    out cornerTargetSpeed);
            }
            else if (navigationMode == AINavigationMode.SmoothedSplinePath)
            {
                ComputeSmoothedSplineGuidance(
                    car,
                    track,
                    wpCount,
                    clampedOffset,
                    effectiveTopSpeed,
                    adjustedCornerFloor,
                    brakeAggression,
                    out aimPoint,
                    out cornerTargetSpeed);
            }
            else
            {
                ComputeLegacyFixedGuidance(
                    car,
                    track,
                    wpCount,
                    clampedOffset,
                    effectiveTopSpeed,
                    adjustedCornerFloor,
                    brakeAggression,
                    out aimPoint,
                    out cornerTargetSpeed);
            }

            // 5. Steering towards aimPoint (+ subtle skill-based human variation)
            Vector2 toTarget = (aimPoint - car.Physics.Position).normalized;
            float angleErrorRad = SignedAngleRad(car.Physics.Forward, toTarget);
            float angleErrorDeg = angleErrorRad * Mathf.Rad2Deg;

            // Note: negative angleError means target is to the right -> positive steering turns right
            float rawSteer = -angleErrorDeg / Mathf.Max(5f, maxSteerAngleDeg);
            float imperfection = (1.05f - effectiveSkill) * activeNoiseAmp;
            float wobble = Mathf.Sin(decisionTime * activeNoiseFreq + _noiseSeed) * imperfection;
            float steer = Mathf.Clamp(rawSteer + wobble, -1f, 1f);

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

        private void ComputeLegacyFixedGuidance(
            Car car,
            WaypointTrack track,
            int wpCount,
            float clampedOffset,
            float effectiveTopSpeed,
            float adjustedCornerFloor,
            float brakeAggression,
            out Vector2 aimPoint,
            out float cornerTargetSpeed)
        {
            targetWaypoint = ((targetWaypoint % wpCount) + wpCount) % wpCount;
            Vector2 wpPos = track.GetWaypoint(targetWaypoint);
            float distToWp = Vector2.Distance(car.Physics.Position, wpPos);
            if (distToWp <= waypointReachRadius)
            {
                targetWaypoint = (targetWaypoint + 1) % wpCount;
                wpPos = track.GetWaypoint(targetWaypoint);
            }

            Vector2 nextWpPos = track.GetWaypoint(targetWaypoint + 1);
            float lookBlend = Mathf.Clamp01(1f - distToWp / 35f) * 0.45f;
            Vector2 baseTarget = Vector2.Lerp(wpPos, nextWpPos, lookBlend);

            Vector2 segDir = track.GetTrackDirection(targetWaypoint);
            Vector2 segRight = new Vector2(segDir.y, -segDir.x);
            aimPoint = baseTarget + segRight * clampedOffset;

            Vector2 dirHere = track.GetTrackDirection(targetWaypoint - 1);
            Vector2 dirNext = track.GetTrackDirection(targetWaypoint);
            Vector2 dirAfter = track.GetTrackDirection(targetWaypoint + 1);

            float turnAngle1 = Vector2.Angle(dirHere, dirNext);
            float turnAngle2 = Vector2.Angle(dirNext, dirAfter);
            float upcomingTurn = Mathf.Max(turnAngle1, turnAngle2 * 0.85f);

            float sharpness = Mathf.Clamp01(upcomingTurn / 75f);
            cornerTargetSpeed = Mathf.Lerp(effectiveTopSpeed, adjustedCornerFloor, sharpness * brakeAggression);
        }

        private void ComputeSpeedAdaptiveWaypointGuidance(
            Car car,
            WaypointTrack track,
            int wpCount,
            float clampedOffset,
            float effectiveTopSpeed,
            float adjustedCornerFloor,
            float brakeAggression,
            out Vector2 aimPoint,
            out float cornerTargetSpeed)
        {
            targetWaypoint = ((targetWaypoint % wpCount) + wpCount) % wpCount;

            float speedRatio = Mathf.Clamp01(car.Physics.Speed / Mathf.Max(10f, car.Physics.MaxSpeed));
            Vector2 wpPos = track.GetWaypoint(targetWaypoint);
            Vector2 inDir = track.GetTrackDirection(targetWaypoint - 1);
            Vector2 outDir = track.GetTrackDirection(targetWaypoint);
            float currentCornerAngle = Vector2.Angle(inDir, outDir);

            // Speed-adaptive reach radius: wider at high speed on straights, tighter into 90-degree corners
            float baseAdaptiveRadius = Mathf.Lerp(waypointReachRadius * 0.72f, waypointReachRadius * 1.55f, speedRatio);
            float cornerRadiusScale = Mathf.Lerp(1f, 0.70f, Mathf.Clamp01((currentCornerAngle - 30f) / 60f));
            float adaptiveReachRadius = baseAdaptiveRadius * cornerRadiusScale;

            Vector2 inRight = new Vector2(inDir.y, -inDir.x);
            Vector2 laneWpPos = wpPos + inRight * clampedOffset;
            float distToCenterWp = Vector2.Distance(car.Physics.Position, wpPos);
            float distToLaneWp = Vector2.Distance(car.Physics.Position, laneWpPos);

            // Check incoming-plane passing (immune to perpendicular 90-degree outDir lateral offset)
            Vector2 bisector = inDir + outDir;
            Vector2 planeNormal = bisector.sqrMagnitude > 0.001f ? bisector.normalized : inDir;
            bool passedPlane = car.Physics.Speed >= stuckSpeedThreshold &&
                               Vector2.Dot(car.Physics.Position - wpPos, inDir) > 1.5f &&
                               Vector2.Dot(car.Physics.Position - wpPos, planeNormal) > 0.5f &&
                               distToCenterWp < track.MaxOffRoadWidth * 1.25f;

            if (distToLaneWp <= adaptiveReachRadius || distToCenterWp <= adaptiveReachRadius || passedPlane)
            {
                targetWaypoint = (targetWaypoint + 1) % wpCount;
                wpPos = track.GetWaypoint(targetWaypoint);
                inDir = track.GetTrackDirection(targetWaypoint - 1);
                outDir = track.GetTrackDirection(targetWaypoint);
                currentCornerAngle = Vector2.Angle(inDir, outDir);
                distToCenterWp = Vector2.Distance(car.Physics.Position, wpPos);
            }

            // Reduce premature corner-cutting interpolation on sharp bends
            Vector2 nextWpPos = track.GetWaypoint(targetWaypoint + 1);
            float cutDamping = 1f - Mathf.Clamp01(currentCornerAngle / 85f) * 0.82f;
            float lookBlend = Mathf.Clamp01(1f - distToCenterWp / 30f) * 0.38f * cutDamping;
            Vector2 baseTarget = Vector2.Lerp(wpPos, nextWpPos, lookBlend);

            Vector2 segRight = new Vector2(outDir.y, -outDir.x);
            float safeEdgeMargin = Mathf.Min(0.55f, track.LaneWidth * 0.16f);
            float safeMaxOffset = Mathf.Max(0f, track.TrackWidth * 0.5f - safeEdgeMargin);
            float cornerSafeOffset = Mathf.Clamp(clampedOffset, -safeMaxOffset, safeMaxOffset);
            float effectiveOffset = Mathf.Abs(car.LateralOffset) > safeMaxOffset
                ? cornerSafeOffset * 0.45f
                : cornerSafeOffset;
            aimPoint = baseTarget + segRight * effectiveOffset;

            // Multi-segment lookahead replacing the 75-degree clamp
            float horizonMeters = Mathf.Clamp(car.Physics.ForwardSpeed * 1.65f, 30f, 105f);
            float scannedDist = distToCenterWp;
            float maxSingleTurn = currentCornerAngle;
            float cumulativeTurn = currentCornerAngle;
            Vector2 prevDir = outDir;

            for (int step = 1; step <= 3; step++)
            {
                Vector2 segVec = track.GetWaypoint(targetWaypoint + step) - track.GetWaypoint(targetWaypoint + step - 1);
                float segLen = segVec.magnitude;
                if (scannedDist > horizonMeters) break;

                Vector2 nextDir = segLen > 0.01f ? segVec / segLen : prevDir;
                float turn = Vector2.Angle(prevDir, nextDir);
                float distWeight = Mathf.Clamp01(1f - scannedDist / (horizonMeters + 12f));
                maxSingleTurn = Mathf.Max(maxSingleTurn, turn * Mathf.Lerp(0.6f, 1.0f, distWeight));
                cumulativeTurn += turn * distWeight;

                scannedDist += segLen;
                prevDir = nextDir;
            }

            float effectiveTurn = Mathf.Max(maxSingleTurn, cumulativeTurn * 0.80f);
            float sharpness = Mathf.Clamp01(effectiveTurn / 95f);
            float severeBlend = Mathf.Clamp01((effectiveTurn - 55f) / 35f);
            float effectiveBrakeFactor = Mathf.Lerp(sharpness * brakeAggression, sharpness, severeBlend);
            cornerTargetSpeed = Mathf.Lerp(effectiveTopSpeed, adjustedCornerFloor, Mathf.Clamp01(effectiveBrakeFactor));
        }

        private void ComputeSmoothedSplineGuidance(
            Car car,
            WaypointTrack track,
            int wpCount,
            float clampedOffset,
            float effectiveTopSpeed,
            float adjustedCornerFloor,
            float brakeAggression,
            out Vector2 aimPoint,
            out float cornerTargetSpeed)
        {
            // Keep sequential targetWaypoint progression so stationary / low-speed tests remain intact
            targetWaypoint = ((targetWaypoint % wpCount) + wpCount) % wpCount;
            if (car.Physics.Speed >= stuckSpeedThreshold)
            {
                Vector2 wpPos = track.GetWaypoint(targetWaypoint);
                Vector2 inDir = track.GetTrackDirection(targetWaypoint - 1);
                Vector2 outDir = track.GetTrackDirection(targetWaypoint);
                Vector2 bisector = inDir + outDir;
                Vector2 planeNormal = bisector.sqrMagnitude > 0.001f ? bisector.normalized : inDir;
                float distToWp = Vector2.Distance(car.Physics.Position, wpPos);
                if (distToWp <= waypointReachRadius * 1.4f ||
                    (Vector2.Dot(car.Physics.Position - wpPos, inDir) > 1.5f &&
                     Vector2.Dot(car.Physics.Position - wpPos, planeNormal) > 0.5f &&
                     distToWp < track.MaxOffRoadWidth * 1.25f))
                {
                    targetWaypoint = (targetWaypoint + 1) % wpCount;
                }
            }

            float speed = car.Physics.Speed;
            float forwardSpeed = Mathf.Max(0f, car.Physics.ForwardSpeed);
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(10f, car.Physics.MaxSpeed));
            float currentS = car.TrackDistance;
            float totalLen = Mathf.Max(1f, track.TotalLength);

            // Multi-horizon scan along corridor-filleted smooth path (no Catmull-Rom outward overshoot, no 75-deg blind spot)
            SampleCorridorSmoothPath(track, currentS, out _, out Vector2 currentTangent, out _);

            float nearCornerDeg = 0f;
            float weightedCornerSeverity = 0f;
            float maxHorizonAngle = 0f;
            float brakingHorizon = Mathf.Clamp(forwardSpeed * 1.55f, 32f, 100f);

            float[] probeOffsets = { 10f, 22f, 36f, 54f, 76f };
            for (int i = 0; i < probeOffsets.Length; i++)
            {
                float d = probeOffsets[i];
                if (d > brakingHorizon && i > 1) break;

                SampleCorridorSmoothPath(track, (currentS + d) % totalLen, out _, out Vector2 aheadTan, out float cornerAngleHere);
                float angleDeg = Vector2.Angle(currentTangent, aheadTan);
                float combinedTurn = Mathf.Max(angleDeg, cornerAngleHere * 0.92f);

                if (d <= 24f)
                {
                    nearCornerDeg = Mathf.Max(nearCornerDeg, combinedTurn);
                }

                maxHorizonAngle = Mathf.Max(maxHorizonAngle, combinedTurn);
                float distWeight = Mathf.Clamp01(1f - Mathf.Max(0f, d - 14f) / Mathf.Max(22f, brakingHorizon));
                float normalizedTurn = Mathf.Clamp01(combinedTurn / 90f);
                weightedCornerSeverity = Mathf.Max(weightedCornerSeverity, normalizedTurn * Mathf.Lerp(0.55f, 1.0f, distWeight));
            }

            // Dynamic Pure-Pursuit lookahead along corridor-safe smoothed path
            float cornerTighten = Mathf.Clamp01(nearCornerDeg / 75f);
            float lookAheadDist = Mathf.Lerp(11f, 24f, speedRatio) * Mathf.Lerp(1f, 0.52f, cornerTighten);

            SampleCorridorSmoothPath(track, (currentS + lookAheadDist) % totalLen, out Vector2 smoothPos, out Vector2 smoothTan, out _);
            Vector2 smoothRight = new Vector2(smoothTan.y, -smoothTan.x);

            float safeEdgeMargin = Mathf.Min(0.55f, track.LaneWidth * 0.16f);
            float safeMaxOffset = Mathf.Max(0f, track.TrackWidth * 0.5f - safeEdgeMargin);
            float laneSafeOffset = Mathf.Clamp(clampedOffset, -safeMaxOffset, safeMaxOffset);
            float effectiveOffset = Mathf.Abs(car.LateralOffset) > safeMaxOffset
                ? laneSafeOffset * 0.35f
                : laneSafeOffset;
            aimPoint = smoothPos + smoothRight * effectiveOffset;

            float severeCornerBlend = Mathf.Clamp01((maxHorizonAngle - 55f) / 35f);
            float effectiveBrakeFactor = Mathf.Lerp(
                weightedCornerSeverity * brakeAggression,
                weightedCornerSeverity,
                severeCornerBlend);
            cornerTargetSpeed = Mathf.Lerp(effectiveTopSpeed, adjustedCornerFloor, Mathf.Clamp01(effectiveBrakeFactor));
        }

        private static void SampleCorridorSmoothPath(
            WaypointTrack track,
            float distanceAlongTrack,
            out Vector2 position,
            out Vector2 tangent,
            out float cornerAngleDeg)
        {
            int wpCount = track.WaypointCount;
            float totalLen = Mathf.Max(0.01f, track.TotalLength);
            float s = ((distanceAlongTrack % totalLen) + totalLen) % totalLen;

            int seg = 0;
            for (int i = 0; i < wpCount; i++)
            {
                float d0 = track.GetDistanceAtWaypoint(i);
                float d1 = i + 1 < wpCount ? track.GetDistanceAtWaypoint(i + 1) : totalLen;
                if (s >= d0 && s <= d1)
                {
                    seg = i;
                    break;
                }
            }

            float segStart = track.GetDistanceAtWaypoint(seg);
            float segEnd = seg + 1 < wpCount ? track.GetDistanceAtWaypoint(seg + 1) : totalLen;
            float segLen = Mathf.Max(0.01f, segEnd - segStart);
            float localDist = Mathf.Clamp(s - segStart, 0f, segLen);

            Vector2 a = track.GetWaypoint(seg);
            Vector2 b = track.GetWaypoint(seg + 1);
            Vector2 dirSeg = (b - a).sqrMagnitude > 0.0001f ? (b - a).normalized : Vector2.right;
            Vector2 dirPrev = track.GetTrackDirection(seg - 1);
            Vector2 dirNext = track.GetTrackDirection(seg + 1);

            float angleStart = Vector2.Angle(dirPrev, dirSeg);
            float angleEnd = Vector2.Angle(dirSeg, dirNext);
            cornerAngleDeg = Mathf.Max(angleStart, angleEnd);

            // Quadratic Bezier corner fillet inside the road corridor (bounded so it never cuts grass or overshoots outer wall)
            float maxFillet = Mathf.Min(segLen * 0.38f, track.TrackWidth * 0.72f);
            float filletStart = angleStart > 12f ? maxFillet * Mathf.Clamp01(angleStart / 75f) : 0f;
            float filletEnd = angleEnd > 12f ? maxFillet * Mathf.Clamp01(angleEnd / 75f) : 0f;

            if (filletStart > 0.5f && localDist < filletStart)
            {
                float t = 0.5f + 0.5f * (localDist / filletStart);
                Vector2 p0 = a - dirPrev * filletStart;
                Vector2 p2 = a + dirSeg * filletStart;
                position = EvalQuadBezier(p0, a, p2, t);
                Vector2 bezTan = EvalQuadBezierTangent(p0, a, p2, t);
                tangent = bezTan.sqrMagnitude > 0.0001f ? bezTan.normalized : dirSeg;
            }
            else if (filletEnd > 0.5f && localDist > segLen - filletEnd)
            {
                float t = 0.5f * ((localDist - (segLen - filletEnd)) / filletEnd);
                Vector2 p0 = b - dirSeg * filletEnd;
                Vector2 p2 = b + dirNext * filletEnd;
                position = EvalQuadBezier(p0, b, p2, t);
                Vector2 bezTan = EvalQuadBezierTangent(p0, b, p2, t);
                tangent = bezTan.sqrMagnitude > 0.0001f ? bezTan.normalized : dirSeg;
            }
            else
            {
                position = Vector2.Lerp(a, b, localDist / segLen);
                tangent = dirSeg;
            }
        }

        private static Vector2 EvalQuadBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Vector2 EvalQuadBezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return 2f * u * (p1 - p0) + 2f * t * (p2 - p1);
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

            float scaledAvoidRange = Mathf.Clamp(avoidanceLateralRange * (track.LaneWidth / 4f), 2.1f, 4.8f);
            float scaledAvoidShift = Mathf.Clamp(track.LaneWidth * 1.05f, 2.2f, 5.8f);
            float edgeAvoidThreshold = Mathf.Max(
                track.LaneWidth * 0.45f,
                (track.LaneCount - 1) * track.LaneWidth * 0.25f);

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
                    if (Mathf.Abs(dLat) < scaledAvoidRange)
                    {
                        bestDz = dz;
                        // Steer away from the blocking bike toward the side with more road space
                        if (Mathf.Abs(other.LateralOffset) > edgeAvoidThreshold)
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

            return avoidSign * scaledAvoidShift;
        }

        private static float SignedAngleRad(Vector2 from, Vector2 to)
        {
            float cross = from.x * to.y - from.y * to.x;
            float dot = Vector2.Dot(from, to);
            return Mathf.Atan2(cross, dot);
        }
    }
}
