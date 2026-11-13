using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Combines VehiclePhysics, an IVehicleDriver (Player or AI), and track-relative state.
    /// </summary>
    public class Car : MonoBehaviour
    {
        [Header("Core Physics")]
        public VehiclePhysics physics = new VehiclePhysics();

        [Header("Runtime State")]
        public bool isPlayer = true;
        public bool isOnTrack = true;
        public int currentWaypointIndex;
        public float trackDistance;
        public float lateralOffset;

        /// <summary>Visual lean in [-1, 1] for Road Rash bike/car rendering.</summary>
        public float leanAngle { get; private set; }
        public VehicleInput lastInput { get; private set; }

        private IVehicleDriver _driver;

        public void Initialize(IVehicleDriver driver, Vector2 startPos, float startHeadingRad, WaypointTrack track)
        {
            _driver = driver;
            physics.Reset(startPos, startHeadingRad);
            leanAngle = 0f;
            UpdateTrackProjection(track);
        }

        public void SetDriver(IVehicleDriver driver)
        {
            _driver = driver;
        }

        public void StepPhysics(float dt, WaypointTrack track)
        {
            if (physics == null || dt <= 0f) return;

            if (_driver != null)
            {
                lastInput = _driver.DecideInput(this, track);
            }
            else
            {
                lastInput = default;
            }

            physics.ApplyThrottle(lastInput.throttle);
            physics.ApplyBrake(lastInput.brake);
            physics.ApplySteering(lastInput.steering);

            // Check road surface before stepping physics
            if (track != null)
            {
                TrackProjection proj = track.ProjectPosition(physics.position);
                isOnTrack = proj.isOnTrack;
                currentWaypointIndex = proj.segmentIndex;
                trackDistance = proj.distanceAlongTrack;
                lateralOffset = proj.lateralOffset;

                if (isOnTrack)
                {
                    physics.SetSurfaceModifier(1f, 1f, 1f);
                }
                else
                {
                    // Off-road grass/dirt slows vehicle down and reduces lateral grip
                    physics.SetSurfaceModifier(3.0f, 0.55f, 0.55f);
                }
            }

            physics.Update(dt);

            // Re-project and enforce outer shoulder barrier so player cannot leave the world
            if (track != null)
            {
                TrackProjection proj = track.ProjectPosition(physics.position);
                float maxLateral = track.maxOffRoadWidth * 0.5f;
                if (Mathf.Abs(proj.lateralOffset) > maxLateral)
                {
                    float clampedLat = Mathf.Sign(proj.lateralOffset) * maxLateral;
                    Vector2 right = new Vector2(proj.tangent.y, -proj.tangent.x);
                    physics.position = proj.closestPoint + right * clampedLat;

                    // Dampen velocity pushing into the barrier
                    float outwardSpeed = Vector2.Dot(physics.velocity, right) * Mathf.Sign(proj.lateralOffset);
                    if (outwardSpeed > 0f)
                    {
                        physics.velocity -= right * (outwardSpeed * Mathf.Sign(proj.lateralOffset) * 1.2f);
                        physics.velocity *= 0.85f;
                    }
                    proj = track.ProjectPosition(physics.position);
                }

                isOnTrack = proj.isOnTrack;
                currentWaypointIndex = proj.segmentIndex;
                trackDistance = proj.distanceAlongTrack;
                lateralOffset = proj.lateralOffset;
            }

            // Smooth visual lean for Road Rash bike/vehicle perspective
            float speedFactor = Mathf.Clamp01(physics.Speed / 18f);
            float targetLean = lastInput.steering * speedFactor;
            leanAngle = Mathf.Lerp(leanAngle, targetLean, 1f - Mathf.Exp(-10f * dt));

            // Keep Transform synced in 2D world space (useful for debugging / Gizmos)
            transform.position = new Vector3(physics.position.x, physics.position.y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, physics.heading * Mathf.Rad2Deg);
        }

        private void UpdateTrackProjection(WaypointTrack track)
        {
            if (track == null) return;
            TrackProjection proj = track.ProjectPosition(physics.position);
            isOnTrack = proj.isOnTrack;
            currentWaypointIndex = proj.segmentIndex;
            trackDistance = proj.distanceAlongTrack;
            lateralOffset = proj.lateralOffset;
        }
    }
}
