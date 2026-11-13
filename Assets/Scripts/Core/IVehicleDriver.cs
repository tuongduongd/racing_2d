namespace RacingGame
{
    /// <summary>
    /// Normalized control input sent to VehiclePhysics by either the player or AI.
    /// </summary>
    public struct VehicleInput
    {
        /// <summary>Throttle amount in [0, 1].</summary>
        public float throttle;

        /// <summary>Brake amount in [0, 1].</summary>
        public float brake;

        /// <summary>Steering amount in [-1, 1] (-1 = left, +1 = right).</summary>
        public float steering;

        public VehicleInput(float throttle, float brake, float steering)
        {
            this.throttle = throttle;
            this.brake = brake;
            this.steering = steering;
        }
    }

    /// <summary>
    /// Common driver interface so VehiclePhysics / Car does not know whether
    /// a human player or an AI opponent is controlling the vehicle.
    /// </summary>
    public interface IVehicleDriver
    {
        VehicleInput DecideInput(Car car, WaypointTrack track);
    }
}
