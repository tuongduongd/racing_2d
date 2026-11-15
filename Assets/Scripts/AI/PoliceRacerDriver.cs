using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// AI điều khiển xe Cảnh Sát (kế thừa từ Driver).
    /// Bám đuổi mục tiêu (xe người chơi), tự động tăng tốc bắt kịp khi bị bỏ xa,
    /// ép làn ngang khi áp sát và tung đòn cản người chơi.
    /// </summary>
    public class PoliceRacerDriver : Driver
    {
        [Header("Pursuit Navigation")]
        [SerializeField] private int targetWaypoint = 1;
        [SerializeField] private float waypointReachRadius = 14f;
        [SerializeField] private float maxSteerAngleDeg = 40f;

        [Header("Police Intercept Tuning")]
        [SerializeField] private float interceptRangeMeters = 28f;
        [SerializeField] private float strikeRangeLongitudinal = 3.2f;
        [SerializeField] private float strikeRangeLateral = 3.4f;
        [SerializeField] private float minCornerSpeed = 26f;
        [SerializeField] private float stuckSpeedThreshold = 2.5f;
        [SerializeField] private float stuckTimeout = 2.0f;

        private Car _targetPlayer;
        private float _stuckTimer;
        private bool _chaseActive = true;

        public Car TargetPlayer => _targetPlayer;
        public int TargetWaypointIndex => targetWaypoint;
        public bool IsChaseActive
        {
            get => _chaseActive;
            set => _chaseActive = value;
        }

        private void Awake()
        {
            SetDriverName("CẢNH SÁT");
        }

        public void ConfigurePolice(Car targetPlayer, string officerName = "CẢNH SÁT")
        {
            SetDriverName(officerName);
            _targetPlayer = targetPlayer;
            _chaseActive = true;
        }

        public void SetTargetPlayer(Car targetPlayer)
        {
            _targetPlayer = targetPlayer;
        }

        public override void OnReset()
        {
            targetWaypoint = 1;
            _stuckTimer = 0f;
        }

        public override VehicleInput DecideInput(Car car, WaypointTrack track)
        {
            if (car == null || track == null || track.WaypointCount < 2 || !_chaseActive)
            {
                return default;
            }

            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            int wpCount = track.WaypointCount;

            // 1. Stuck recovery
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

            // 2. Sync waypoint progression along track
            targetWaypoint = ((car.CurrentWaypointIndex + 1) % wpCount + wpCount) % wpCount;
            Vector2 wpPos = track.GetWaypoint(targetWaypoint);
            float distToWp = Vector2.Distance(car.Physics.Position, wpPos);
            if (distToWp <= waypointReachRadius)
            {
                targetWaypoint = (targetWaypoint + 1) % wpCount;
                wpPos = track.GetWaypoint(targetWaypoint);
            }

            // 3. Compute relative distance & lateral intercept toward target player
            float dzToPlayer = 0f;
            float targetLateral = 0f;
            bool shouldKick = false;

            if (_targetPlayer != null && track.TotalLength > 1f)
            {
                dzToPlayer = SignedTrackDelta(car.TrackDistance, _targetPlayer.TrackDistance, track.TotalLength);
                float absDz = Mathf.Abs(dzToPlayer);

                if (absDz <= interceptRangeMeters)
                {
                    float blend = Mathf.Clamp01(1f - (absDz / interceptRangeMeters));
                    // Áp sát cạnh xe người chơi (giữ khoảng cách 1.6m để ép làn)
                    float sideSign = car.LateralOffset >= _targetPlayer.LateralOffset ? 1f : -1f;
                    float desiredSideOffset = _targetPlayer.LateralOffset + sideSign * 1.4f;
                    targetLateral = Mathf.Lerp(0f, desiredSideOffset, blend);

                    float dLat = Mathf.Abs(_targetPlayer.LateralOffset - car.LateralOffset);
                    if (absDz <= strikeRangeLongitudinal && dLat <= strikeRangeLateral && car.CanKick)
                    {
                        shouldKick = true;
                    }
                }
            }

            // 4. Steer toward look-ahead aim point with intercept lateral offset
            Vector2 nextWpPos = track.GetWaypoint(targetWaypoint + 1);
            float lookBlend = Mathf.Clamp01(1f - distToWp / 35f) * 0.45f;
            Vector2 baseTarget = Vector2.Lerp(wpPos, nextWpPos, lookBlend);

            Vector2 segDir = track.GetTrackDirection(targetWaypoint);
            Vector2 segRight = new Vector2(segDir.y, -segDir.x);
            float maxOffset = track.TrackWidth * 0.38f;
            float clampedLat = Mathf.Clamp(targetLateral, -maxOffset, maxOffset);
            Vector2 aimPoint = baseTarget + segRight * clampedLat;

            Vector2 toTarget = (aimPoint - car.Physics.Position).normalized;
            float angleErrorDeg = SignedAngleRad(car.Physics.Forward, toTarget) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(-angleErrorDeg / Mathf.Max(5f, maxSteerAngleDeg), -1f, 1f);

            // 5. Speed modulation (catch up when behind player, match pace when alongside)
            float catchUpFactor = 1.0f;
            if (_targetPlayer != null)
            {
                if (dzToPlayer > 8f)
                {
                    catchUpFactor = 1.08f; // Tăng tốc đuổi kịp khi ở phía sau người chơi
                }
                else if (dzToPlayer < -10f)
                {
                    catchUpFactor = 0.80f; // Giảm nhẹ tốc độ nếu vượt quá xa phía trước để giữ thế vây ép
                }
            }

            Vector2 dirHere = track.GetTrackDirection(targetWaypoint - 1);
            Vector2 dirNext = track.GetTrackDirection(targetWaypoint);
            float turnAngle = Vector2.Angle(dirHere, dirNext);
            float sharpness = Mathf.Clamp01(turnAngle / 75f);

            float desiredTopSpeed = car.Physics.MaxSpeed * catchUpFactor;
            float cornerTargetSpeed = Mathf.Lerp(desiredTopSpeed, minCornerSpeed, sharpness * 0.78f);

            float currentSpeed = car.Physics.ForwardSpeed;
            float throttle = 0f;
            float brake = 0f;

            if (currentSpeed > cornerTargetSpeed + 5f)
            {
                brake = Mathf.Clamp01((currentSpeed - cornerTargetSpeed) / 14f);
            }
            else
            {
                float steerPenalty = Mathf.Lerp(1f, 0.72f, Mathf.Abs(steer) * 0.5f);
                throttle = Mathf.Clamp01(catchUpFactor * steerPenalty);
            }

            return new VehicleInput(throttle, brake, steer, shouldKick);
        }

        public static float SignedTrackDelta(float fromDistance, float toDistance, float totalLength)
        {
            if (totalLength <= 0.01f) return toDistance - fromDistance;
            float dz = toDistance - fromDistance;
            if (dz < -totalLength * 0.5f) dz += totalLength;
            if (dz > totalLength * 0.5f) dz -= totalLength;
            return dz;
        }

        private static float SignedAngleRad(Vector2 from, Vector2 to)
        {
            float cross = from.x * to.y - from.y * to.x;
            float dot = Vector2.Dot(from, to);
            return Mathf.Atan2(cross, dot);
        }
    }
}
