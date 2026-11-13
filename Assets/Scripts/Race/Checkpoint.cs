using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// A directional gate across the track. A checkpoint is only crossed when a racer's
    /// movement segment intersects the gate line while travelling in the race direction.
    /// Immutable after construction (Encapsulation).
    /// </summary>
    public class Checkpoint
    {
        private readonly int _index;
        private readonly int _waypointIndex;
        private readonly Vector2 _position;
        private readonly Vector2 _forward;
        private readonly float _halfWidth;
        private readonly float _trackDistance;

        public int Index => _index;
        public int WaypointIndex => _waypointIndex;
        public Vector2 Position => _position;
        public Vector2 Forward => _forward;
        public Vector2 Right => new Vector2(_forward.y, -_forward.x);
        public float HalfWidth => _halfWidth;
        public float TrackDistance => _trackDistance;
        public bool IsFinishLine => _index == 0;

        public Vector2 GateStart => _position - Right * _halfWidth;
        public Vector2 GateEnd => _position + Right * _halfWidth;

        public Checkpoint(int index, int waypointIndex, Vector2 position, Vector2 forward, float halfWidth, float trackDistance)
        {
            _index = index;
            _waypointIndex = waypointIndex;
            _position = position;
            _forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
            _halfWidth = Mathf.Max(0.5f, halfWidth);
            _trackDistance = trackDistance;
        }

        /// <summary>
        /// Returns true if moving from <paramref name="from"/> to <paramref name="to"/> crosses
        /// the gate line in the forward (race) direction. Reverse crossings are rejected.
        /// </summary>
        public bool IsCrossedForward(Vector2 from, Vector2 to)
        {
            Vector2 move = to - from;
            if (move.sqrMagnitude < 0.000001f) return false;
            if (Vector2.Dot(move, _forward) <= 0f) return false;

            Vector2 a = GateStart;
            Vector2 gate = GateEnd - a;

            float denom = Cross(move, gate);
            if (Mathf.Abs(denom) < 0.000001f) return false;

            Vector2 diff = a - from;
            float t = Cross(diff, gate) / denom;  // along movement
            float u = Cross(diff, move) / denom;  // along gate
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
