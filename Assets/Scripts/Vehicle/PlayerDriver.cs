using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Concrete Driver implementation for the human player.
    /// Inherits from abstract Driver and delegates input reading to IInputProvider (DIP).
    /// </summary>
    public class PlayerDriver : Driver
    {
        [SerializeField] private InputHandler inputHandler;

        private IInputProvider _inputProvider;

        public override bool UsesArcadeAssist => true;

        private IInputProvider ActiveProvider => _inputProvider ?? inputHandler;

        public void SetInputHandler(InputHandler handler)
        {
            inputHandler = handler;
            _inputProvider = handler;
        }

        public void SetInputProvider(IInputProvider provider)
        {
            _inputProvider = provider;
        }

        public override VehicleInput DecideInput(Car car, WaypointTrack track)
        {
            IInputProvider provider = ActiveProvider;
            if (provider == null)
            {
                return default;
            }
            return provider.ReadInput();
        }

        public override void OnReset()
        {
            IInputProvider provider = ActiveProvider;
            if (provider != null)
            {
                provider.ResetState();
            }
        }
    }
}
