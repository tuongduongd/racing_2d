using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public enum ObstacleKind
    {
        TrafficCone = 0,
        OilBarrel = 1,
        RoadBarrier = 2,
        OilSlick = 3,
        ParkedBus = 4
    }

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
            _ => RoadsidePropKind.TrafficCone
        };
    }

    /// <summary>
    /// Hệ thống sinh chướng ngại vật ngẫu nhiên trên mặt đường đua (chóp nón, thùng phuy,
    /// rào chắn công trường, vũng dầu) và xử lý va chạm làm giảm tốc / lệch hướng xe.
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

        public event Action<Car, TrackObstacle> ObstacleHit;

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
                    ObstacleKind.OilSlick
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
                bool inFeature = track.TryGetFeatureAtDistance(s, out TrackFeatureKind featureKind, out _);
                if (kind == ObstacleKind.ParkedBus && inFeature && (featureKind == TrackFeatureKind.Tunnel || featureKind == TrackFeatureKind.Overpass))
                {
                    // Không đặt xe buýt đỗ chắn trong hầm hẹp hoặc trên mặt cầu vượt
                    kind = ObstacleKind.TrafficCone;
                }

                float lateral;
                if (kind == ObstacleKind.ParkedBus)
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

                GetKindParameters(kind, out float radius, out float speedMult, out float knockImpulse);

                Sprite customSpr = null;
                if (customObstacleSprites != null && customObstacleSprites.Length > 0)
                {
                    int idx = (int)kind % customObstacleSprites.Length;
                    customSpr = customObstacleSprites[idx];
                }

                _obstacles.Add(new TrackObstacle
                {
                    Id = i,
                    Kind = kind,
                    TrackDistance = s,
                    LateralOffset = lateral,
                    HitRadius = radius,
                    SpeedMultiplierOnHit = speedMult,
                    LateralKnockImpulse = knockImpulse,
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
            GetKindParameters(kind, out float defaultRad, out float speedMult, out float knockImpulse);
            var obs = new TrackObstacle
            {
                Id = _obstacles.Count,
                Kind = kind,
                TrackDistance = trackDistance,
                LateralOffset = lateralOffset,
                HitRadius = hitRadius > 0f ? hitRadius : defaultRad,
                SpeedMultiplierOnHit = speedMult,
                LateralKnockImpulse = knockImpulse,
                RespawnTimer = 0f
            };
            _obstacles.Add(obs);
            return obs;
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
                _ => RoadRashPropSprites.GetPropSprite(RoadsidePropKind.TrafficCone)
            };
        }

        private void GetKindParameters(ObstacleKind kind, out float radius, out float speedMult, out float knockImpulse)
        {
            switch (kind)
            {
                case ObstacleKind.TrafficCone:
                    radius = defaultHitRadius * 0.90f;
                    speedMult = Mathf.Clamp(defaultSpeedPenalty + 0.14f, 0.4f, 0.9f);
                    knockImpulse = defaultLateralImpulse * 0.65f;
                    break;
                case ObstacleKind.OilBarrel:
                    radius = defaultHitRadius * 1.05f;
                    speedMult = defaultSpeedPenalty;
                    knockImpulse = defaultLateralImpulse * 1.15f;
                    break;
                case ObstacleKind.RoadBarrier:
                    radius = defaultHitRadius * 1.25f;
                    speedMult = Mathf.Clamp(defaultSpeedPenalty - 0.08f, 0.35f, 0.85f);
                    knockImpulse = defaultLateralImpulse * 1.25f;
                    break;
                case ObstacleKind.ParkedBus:
                    radius = defaultHitRadius * 1.32f;
                    speedMult = Mathf.Clamp(defaultSpeedPenalty - 0.18f, 0.32f, 0.72f);
                    knockImpulse = defaultLateralImpulse * 1.35f;
                    break;
                default: // OilSlick
                    radius = defaultHitRadius * 1.15f;
                    speedMult = Mathf.Clamp(defaultSpeedPenalty + 0.08f, 0.4f, 0.9f);
                    knockImpulse = defaultLateralImpulse * 1.30f;
                    break;
            }
        }

        private static void ApplyObstacleImpact(Car car, TrackObstacle obs, WaypointTrack track)
        {
            Vector2 segDir = track.GetTrackDirection(car.CurrentWaypointIndex);
            Vector2 segRight = new Vector2(segDir.y, -segDir.x);
            int pushSide = car.LateralOffset >= obs.LateralOffset ? 1 : -1;

            car.ReceiveKick(pushSide, segRight * (pushSide * obs.LateralKnockImpulse), 0.32f);
            car.Physics.ScaleVelocity(obs.SpeedMultiplierOnHit);
        }
    }
}
