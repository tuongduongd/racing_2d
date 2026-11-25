using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public struct TrackProjection
    {
        public int segmentIndex;
        public float distanceAlongTrack;
        public float lateralOffset;
        public Vector2 tangent;
        public Vector2 closestPoint;
        public bool isOnTrack;
    }

    public struct TrackSample
    {
        public Vector2 position;
        public Vector2 tangent;
        public float curvature;
        public float elevation;
    }

    /// <summary>
    /// Represents a closed-loop racing circuit using 15-30 waypoints.
    /// Encapsulates waypoint data and exposes controlled track queries.
    /// </summary>
    public class WaypointTrack : MonoBehaviour
    {
        [Header("Map Data (TrackDefinition)")]
        [SerializeField] private TrackDefinition trackDefinition;

        [Header("Track Geometry")]
        [SerializeField] private Vector2[] waypoints;
        [Range(2, 6)]
        [SerializeField] private int laneCount = 4;
        [Min(2.4f)]
        [SerializeField] private float laneWidth = 4f;
        [SerializeField] private float trackWidth = 16f;
        [SerializeField] private float maxOffRoadWidth = 25f;

        private float _elevationAmplitude = 3.5f;
        private float _elevationFrequency = 3.0f;
        private float _elevationSecondaryAmp = 1.5f;
        private TrackFeatureZone[] _featureZones = System.Array.Empty<TrackFeatureZone>();

        private float[] _cumulativeDistances;
        private float _totalLength;
        private bool _cacheDirty = true;

        public TrackDefinition ActiveDefinition => trackDefinition;
        public MapTheme ActiveTheme => trackDefinition != null ? trackDefinition.Theme : null;
        public TrackFeatureZone[] FeatureZones => _featureZones;
        public int LaneCount => Mathf.Clamp(laneCount, 2, 6);
        public float LaneWidth => Mathf.Max(2.4f, laneWidth);
        public float MaxLaneCenterOffset => (LaneCount - 1) * 0.5f * LaneWidth;
        public IReadOnlyList<Vector2> Waypoints => waypoints;
        public float TrackWidth => trackWidth;
        public float MaxOffRoadWidth => maxOffRoadWidth;
        public int WaypointCount => waypoints != null ? waypoints.Length : 0;

        public float TotalLength
        {
            get
            {
                EnsureCache();
                return _totalLength;
            }
        }

        private void Awake()
        {
            if (trackDefinition != null)
            {
                ApplyTrackDefinition(trackDefinition);
            }
            else if (waypoints == null || waypoints.Length < 3)
            {
                BuildDefaultCircuit();
            }
            EnsureCache();
        }

        public void ApplyTrackDefinition(TrackDefinition definition)
        {
            if (definition == null) return;

            trackDefinition = definition;
            if (definition.Waypoints != null && definition.Waypoints.Length >= 3)
            {
                waypoints = (Vector2[])definition.Waypoints.Clone();
            }
            else if (waypoints == null || waypoints.Length < 3)
            {
                waypoints = TrackDefinition.GetDefaultCityWaypoints();
            }

            laneCount = definition.LaneCount;
            laneWidth = definition.LaneWidth;
            trackWidth = laneCount * laneWidth;
            maxOffRoadWidth = definition.MaxOffRoadWidth;
            _elevationAmplitude = definition.ElevationAmplitude;
            _elevationFrequency = definition.ElevationFrequency;
            _elevationSecondaryAmp = definition.ElevationSecondaryAmp;
            _featureZones = definition.FeatureZones ?? System.Array.Empty<TrackFeatureZone>();

            _cacheDirty = true;
            EnsureCache();
        }

        public bool TryGetFeatureAtDistance(float distanceAlongTrack, out TrackFeatureKind kind, out float zoneProgress)
        {
            kind = TrackFeatureKind.None;
            zoneProgress = 0f;
            if (_featureZones == null || _featureZones.Length == 0 || TotalLength <= 0.01f)
            {
                return false;
            }

            float norm = WrapDistance(distanceAlongTrack) / _totalLength;
            for (int i = 0; i < _featureZones.Length; i++)
            {
                TrackFeatureZone z = _featureZones[i];
                if (z == null || z.kind == TrackFeatureKind.None) continue;
                float start = Mathf.Clamp01(Mathf.Min(z.startNormalized, z.endNormalized));
                float end = Mathf.Clamp01(Mathf.Max(z.startNormalized, z.endNormalized));
                if (norm >= start && norm <= end && end > start)
                {
                    kind = z.kind;
                    zoneProgress = Mathf.Clamp01((norm - start) / (end - start));
                    return true;
                }
            }
            return false;
        }

        private void OnValidate()
        {
            laneCount = Mathf.Clamp(laneCount, 2, 6);
            laneWidth = Mathf.Max(2.4f, laneWidth);
            if (trackDefinition != null)
            {
                ApplyTrackDefinition(trackDefinition);
                return;
            }

            trackWidth = laneCount * laneWidth;
            _cacheDirty = true;
        }

        [ContextMenu("Build Default 22-Waypoint Circuit")]
        public void BuildDefaultCircuit()
        {
            if (trackDefinition != null)
            {
                ApplyTrackDefinition(trackDefinition);
                return;
            }

            waypoints = TrackDefinition.GetDefaultValleyWaypoints();
            laneCount = 4;
            laneWidth = 4f;
            trackWidth = laneCount * laneWidth;
            maxOffRoadWidth = 25f;
            _elevationAmplitude = 3.5f;
            _elevationFrequency = 3.0f;
            _elevationSecondaryAmp = 1.5f;
            _featureZones = System.Array.Empty<TrackFeatureZone>();
            _cacheDirty = true;
            EnsureCache();
        }

        public Vector2 GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Length == 0) return Vector2.zero;
            int safe = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
            return waypoints[safe];
        }

        /// <summary>
        /// Distance along the closed track (meters) at which the given waypoint lies.
        /// </summary>
        public float GetDistanceAtWaypoint(int index)
        {
            EnsureCache();
            if (waypoints == null || waypoints.Length == 0) return 0f;
            int safe = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
            return _cumulativeDistances[safe];
        }

        /// <summary>
        /// Smoothed direction at a waypoint (average of incoming and outgoing segment directions).
        /// </summary>
        public Vector2 GetDirectionAtWaypoint(int index)
        {
            Vector2 dir = GetTrackDirection(index - 1) + GetTrackDirection(index);
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : GetTrackDirection(index);
        }

        public Vector2 GetNextWaypoint(int currentIndex)
        {
            return GetWaypoint(currentIndex + 1);
        }

        public Vector2 GetTrackDirection(int index)
        {
            if (waypoints == null || waypoints.Length < 2) return Vector2.right;
            int count = waypoints.Length;
            int i0 = ((index % count) + count) % count;
            int i1 = (i0 + 1) % count;
            Vector2 dir = waypoints[i1] - waypoints[i0];
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        }

        /// <summary>
        /// Returns the lateral center offset (in meters, negative = left, positive = right)
        /// for lane index in [0 .. LaneCount - 1].
        /// </summary>
        public float GetLaneCenterOffset(int laneIndex)
        {
            int count = LaneCount;
            int safeIdx = Mathf.Clamp(laneIndex, 0, count - 1);
            return (-(count - 1) * 0.5f + safeIdx) * LaneWidth;
        }

        public bool IsOnTrack(Vector2 pos)
        {
            return ProjectPosition(pos).isOnTrack;
        }

        public TrackProjection ProjectPosition(Vector2 pos)
        {
            EnsureCache();
            TrackProjection result = new TrackProjection
            {
                segmentIndex = 0,
                distanceAlongTrack = 0f,
                lateralOffset = 0f,
                tangent = Vector2.right,
                closestPoint = pos,
                isOnTrack = false
            };

            if (waypoints == null || waypoints.Length < 2) return result;

            int count = waypoints.Length;
            float bestDistSq = float.MaxValue;
            float halfWidth = trackWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = waypoints[i];
                Vector2 b = waypoints[(i + 1) % count];
                Vector2 ab = b - a;
                float lenSq = ab.sqrMagnitude;
                float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(pos - a, ab) / lenSq) : 0f;
                Vector2 closest = a + ab * t;
                float distSq = (pos - closest).sqrMagnitude;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    Vector2 tangent = lenSq > 0.0001f ? ab.normalized : Vector2.right;
                    Vector2 right = new Vector2(tangent.y, -tangent.x);
                    float segLen = Mathf.Sqrt(lenSq);

                    result.segmentIndex = i;
                    result.closestPoint = closest;
                    result.tangent = tangent;
                    result.distanceAlongTrack = _cumulativeDistances[i] + t * segLen;
                    result.lateralOffset = Vector2.Dot(pos - closest, right);
                    result.isOnTrack = distSq <= halfWidth * halfWidth;
                }
            }

            return result;
        }

        public TrackSample SampleAtDistance(float s)
        {
            EnsureCache();
            if (waypoints == null || waypoints.Length < 3 || _totalLength <= 0.01f)
            {
                return new TrackSample
                {
                    position = Vector2.zero,
                    tangent = Vector2.right,
                    curvature = 0f,
                    elevation = 0f
                };
            }

            s = WrapDistance(s);
            int count = waypoints.Length;

            int seg = 0;
            for (int i = 0; i < count; i++)
            {
                if (s >= _cumulativeDistances[i] && s <= _cumulativeDistances[i + 1])
                {
                    seg = i;
                    break;
                }
            }

            float segStart = _cumulativeDistances[seg];
            float segEnd = _cumulativeDistances[seg + 1];
            float segLen = Mathf.Max(0.001f, segEnd - segStart);
            float t = Mathf.Clamp01((s - segStart) / segLen);

            Vector2 p0 = waypoints[(seg - 1 + count) % count];
            Vector2 p1 = waypoints[seg];
            Vector2 p2 = waypoints[(seg + 1) % count];
            Vector2 p3 = waypoints[(seg + 2) % count];

            Vector2 pos = CatmullRom(p0, p1, p2, p3, t);
            Vector2 tangent = CatmullRomTangent(p0, p1, p2, p3, t).normalized;

            float step = 4f;
            Vector2 tanNext = SampleTangentRaw(s + step);
            float angleDiff = SignedAngleRad(tangent, tanNext);
            // Positive curvature = right turn (CW), negative = left turn (CCW), matching visual curve projection & chevrons
            float curvature = -angleDiff / step;

            float normalizedS = (s / _totalLength) * Mathf.PI * 2f;
            float elevation = Mathf.Sin(normalizedS * _elevationFrequency) * _elevationAmplitude +
                              Mathf.Cos(normalizedS * (_elevationFrequency + 2f)) * _elevationSecondaryAmp;

            if (TryGetFeatureAtDistance(s, out TrackFeatureKind featKind, out float zoneProgress) &&
                featKind == TrackFeatureKind.Overpass)
            {
                float boost = GetOverpassBoostAt(s);
                elevation += Mathf.Sin(zoneProgress * Mathf.PI) * boost;
            }

            return new TrackSample
            {
                position = pos,
                tangent = tangent,
                curvature = curvature,
                elevation = elevation
            };
        }

        public float WrapDistance(float s)
        {
            EnsureCache();
            if (_totalLength <= 0.001f) return 0f;
            s %= _totalLength;
            if (s < 0f) s += _totalLength;
            return s;
        }

        public int GetSegmentIndexAtDistance(float s)
        {
            EnsureCache();
            if (waypoints == null || waypoints.Length < 2 || _totalLength <= 0.001f)
            {
                return 0;
            }

            float wrapped = WrapDistance(s);
            int count = waypoints.Length;
            for (int i = 0; i < count; i++)
            {
                if (wrapped >= _cumulativeDistances[i] && wrapped <= _cumulativeDistances[i + 1])
                {
                    return i;
                }
            }
            return count - 1;
        }

        /// <summary>
        /// Evaluates a smooth continuous world position from continuous Frenet track coordinates
        /// (distanceAlongTrack, lateralOffset) without snapping across polygonal segment corners.
        /// </summary>
        public Vector2 EvaluateWorldPosition(
            float distanceAlongTrack,
            float lateralOffset,
            out int segmentIndex,
            out Vector2 sampleTangent)
        {
            EnsureCache();
            if (waypoints == null || waypoints.Length < 2 || _totalLength <= 0.001f)
            {
                segmentIndex = 0;
                sampleTangent = Vector2.right;
                return new Vector2(distanceAlongTrack, -lateralOffset);
            }

            float wrapped = WrapDistance(distanceAlongTrack);
            int count = waypoints.Length;
            int seg = GetSegmentIndexAtDistance(wrapped);
            float segStart = _cumulativeDistances[seg];
            float segEnd = _cumulativeDistances[seg + 1];
            float t = Mathf.Clamp01((wrapped - segStart) / Mathf.Max(0.001f, segEnd - segStart));

            Vector2 basePos = Vector2.Lerp(waypoints[seg], waypoints[(seg + 1) % count], t);
            Vector2 tan = SampleTangentRaw(wrapped);
            if (tan.sqrMagnitude < 0.0001f)
            {
                tan = GetTrackDirection(seg);
            }
            tan.Normalize();
            Vector2 right = new Vector2(tan.y, -tan.x);

            segmentIndex = seg;
            sampleTangent = tan;
            return basePos + right * lateralOffset;
        }

        private float GetOverpassBoostAt(float s)
        {
            if (_featureZones == null || _totalLength <= 0.01f) return 0f;
            float norm = WrapDistance(s) / _totalLength;
            for (int i = 0; i < _featureZones.Length; i++)
            {
                TrackFeatureZone z = _featureZones[i];
                if (z != null && z.kind == TrackFeatureKind.Overpass &&
                    norm >= z.startNormalized && norm <= z.endNormalized)
                {
                    return Mathf.Max(0f, z.elevationBoostMeters);
                }
            }
            return 0f;
        }

        private Vector2 SampleTangentRaw(float s)
        {
            s = WrapDistance(s);
            int count = waypoints.Length;
            int seg = 0;
            for (int i = 0; i < count; i++)
            {
                if (s >= _cumulativeDistances[i] && s <= _cumulativeDistances[i + 1])
                {
                    seg = i;
                    break;
                }
            }
            float segStart = _cumulativeDistances[seg];
            float segEnd = _cumulativeDistances[seg + 1];
            float t = Mathf.Clamp01((s - segStart) / Mathf.Max(0.001f, segEnd - segStart));
            Vector2 p0 = waypoints[(seg - 1 + count) % count];
            Vector2 p1 = waypoints[seg];
            Vector2 p2 = waypoints[(seg + 1) % count];
            Vector2 p3 = waypoints[(seg + 2) % count];
            Vector2 tan = CatmullRomTangent(p0, p1, p2, p3, t);
            return tan.sqrMagnitude > 0.0001f ? tan.normalized : Vector2.right;
        }

        private void EnsureCache()
        {
            if (!_cacheDirty && _cumulativeDistances != null) return;
            if (waypoints == null || waypoints.Length < 2)
            {
                _cumulativeDistances = new float[] { 0f };
                _totalLength = 0f;
                _cacheDirty = false;
                return;
            }

            int count = waypoints.Length;
            _cumulativeDistances = new float[count + 1];
            _cumulativeDistances[0] = 0f;
            float acc = 0f;
            for (int i = 0; i < count; i++)
            {
                acc += Vector2.Distance(waypoints[i], waypoints[(i + 1) % count]);
                _cumulativeDistances[i + 1] = acc;
            }
            _totalLength = acc;
            _cacheDirty = false;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector2 CatmullRomTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
            );
        }

        private static float SignedAngleRad(Vector2 from, Vector2 to)
        {
            float cross = from.x * to.y - from.y * to.x;
            float dot = Vector2.Dot(from, to);
            return Mathf.Atan2(cross, dot);
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length < 2) return;
            int count = waypoints.Length;
            float halfW = trackWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = waypoints[i];
                Vector2 b = waypoints[(i + 1) % count];
                Vector2 dir = (b - a).normalized;
                Vector2 right = new Vector2(dir.y, -dir.x);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawSphere(a, 1.5f);

                Gizmos.color = Color.gray;
                Gizmos.DrawLine(a + right * halfW, b + right * halfW);
                Gizmos.DrawLine(a - right * halfW, b - right * halfW);
            }
        }
    }
}
