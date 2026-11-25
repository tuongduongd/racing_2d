using System;

namespace RacingGame
{
    /// <summary>
    /// Normalized control input sent to VehiclePhysics by any Driver (Player or AI).
    /// Marked [Serializable] so Unity inspector/test serialization never triggers UAC1001 warnings.
    /// </summary>
    [Serializable]
    public struct VehicleInput
    {
        /// <summary>Throttle amount in [0, 1].</summary>
        public float throttle;

        /// <summary>Brake amount in [0, 1].</summary>
        public float brake;

        /// <summary>Steering amount in [-1, 1] (-1 = left, +1 = right).</summary>
        public float steering;

        /// <summary>True on the step when a kick attack is triggered.</summary>
        public bool kick;

        public VehicleInput(float throttle, float brake, float steering)
            : this(throttle, brake, steering, false)
        {
        }

        public VehicleInput(float throttle, float brake, float steering, bool kick)
        {
            this.throttle = CoreMath.Clamp01(throttle);
            this.brake = CoreMath.Clamp01(brake);
            this.steering = CoreMath.Clamp(steering, -1f, 1f);
            this.kick = kick;
        }
    }
}
