using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    [Serializable]
    public class TrackObstacle
    {
        public int Id;
        public ObstacleKind Kind;
        public float TrackDistance;
        public float LateralOffset;
        public float HitRadius;
        public float SpeedMultiplierOnHit;
        public float LateralKnockImpulse;
        public bool IsSurfaceHazard;
        public float BackwardKnockbackMeters;
        public float BackwardVelocityImpulse;
        public float RespawnTimer;
        public Sprite CustomSprite;

        public bool IsActive => RespawnTimer <= 0f;
        public bool IsOnCooldown => RespawnTimer > 0f;

        public RoadsidePropKind VisualPropKind => Kind switch
        {
            ObstacleKind.TrafficCone => RoadsidePropKind.TrafficCone,
            ObstacleKind.OilBarrel => RoadsidePropKind.OilBarrel,
            ObstacleKind.RoadBarrier => RoadsidePropKind.RoadBarrier,
            ObstacleKind.OilSlick => RoadsidePropKind.OilSlick,
            ObstacleKind.ParkedBus => RoadsidePropKind.ParkedBus,
            ObstacleKind.Pothole => RoadsidePropKind.Pothole,
            _ => RoadsidePropKind.TrafficCone
        };
    }

    /// <summary>
    /// Hệ thống sinh chướng ngại vật ngẫu nhiên trên mặt đường đua sử dụng IObstacleFactory (GoF Creational Factory + OCP)
    /// và xử lý va chạm làm giảm tốc / lệch hướng xe.
    /// </summary>
    public class TrackObstacleSystem : MonoBehaviour
    {
        [Header("Cấu hình Spawn Chướng Ngại Vật")]
        [SerializeField] private bool enableObstacles = true;
        [Range(0, 40)]
        [SerializeField] private int obstacleCount = 14;
        [Min(20f)]
        [SerializeField] private float startSafeZoneMeters = 65f;
        [Min(15f)]
        [SerializeField] private float minSpacingMeters = 32f;
        [SerializeField] private float respawnCooldownSeconds = 4.5f;

        [Header("Thông số Va Chạm")]
        [SerializeField] private float defaultHitRadius = 1.65f;
        [Range(0.3f, 0.9f)]
        [SerializeField] private float defaultSpeedPenalty = 0.62f;
        [SerializeField] private float defaultLateralImpulse = 8.5f;

        [Header("Seed Ngẫu Nhiên")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 2026;

        [Header("Sprite Chướng Ngại Vật Tùy Chỉnh (Tùy chọn)")]
        [SerializeField] private Sprite[] customObstacleSprites;

        private readonly List<TrackObstacle> _obstacles = new List<TrackObstacle>();
        private ObstacleKind[] _allowedKindsOverride;
        private IObstacleFactory _obstacleFactory = new TrackObstacleFactory();

        public event Action<Car, TrackObstacle> ObstacleHit;

        public IObstacleFactory ObstacleFactory => _obstacleFactory;

        public bool EnableObstacles
        {
            get => enableObstacles;
            set => enableObstacles = value;
        }

        public int ObstacleCount
        {
            get => obstacleCount;
            set => obstacleCount = Mathf.Clamp(value, 0, 60);
        }

        public IReadOnlyList<TrackObstacle> Obstacles => _obstacles;

        public void SetObstacleFactory(IObstacleFactory factory)
        {
            _obstacleFactory = factory ?? new TrackObstacleFactory();
        }

        public void ApplyMapSettings(TrackDefinition def)
        {
            if (def == null)
            {
                _allowedKindsOverride = null;
                return;
            }

            obstacleCount = def.ObstacleCount;
            minSpacingMeters = def.ObstacleMinSpacingMeters;
            _allowedKindsOverride = (def.AllowedObstacles != null && def.AllowedObstacles.Length > 0)
                ? def.AllowedObstacles
                : null;
        }

        public void SpawnObstacles(WaypointTrack track, int? overrideSeed = null)
        {
            _obstacles.Clear();
            if (!enableObstacles || track == null || track.TotalLength <= startSafeZoneMeters + 40f || obstacleCount <= 0)
            {
                return;
            }

            _obstacleFactory ??= new TrackObstacleFactory();
            int seedToUse = overrideSeed ?? (useFixedSeed ? fixedSeed : Environment.TickCount);
            var rng = new System.Random(seedToUse);

            float totalLen = track.TotalLength;
            float usableStart = startSafeZoneMeters;
            float usableEnd = totalLen - 25f;
            float usableSpan = Mathf.Max(40f, usableEnd - usableStart);
            int laneCount = track.LaneCount;
            float maxLaneJitter = track.LaneWidth * 0.22f;
            float maxBound = track.MaxLaneCenterOffset + maxLaneJitter;

            ObstacleKind[] allKinds = _allowedKindsOverride != null && _allowedKindsOverride.Length > 0
                ? _allowedKindsOverride
                : new[]
                {
                    ObstacleKind.TrafficCone,
                    ObstacleKind.OilBarrel,
                    ObstacleKind.RoadBarrier,
                    ObstacleKind.OilSlick,
                    ObstacleKind.Pothole
                };

            float segmentStep = usableSpan / obstacleCount;
            float lastPlacedS = -999f;

            for (int i = 0; i < obstacleCount; i++)
            {
                float bandMin = usableStart + i * segmentStep;
                float bandMax = Mathf.Min(usableEnd, bandMin + segmentStep * 0.85f);
                float t = (float)rng.NextDouble();
                float s = Mathf.Lerp(bandMin, Mathf.Max(bandMin, bandMax), t);

                if (s - lastPlacedS < minSpacingMeters)
                {
                    s = Mathf.Min(usableEnd, lastPlacedS + minSpacingMeters);
                }
                lastPlacedS = s;

                ObstacleKind kind = allKinds[rng.Next(0, allKinds.Length)];
                ObstacleSpec spec = _obstacleFactory.CreateSpec(kind, defaultHitRadius, defaultSpeedPenalty, defaultLateralImpulse);

                bool inFeature = track.TryGetFeatureAtDistance(s, out TrackFeatureKind featureKind, out _);
                if (spec.AvoidTunnelAndOverpass && inFeature && (featureKind == TrackFeatureKind.Tunnel || featureKind == TrackFeatureKind.Overpass))
                {
                    // Không đặt vật cản cỡ lớn chắn trong hầm hẹp hoặc trên mặt cầu vượt
                    kind = ObstacleKind.TrafficCone;
                    spec = _obstacleFactory.CreateSpec(kind, defaultHitRadius, defaultSpeedPenalty, defaultLateralImpulse);
                }

                float lateral;
                if (spec.OuterLaneOnly)
                {
                    int outerLaneIdx = rng.Next(0, 2) == 0 ? 0 : (laneCount - 1);
                    float edgePush = (outerLaneIdx == 0 ? -1f : 1f) * (track.LaneWidth * 0.12f);
                    lateral = Mathf.Clamp(track.GetLaneCenterOffset(outerLaneIdx) + edgePush, -maxBound, maxBound);
                }
                else
                {
                    int laneIdx = rng.Next(0, laneCount);
                    float laneCenter = track.GetLaneCenterOffset(laneIdx);
                    float jitterUnit = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lateral = Mathf.Clamp(laneCenter + jitterUnit * maxLaneJitter, -maxBound, maxBound);
                }

                Sprite customSpr = ResolveCustomObstacleSprite(kind);

                _obstacles.Add(new TrackObstacle
                {
                    Id = i,
                    Kind = kind,
                    TrackDistance = s,
                    LateralOffset = lateral,
                    HitRadius = spec.HitRadius,
                    SpeedMultiplierOnHit = spec.SpeedMultiplierOnHit,
                    LateralKnockImpulse = spec.LateralKnockImpulse,
                    IsSurfaceHazard = spec.IsSurfaceHazard,
                    BackwardKnockbackMeters = spec.BackwardKnockbackMeters,
                    BackwardVelocityImpulse = spec.BackwardVelocityImpulse,
                    RespawnTimer = 0f,
                    CustomSprite = customSpr
                });
            }
        }

        public TrackObstacle AddManualObstacle(
            ObstacleKind kind,
            float trackDistance,
            float lateralOffset,
            float hitRadius = -1f)
        {
            _obstacleFactory ??= new TrackObstacleFactory();
            ObstacleSpec spec = _obstacleFactory.CreateSpec(kind, defaultHitRadius, defaultSpeedPenalty, defaultLateralImpulse);
            var obs = new TrackObstacle
            {
                Id = _obstacles.Count,
                Kind = kind,
                TrackDistance = trackDistance,
                LateralOffset = lateralOffset,
                HitRadius = hitRadius > 0f ? hitRadius : spec.HitRadius,
                SpeedMultiplierOnHit = spec.SpeedMultiplierOnHit,
                LateralKnockImpulse = spec.LateralKnockImpulse,
                IsSurfaceHazard = spec.IsSurfaceHazard,
                BackwardKnockbackMeters = spec.BackwardKnockbackMeters,
                BackwardVelocityImpulse = spec.BackwardVelocityImpulse,
                RespawnTimer = 0f,
                CustomSprite = ResolveCustomObstacleSprite(kind)
            };
            _obstacles.Add(obs);
            return obs;
        }

        public void SetCustomObstacleSprites(Sprite[] sprites)
        {
            customObstacleSprites = sprites;
        }

        private Sprite ResolveCustomObstacleSprite(ObstacleKind kind)
        {
            if (customObstacleSprites == null || customObstacleSprites.Length == 0)
            {
                return null;
            }

            int index = (int)kind;
            if (index >= 0 && index < customObstacleSprites.Length)
            {
                return customObstacleSprites[index];
            }

            return null;
        }

        public void StepObstacles(WaypointTrack track, IReadOnlyList<Car> cars, float dt)
        {
            if (!enableObstacles || track == null || _obstacles.Count == 0) return;

            float totalLen = track.TotalLength;
            if (totalLen <= 1f) return;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                TrackObstacle obs = _obstacles[i];
                if (obs.RespawnTimer > 0f)
                {
                    obs.RespawnTimer = Mathf.Max(0f, obs.RespawnTimer - dt);
                }
            }

            if (cars == null) return;

            for (int c = 0; c < cars.Count; c++)
            {
                Car car = cars[c];
                if (car == null) continue;

                car.ConsumeOrTickObstacleImmunity(dt);
                if (car.IsObstacleImmune) continue;

                for (int i = 0; i < _obstacles.Count; i++)
                {
                    TrackObstacle obs = _obstacles[i];
                    if (!obs.IsActive) continue;

                    float dz = obs.TrackDistance - car.TrackDistance;
                    if (dz < -totalLen * 0.5f) dz += totalLen;
                    if (dz > totalLen * 0.5f) dz -= totalLen;

                    if (Mathf.Abs(dz) <= obs.HitRadius)
                    {
                        float dLat = obs.LateralOffset - car.LateralOffset;
                        if (Mathf.Abs(dLat) <= obs.HitRadius)
                        {
                            ApplyObstacleImpact(car, obs, track);
                            obs.RespawnTimer = Mathf.Max(0.5f, respawnCooldownSeconds);
                            ObstacleHit?.Invoke(car, obs);
                            break;
                        }
                    }
                }
            }
        }

        public static Sprite GetObstacleSprite(TrackObstacle obstacle)
        {
            if (obstacle != null && obstacle.CustomSprite != null)
            {
                return obstacle.CustomSprite;
            }

            ObstacleKind kind = obstacle != null ? obstacle.Kind : ObstacleKind.TrafficCone;
            return kind switch
            {
                ObstacleKind.TrafficCone => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.TrafficCone),
                ObstacleKind.OilBarrel => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.OilBarrel),
                ObstacleKind.RoadBarrier => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.RoadBarrier),
                ObstacleKind.OilSlick => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.OilSlick),
                ObstacleKind.ParkedBus => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.ParkedBus),
                ObstacleKind.Pothole => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.Pothole),
                _ => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.TrafficCone)
            };
        }

        private static void ApplyObstacleImpact(Car car, TrackObstacle obs, WaypointTrack track)
        {
            bool isSurfaceHazard = obs.IsSurfaceHazard ||
                                   obs.Kind == ObstacleKind.Pothole ||
                                   obs.Kind == ObstacleKind.OilSlick;
            if (isSurfaceHazard)
            {
                car.ApplySurfaceHazardSlowdown(obs.SpeedMultiplierOnHit, 0.45f);
                return;
            }

            int pushSide = car.LateralOffset >= obs.LateralOffset ? 1 : -1;
            float backMeters = obs.BackwardKnockbackMeters > 0f ? obs.BackwardKnockbackMeters : 2.6f;
            float backImpulse = obs.BackwardVelocityImpulse > 0f ? obs.BackwardVelocityImpulse : 12.0f;
            car.ApplySolidObstacleKnockback(
                pushSide,
                track,
                obs.SpeedMultiplierOnHit,
                obs.LateralKnockImpulse,
                backMeters,
                backImpulse,
                0.55f);
        }
    }
}
