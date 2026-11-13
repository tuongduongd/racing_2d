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
        [Header("Track Geometry")]
        [SerializeField] private Vector2[] waypoints;
        [SerializeField] private float trackWidth = 16f;
        [SerializeField] private float maxOffRoadWidth = 28f;

        private float[] _cumulativeDistances;
        private float _totalLength;
        private bool _cacheDirty = true;

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
            if (waypoints == null || waypoints.Length < 3)
            {
                BuildDefaultCircuit();
            }
            EnsureCache();
        }

        private void OnValidate()
        {
            _cacheDirty = true;
        }

        [ContextMenu("Build Default 22-Waypoint Circuit")]
        public void BuildDefaultCircuit()
        {
            waypoints = new Vector2[]
            {
                new Vector2(  0f,   0f),
                new Vector2( 90f,   0f),
                new Vector2(180f,   0f),
                new Vector2(260f,  15f),
                new Vector2(320f,  60f),
                new Vector2(345f, 125f),
                new Vector2(325f, 195f),
                new Vector2(270f, 240f),
                new Vector2(200f, 255f),
                new Vector2(145f, 220f),
                new Vector2(115f, 165f),
                new Vector2( 70f, 145f),
                new Vector2( 20f, 175f),
                new Vector2(-25f, 235f),
                new Vector2(-95f, 265f),
                new Vector2(-175f, 245f),
                new Vector2(-225f, 185f),
                new Vector2(-235f, 110f),
                new Vector2(-200f,  45f),
                new Vector2(-140f,   8f),
                new Vector2( -75f,  -4f),
                new Vector2( -35f,   0f),
            };
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
            float curvature = angleDiff / step;

            float normalizedS = (s / _totalLength) * Mathf.PI * 2f;
            float elevation = Mathf.Sin(normalizedS * 3f) * 3.5f + Mathf.Cos(normalizedS * 5f) * 1.5f;

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
