using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Normalized control input sent to VehiclePhysics by any Driver (Player or AI).
    /// </summary>
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
            this.throttle = Mathf.Clamp01(throttle);
            this.brake = Mathf.Clamp01(brake);
            this.steering = Mathf.Clamp(steering, -1f, 1f);
            this.kick = kick;
        }
    }

    /// <summary>
    /// Abstract base class for all vehicle drivers (PlayerDriver, AIRacerDriver).
    /// Demonstrates OOP Abstraction, Inheritance, and Polymorphism:
    /// Car interacts only with Driver without knowing whether a human or AI is driving.
    /// </summary>
    public abstract class Driver : MonoBehaviour
    {
        [SerializeField] private string driverName = "Racer";

        public string DriverName
        {
            get => driverName;
            protected set => driverName = value;
        }

        public void SetDriverName(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                driverName = newName;
            }
        }

        /// <summary>
        /// Polymorphic decision method implemented by each concrete driver subclass.
        /// </summary>
        public abstract VehicleInput DecideInput(Car car, WaypointTrack track);

        /// <summary>
        /// Optional hook called when the race or vehicle resets.
        /// </summary>
        public virtual void OnReset()
        {
        }
    }
}
