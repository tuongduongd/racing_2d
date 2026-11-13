using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Concrete Driver implementation for the human player.
    /// Inherits from abstract Driver and delegates input reading to InputHandler.
    /// </summary>
    public class PlayerDriver : Driver
    {
        [SerializeField] private InputHandler inputHandler;

        public void SetInputHandler(InputHandler handler)
        {
            inputHandler = handler;
        }

        public override VehicleInput DecideInput(Car car, WaypointTrack track)
        {
            if (inputHandler == null)
            {
                return default;
            }
            return inputHandler.ReadInput();
        }

        public override void OnReset()
        {
            if (inputHandler != null)
            {
                inputHandler.ResetState();
            }
        }
    }
}
