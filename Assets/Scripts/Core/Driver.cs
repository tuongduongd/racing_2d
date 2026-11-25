using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Abstract base class for all vehicle drivers (PlayerDriver, AIRacerDriver, PoliceRacerDriver).
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

        /// <summary>
        /// Polymorphic capability hook replacing runtime type checks (0 is/as downcast).
        /// </summary>
        public virtual bool UsesArcadeAssist => false;

        /// <summary>
        /// Polymorphic capability hook enabling AI off-road recovery without runtime downcasting.
        /// </summary>
        public virtual bool UsesAiOffRoadRecovery => false;

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
